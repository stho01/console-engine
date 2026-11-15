using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Collision;

/// <summary>A wall box standing on the line from <see cref="Start"/> to <see cref="End"/>, as thick as twice
/// <see cref="HalfThickness"/> (Kartster's fence boxes and outer wall, boundaries, road-edge walls, gate poles,
/// building walls, pillars).</summary>
public readonly record struct WallSegment(Vector2 Start, Vector2 End, float HalfThickness);

/// <summary>A round post (Kartster's pipes, tree trunks, lamp posts).</summary>
public readonly record struct WallPost(Vector2 Center, float Radius);

/// <summary>
/// What karts hit beside the road: the walls and posts of Kartster's collision mesh (what its
/// <c>LevelLoader.LoadCollisionMesh</c> builds), loaded from <c>&lt;id&gt;.collision.txt</c> and stood in the
/// <see cref="World"/> as static physics bodies. One line per shape, <c>#</c> starts a comment, metres in the physics
/// plane (x = world X, y = world Z):
/// <code>
/// segment x1 y1 x2 y2 halfThickness
/// circle x y radius
/// </code>
/// A <c>segment</c> is a box centred on the line, ending at its end points; a <c>circle</c> is a post. Kartster
/// resolves its collision mesh with the kart's own restitution and friction (<see cref="KartEntity.Restitution"/>,
/// <see cref="KartEntity.Friction"/>), so the walls carry the same: the physics mixes friction as
/// <c>sqrt(kart × wall)</c> and restitution as the larger, which gives Kartster's numbers back.
/// </summary>
public sealed class TrackCollision(WallSegment[] segments, WallPost[] posts)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string FileSuffix = ".collision.txt";

    /// <summary>Wall friction: with the kart's 0.3 the pair's is <c>sqrt(0.3 × 0.3)</c>, Kartster's 0.3.</summary>
    public const float Friction = KartEntity.Friction;

    public const float Restitution = KartEntity.Restitution;

    public static readonly TrackCollision Empty = new([], []);

    //**********************************************************
    //** props
    //**********************************************************

    public WallSegment[] Segments => segments;
    public WallPost[] Posts => posts;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Loads <c>&lt;id&gt;.collision.txt</c> from <paramref name="folder"/>; <see cref="Empty"/> if there is
    /// none (the track has no walls).</summary>
    public static TrackCollision Load(string folder, string id)
    {
        var path = Path.Combine(folder, id + FileSuffix);
        return File.Exists(path) ? Parse(File.ReadLines(path), Path.GetFileName(path)) : Empty;
    }

    /// <param name="source">The file name, for error messages.</param>
    /// <exception cref="InvalidDataException">A line is not a valid segment or circle line.</exception>
    public static TrackCollision Parse(IEnumerable<string> lines, string source)
    {
        var segments = new List<WallSegment>();
        var posts = new List<WallPost>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            var hash = line.IndexOf('#');
            var parts = (hash < 0 ? line : line[..hash]).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "segment")
            {
                if (parts.Length != 6)
                    throw Error(source, lineNumber, "expected 'segment x1 y1 x2 y2 halfThickness'");

                var start = new Vector2(Number(parts[1], source, lineNumber), Number(parts[2], source, lineNumber));
                var end = new Vector2(Number(parts[3], source, lineNumber), Number(parts[4], source, lineNumber));
                var half = Positive(parts[5], source, lineNumber);
                if (start == end)
                    throw Error(source, lineNumber, "segment has no length");
                segments.Add(new WallSegment(start, end, half));
            }
            else if (parts[0] == "circle")
            {
                if (parts.Length != 4)
                    throw Error(source, lineNumber, "expected 'circle x y radius'");

                var center = new Vector2(Number(parts[1], source, lineNumber), Number(parts[2], source, lineNumber));
                posts.Add(new WallPost(center, Positive(parts[3], source, lineNumber)));
            }
            else
            {
                throw Error(source, lineNumber, $"unknown line '{parts[0]}', expected segment or circle");
            }
        }

        return new TrackCollision(segments.ToArray(), posts.ToArray());
    }

    /// <summary>
    /// Stands every shape in <paramref name="world"/> as a static body (a <see cref="Collider"/> without a
    /// <see cref="RigidBody"/>): a turned box per segment, a circle per post. Returns how many.
    /// </summary>
    public int AddTo(World world)
    {
        foreach (var segment in segments)
        {
            var along = segment.End - segment.Start;
            var middle = (segment.Start + segment.End) / 2f;
            world.Create(
                new Position(new XnaVector2(middle.X, middle.Y)),
                new Rotation(MathF.Atan2(along.Y, along.X)), // turns the box's x axis onto the line
                Collider.Box(new XnaVector2(along.Length() / 2f, segment.HalfThickness)) with
                {
                    Friction = Friction, Restitution = Restitution,
                });
        }

        foreach (var post in posts)
        {
            world.Create(
                new Position(new XnaVector2(post.Center.X, post.Center.Y)),
                Collider.Circle(post.Radius) with { Friction = Friction, Restitution = Restitution });
        }

        return segments.Length + posts.Length;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float Number(string text, string source, int lineNumber) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
            ? value
            : throw Error(source, lineNumber, $"'{text}' is not a number");

    private static float Positive(string text, string source, int lineNumber)
    {
        var value = Number(text, source, lineNumber);
        return value > 0f ? value : throw Error(source, lineNumber, $"'{text}' must be more than 0");
    }

    private static InvalidDataException Error(string source, int lineNumber, string message) =>
        new($"{source}:{lineNumber}: {message}");
}
