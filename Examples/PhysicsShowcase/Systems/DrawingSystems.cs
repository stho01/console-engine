using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;

namespace PhysicsShowcase.Systems;

/// <summary>Draws a sleeping body dark grey and an awake one in its <see cref="Tint"/>.</summary>
public sealed class TintSystem : EntitySystem
{
    private Query _bodies;

    protected override void OnCreate() => _bodies = World.Query(Query.With<Tint, RigidBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bodies.GetChunks())
        {
            var tints = chunk.GetSpan<Tint>();
            var bodies = chunk.GetSpan<RigidBody>();
            var outlines = chunk.Has<Outline>() ? chunk.GetSpan<Outline>() : default;
            var glyphs = chunk.Has<Glyph>() ? chunk.GetSpan<Glyph>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                var color = bodies[i].IsSleeping ? ConsoleColor.DarkGray : tints[i].Color;
                if (!outlines.IsEmpty)
                    outlines[i].Color = color;
                if (!glyphs.IsEmpty)
                    glyphs[i].Foreground = color;
            }
        }
    }
}

/// <summary>Draws the solid tiles of the level.</summary>
public sealed class TileRenderSystem(RenderConsole console, TileGrid tiles) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        var rows = Math.Min(tiles.Rows, console.Height);
        var columns = Math.Min(tiles.Columns, console.Width);
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            if (tiles.IsSolid(column, row))
                console.Draw(column, row, '█', ConsoleColor.DarkGray);
        }
    }
}

/// <summary>
/// The player's laser: a <see cref="IPhysicsQueries.Raycast"/> in the direction the player faces, drawn up to what it
/// hits (tile, body or nothing), with an × on the hit point.
/// </summary>
public sealed class LaserSystem(RenderConsole console, IPhysicsQueries physics) : EntitySystem
{
    private const float Range = 60f;

    private Query _players;

    protected override void OnCreate() => _players = World.Query(Query.With<Player, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _players.GetChunks())
        {
            var players = chunk.GetSpan<Player>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var origin = positions[i].Value;
                var facing = players[i].Facing == 0f ? 1f : players[i].Facing;
                var translation = new Vector2(facing * Range, 0f);
                var hit = physics.Raycast(origin, translation, Layers.Tiles | Layers.Solid | Layers.Ghost,
                    out var result);
                var end = hit ? result.Point : origin + translation;
                console.DrawLine(Cell(origin + new Vector2(facing, 0f)), Cell(end), '-', ConsoleColor.DarkRed);
                if (hit)
                    console.Draw((int)MathF.Floor(end.X), (int)MathF.Floor(end.Y), '×', ConsoleColor.Red);
            }
        }
    }

    private static Point Cell(Vector2 position) => new((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
}

/// <summary>
/// The two HUD lines: live counts on row 0 (numbers drawn digit by digit, so nothing is allocated per frame) and the keys
/// on row 1.
/// </summary>
public sealed class HudSystem(RenderConsole console, PhysicsSystem physics, ShowcaseState state) : EntitySystem
{
    private const string Keys =
        " 1 BALL  2 BOX  3 TRIANGLE  4 PENTAGON  5 GHOST  ENTER SHOWER  A D W MOVE  E BLAST  G GRAVITY  I SMOOTH  " +
        "R CLEAR  ESC QUIT";

    private const ConsoleColor Foreground = ConsoleColor.Black;
    private const ConsoleColor Background = ConsoleColor.Gray;

    protected override void OnUpdate(float deltaSeconds)
    {
        for (var x = 0; x < console.Width; x++)
        {
            console.Draw(x, 0, ' ', Foreground, Background);
            console.Draw(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.DarkBlue);
        }

        var column = 1;
        column = Field(column, "BODIES ", physics.BodyCount);
        column = Field(column, "ASLEEP ", physics.SleepingBodyCount);
        column = Field(column, "CONTACTS ", physics.ContactCount);
        column = Field(column, "IN ZONE ", state.InZone);
        column = Field(column, "IMPACTS ", state.Impacts);
        column = Label(column, physics.Gravity.Y >= 0f ? "GRAVITY DOWN  " : "GRAVITY UP  ");
        Label(column, state.Smoothing ? "SMOOTH ON" : "SMOOTH OFF");
        console.Draw(0, 1, Keys, ConsoleColor.Gray, ConsoleColor.DarkBlue);
    }

    private int Field(int column, string label, int value)
    {
        column = Label(column, label);
        column = Number(column, value);
        return column + 3;
    }

    private int Label(int column, string label)
    {
        console.Draw(column, 0, label, Foreground, Background);
        return column + label.Length;
    }

    private int Number(int column, int value)
    {
        var digits = 1;
        for (var rest = value / 10; rest > 0; rest /= 10)
            digits++;

        for (var d = digits - 1; d >= 0; d--, value /= 10)
            console.Draw(column + d, 0, (char)('0' + value % 10), Foreground, Background);
        return column + digits;
    }
}
