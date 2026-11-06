using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Platformer.Ecs.Components;

namespace Platformer.Ecs.Systems;

/// <summary>
/// In debug mode, writes each visible tile's column (first line) and row (second line) on the tile, black on white.
/// Digits are drawn one by one, so there is no string per tile and frame.
/// </summary>
public sealed class TileDebugLabelSystem(PlatformerEcsGame game, RenderConsole console, Camera camera)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _tiles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _tiles = World.Query(Query.With<Position, MapCell, Tile>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (!game.IsDebugMode)
            return;

        var cameraPosition = camera.Position;
        foreach (var chunk in _tiles.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var cells = chunk.GetSpan<MapCell>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var screen = positions[i].Value - cameraPosition;
                var x = (int)MathF.Floor(screen.X);
                var y = (int)MathF.Floor(screen.Y);

                if (y + 1 < 0 || y >= console.Height || x >= console.Width)
                    continue;

                DrawNumber(x, y, cells[i].Column);
                DrawNumber(x, y + 1, cells[i].Row);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Draws a non-negative number left-aligned at (x, y). Cells outside the screen are clipped.</summary>
    private void DrawNumber(int x, int y, int value)
    {
        var digits = 1;
        for (var rest = value / 10; rest > 0; rest /= 10)
            digits++;

        for (var i = digits - 1; i >= 0; i--)
        {
            console.Draw(x + i, y, (char)('0' + value % 10), ConsoleColor.Black, ConsoleColor.White);
            value /= 10;
        }
    }
}
