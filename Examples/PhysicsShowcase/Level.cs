using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;

namespace PhysicsShowcase;

/// <summary>
/// The level: walls, floor, shelves and steps as a <see cref="TileGrid"/> of 1×1 tiles, plus bodies that show the other
/// kinds of colliders. Rows 0–1 are the HUD.
/// </summary>
public static class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Top = 2;
    public const float PlatformSpeed = 10f;
    public const float WindmillSpeed = 1.2f;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static TileGrid CreateTiles(int width, int height)
    {
        var tiles = new TileGrid(width, height, Vector2.One) { Category = Layers.Tiles };
        Fill(tiles, 0, 0, width - 1, Top);                      // ceiling, under the HUD rows too, so it's thick
        Fill(tiles, 0, height - 3, width - 1, height - 1);      // floor
        Fill(tiles, 0, Top, 0, height - 1);                     // left wall
        Fill(tiles, width - 1, Top, width - 1, height - 1);     // right wall
        Fill(tiles, 1, 40, 22, 40);                             // left shelf, under the ramp
        Fill(tiles, 85, 28, width - 2, 28);                     // right shelf
        Fill(tiles, 34, height - 6, 37, height - 4);            // two steps on the floor
        Fill(tiles, 38, height - 9, 41, height - 4);
        return tiles;
    }

    /// <summary>
    /// The colliders that aren't tiles. Returns the sensor zone's entity.
    /// <list type="bullet">
    /// <item>A static ramp: a turned box (static bodies can be turned too).</item>
    /// <item>An ice ledge: a static box without friction.</item>
    /// <item>A kinematic platform that patrols left and right, and a kinematic windmill that turns.</item>
    /// <item>A static sensor zone that counts what is inside.</item>
    /// </list>
    /// </summary>
    public static Entity CreateProps(World world, OutlineLibrary outlines, int width, int height)
    {
        CreateStaticBox(world, outlines, new Vector2(16, 34), new Vector2(9, .5f), .35f, '▓', ConsoleColor.DarkYellow,
            friction: Collider.DefaultFriction);
        CreateStaticBox(world, outlines, new Vector2(60, 16.5f), new Vector2(12, .5f), 0f, '═', ConsoleColor.Cyan,
            friction: 0f);

        var platformOutline = outlines.Add(BoxCorners(new Vector2(6, .5f)));
        world.Create(
            new Position(new Vector2(40, 30.5f)),
            new Velocity(new Vector2(PlatformSpeed, 0)),
            RigidBody.Kinematic(),
            Collider.Box(new Vector2(6, .5f)) with { Category = Layers.Solid },
            new Outline(platformOutline, '▒', ConsoleColor.Magenta),
            new Patrol(30, 52, PlatformSpeed));

        var windmillOutline = outlines.Add(BoxCorners(new Vector2(9, .5f)));
        var windmill = world.Create(
            new Position(new Vector2(62, 44)),
            new Velocity(Vector2.Zero),
            new Rotation(0f),
            new AngularVelocity(WindmillSpeed),
            RigidBody.Kinematic(),
            Collider.Box(new Vector2(9, .5f)) with { Category = Layers.Solid });
        world.Add(windmill, new Outline(windmillOutline, '#', ConsoleColor.Red));

        var zoneHalf = new Vector2(12, 3.5f);
        var zoneOutline = outlines.Add(BoxCorners(zoneHalf));
        return world.Create(
            new Position(new Vector2(width - 15, height - 6.5f)),
            Collider.Box(zoneHalf) with
            {
                IsSensor = true,
                Category = Layers.Zone,
                Mask = Layers.Solid | Layers.Ghost
            },
            new Outline(zoneOutline, '·', ConsoleColor.DarkGreen));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void CreateStaticBox(World world, OutlineLibrary outlines, Vector2 center, Vector2 halfSize,
        float angle, char character, ConsoleColor color, float friction)
    {
        var outline = outlines.Add(BoxCorners(halfSize));
        world.Create(
            new Position(center),
            new Rotation(angle),
            Collider.Box(halfSize) with { Category = Layers.Solid, Friction = friction },
            new Outline(outline, character, color));
    }

    private static Vector2[] BoxCorners(Vector2 half) =>
        OutlineShape.ThroughCellCentres([new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y),
            new(-half.X, half.Y)]);

    private static void Fill(TileGrid tiles, int left, int top, int right, int bottom)
    {
        for (var row = top; row <= bottom; row++)
        for (var column = left; column <= right; column++)
            tiles.SetSolid(column, row);
    }
}
