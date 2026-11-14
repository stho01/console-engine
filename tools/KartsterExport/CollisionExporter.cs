using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Export;
using Kartster.Models.Tracks.Frames;
using Microsoft.Xna.Framework;

namespace KartsterExport;

/// <summary>
/// What karts hit beside the road, as 2D shapes in the plane (x = world X, y = world Z): what
/// <c>LevelLoader.LoadCollisionMesh</c> puts in the collision mesh (fences and outer wall, boundaries, start gate
/// poles, pipes) and the road-edge walls <c>KartWallConstraint</c> collides with. Height is dropped. Refuses a track
/// with something it cannot write yet rather than write one with missing walls.
/// Format: <c>Examples/Kartster/Collision/TrackCollision.cs</c>.
/// </summary>
internal sealed class CollisionExporter : ITrackExporter
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Half the thickness given to Kartster's zero-thick walls (boundaries, edge walls): a fence's.</summary>
    private const float ThinWallHalf = 0.25f;

    /// <summary>How far an edge wall's polyline may stray from one segment before a new one starts.</summary>
    private const float EdgeTolerance = 0.02f;

    private readonly StringBuilder _text = new();
    private int _segments;
    private int _circles;

    //**********************************************************
    //** props
    //**********************************************************

    public string Suffix => ".collision.txt";

    //**********************************************************
    //** public methods
    //**********************************************************

    public string Export(string id, TrackGraph track)
    {
        var unsupported = Unsupported(track);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"not exported yet: {string.Join(", ", unsupported)}");

        _text.Clear();
        _segments = 0;
        _circles = 0;
        _text.Append($"# {id} collision, exported from Kartster by CollisionExport. " +
                     "Format: Examples/Kartster/Collision/TrackCollision.cs\n");
        _text.Append("# segment x1 y1 x2 y2 halfThickness   (a wall box standing on the line, metres)\n");
        _text.Append("# circle x y radius                    (a post)\n");

        // TrackFenceMesh.ThicknessMeters: the boxes are centred on the line.
        var thickness = (float)typeof(TrackFenceMesh)
            .GetField("ThicknessMeters", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        if (track.Ground is TrackGround ground)
        {
            var nw = new Vector2(ground.MinX, ground.MinZ);
            var ne = new Vector2(ground.MaxX, ground.MinZ);
            var se = new Vector2(ground.MaxX, ground.MaxZ);
            var sw = new Vector2(ground.MinX, ground.MaxZ);
            _text.Append("# outer wall\n");
            Segment(nw, ne, thickness / 2f);
            Segment(ne, se, thickness / 2f);
            Segment(se, sw, thickness / 2f);
            Segment(sw, nw, thickness / 2f);
            _text.Append("# fences\n");
            foreach (var fence in ground.Fences)
                Segment(fence.Start, fence.End, thickness / 2f);
        }

        WriteBoundaries(track.Boundaries);
        WriteEdgeWalls(track);
        WriteGatePoles(id, track);

        _text.Append("# pipes (Kartster: an eight-sided post with corners on this radius)\n");
        foreach (var pipe in TrackPipes.Place(track))
            Circle(pipe.Position.X, pipe.Position.Z, pipe.Radius);

        Console.WriteLine($"  collision: segment={_segments} circle={_circles}");
        return _text.ToString();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>What <c>LoadCollisionMesh</c> collides with that is not written.</summary>
    private static List<string> Unsupported(TrackGraph track)
    {
        var unsupported = new List<string>();
        if (TrackRouteEnds.Build(track).Length > 0)
            unsupported.Add("route ends");
        Count("trees", track.Trees.Count);
        Count("buildings", track.Buildings.Count);
        Count("lamps", track.Lamps.Count);
        Count("roadblocks", track.Roadblocks.Count);
        Count("medians", track.Medians.Count);
        Count("guardrails", track.Guardrails.Count);
        Count("pillars", track.Pillars.Count);
        Count("bridge pillars", TrackBridgeMesh.Build(track).Pillars.Length);
        Count("tunnel ceiling triangles", TrackTunnelMesh.Build(track).Ceiling.Length / 3);
        Count("street drops", track.Streets.Sum(street => street.Drops.Count));
        Count("direction signs", track.DirectionSigns.Count);
        return unsupported;

        void Count(string what, int count)
        {
            if (count > 0)
                unsupported.Add($"{count} {what}");
        }
    }

    /// <summary><c>TrackBoundaryMesh.BuildWalls</c>: one zero-thick wall along each boundary's points.</summary>
    private void WriteBoundaries(IReadOnlyList<TrackBoundary> boundaries)
    {
        if (boundaries.Count == 0)
            return;

        _text.Append($"# boundaries (Kartster: zero-thick walls, here {ThinWallHalf * 2f:0.##} m)\n");
        foreach (var boundary in boundaries)
        {
            for (var index = 1; index < boundary.Points.Count; index++)
            {
                var from = Flat(boundary.Points[index - 1]);
                var to = Flat(boundary.Points[index]);
                if (Vector2.DistanceSquared(from, to) > 1e-6f)
                    Segment(from, to, ThinWallHalf);
            }
        }
    }

    /// <summary>
    /// The road-edge walls of sections with a <c>WallHeight</c>: <c>KartWallConstraint</c>'s plane on each edge of the
    /// frames the ribbon's <c>*_wall_left/right</c> strips stand on. Each box lies just outside the edge, so its inner
    /// face is the plane, and a straight run of frames is one box.
    /// </summary>
    private void WriteEdgeWalls(TrackGraph track)
    {
        var walled = new TrackExportFrameBuilder().Build(track)
            .Where(s => s.Section.WallHeight > 0f && s.Frames.Count >= 2).ToArray();
        if (walled.Length == 0)
            return;

        _text.Append($"# edge walls (Kartster: a plane on the road edge; here a box outside it, {ThinWallHalf * 2f:0.##}" +
                     " m thick)\n");
        foreach (var section in walled)
        {
            EdgeWall(section.Frames, left: true);
            EdgeWall(section.Frames, left: false);
        }
    }

    private void EdgeWall(IReadOnlyList<TrackFrame> frames, bool left)
    {
        var points = new Vector2[frames.Count];
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            var outward = Vector2.Normalize(Flat(left ? -frame.Right : frame.Right));
            points[index] = Flat(left ? frame.LeftEdge : frame.RightEdge) + outward * ThinWallHalf;
        }

        // Greedy: extend the segment while every point it skips lies within EdgeTolerance of it.
        var start = 0;
        for (var end = 2; end <= points.Length; end++)
        {
            if (end < points.Length && Straight(points, start, end))
                continue;
            Segment(points[start], points[end - 1], ThinWallHalf);
            start = end - 1;
        }
    }

    private static bool Straight(Vector2[] points, int start, int end)
    {
        var a = points[start];
        var along = points[end] - a;
        var length = along.Length();
        if (length < 1e-4f)
            return false;
        for (var index = start + 1; index < end; index++)
        {
            var offset = points[index] - a;
            if (MathF.Abs(offset.X * along.Y - offset.Y * along.X) / length > EdgeTolerance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// <c>StartGateMesh.BuildPoles</c>: a square pole <c>PoleHalfWidthMeters</c> either way, upright, its sides along
    /// the levelled road right, <c>PoleOffsetMeters</c> outside each edge of every gate <c>PlaceGates</c> puts out.
    /// </summary>
    private void WriteGatePoles(string id, TrackGraph track)
    {
        if (!RaceMarkings.HasGate(track.StartFinishStyle))
            return;

        _text.Append("# start gate poles\n");
        const float half = StartGateMesh.PoleHalfWidthMeters;
        foreach (var gate in RaceMarkings.PlaceGates(track, SceneryExporter.Surface(id, track)))
        {
            var right = Vector2.Normalize(Flat(gate.Right));
            var leftFoot = Flat(gate.Center - gate.Right * (gate.LeftWidth + StartGateMesh.PoleOffsetMeters));
            var rightFoot = Flat(gate.Center + gate.Right * (gate.RightWidth + StartGateMesh.PoleOffsetMeters));

            // The poles the mesh builds, left then right: their middles must be these feet.
            var mesh = StartGateMesh.BuildPoles(gate);
            CheckPole(id, mesh.Take(mesh.Length / 2), leftFoot);
            CheckPole(id, mesh.Skip(mesh.Length / 2), rightFoot);

            Segment(leftFoot - right * half, leftFoot + right * half, half);
            Segment(rightFoot - right * half, rightFoot + right * half, half);
        }
    }

    private static void CheckPole(string id, IEnumerable<Microsoft.Xna.Framework.Graphics.VertexPositionNormalTexture> pole,
        Vector2 foot)
    {
        var vertices = pole.ToArray();
        var middle = vertices.Aggregate(Vector2.Zero, (sum, v) => sum + Flat(v.Position)) / vertices.Length;
        if (Vector2.Distance(middle, foot) > 0.01f)
            throw new InvalidOperationException($"{id}: gate pole at {middle}, expected {foot}");
    }

    private static Vector2 Flat(Vector3 point) => new(point.X, point.Z);

    private void Segment(Vector2 a, Vector2 b, float halfThickness)
    {
        _text.Append($"segment {a.X:0.###} {a.Y:0.###} {b.X:0.###} {b.Y:0.###} {halfThickness:0.###}\n");
        _segments++;
    }

    private void Circle(float x, float y, float radius)
    {
        _text.Append($"circle {x:0.###} {y:0.###} {radius:0.###}\n");
        _circles++;
    }
}
