using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The daylight sky above the grass: a blue-to-cyan gradient, the sun, clouds drifting by and green hills on the
/// horizon. Covers every screen cell above the surface row, so the <see cref="Canvas"/> knows their backgrounds.
/// </summary>
public sealed class SkyRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>World cell rows of the sky (everything above the grass row).</summary>
    public const int SkyHeight = Mine.SurfaceRow * Layout.TileHeight;

    /// <summary>World rows above this are deep blue, rows from it on are cyan, the row itself is dithered.</summary>
    private const int HorizonBand = 6;

    private const int SunX = 98;
    private const int SunY = 3;
    private const float CloudSpeed = 1.5f;

    private static readonly string[] Sun = ["▄██▄", "▀██▀"];
    private static readonly string[] Cloud = [" ▄██▄▄ ", "▀██████▄", "  ▀▀▀▀▀ "];
    private static readonly (int X, int Y)[] CloudStarts = [(6, 2), (52, 5), (88, 8)];

    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        var originX = (int)MathF.Floor(camera.Position.X);
        var originY = (int)MathF.Floor(camera.Position.Y);
        var lastScreenRow = Math.Min(canvas.Height - 1, SkyHeight - 1 - originY);
        if (lastScreenRow < 0)
            return;

        for (var y = 0; y <= lastScreenRow; y++)
        {
            var worldY = y + originY;
            for (var x = 0; x < canvas.Width; x++)
                DrawSkyCell(x, y, x + originX, worldY);
        }

        DrawSprite(Sun, SunX, SunY, ConsoleColor.Yellow, originX, originY);
        for (var i = 0; i < CloudStarts.Length; i++)
        {
            var width = canvas.Width + Cloud[1].Length;
            var x = (int)(CloudStarts[i].X + _time * CloudSpeed * (1f + i * .3f)) % width - Cloud[1].Length;
            DrawSprite(Cloud, x, CloudStarts[i].Y, ConsoleColor.White, originX, originY);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The sky's background colour at a world cell row.</summary>
    public static ConsoleColor SkyColor(int worldY) => worldY < HorizonBand ? ConsoleColor.Blue : ConsoleColor.Cyan;

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawSkyCell(int x, int y, int worldX, int worldY)
    {
        var hillTop = SkyHeight - HillHeight(worldX);
        if (worldY >= hillTop)
        {
            // Far hills, with a lighter rim on top.
            var rim = worldY == hillTop;
            canvas.Draw(x, y, rim ? '▄' : '█', ConsoleColor.DarkGreen, rim ? SkyColor(worldY) : ConsoleColor.DarkGreen);
            return;
        }

        if (worldY == HorizonBand - 1)
            canvas.Draw(x, y, (worldX & 1) == 0 ? '▒' : '░', ConsoleColor.Cyan, ConsoleColor.Blue);
        else
            canvas.Draw(x, y, ' ', ConsoleColor.White, SkyColor(worldY));
    }

    /// <summary>Rolling hills from two sines, 1 to 5 cells high.</summary>
    private static int HillHeight(int worldX) =>
        3 + (int)MathF.Round(MathF.Sin(worldX * .07f) * 1.5f + MathF.Sin(worldX * .19f + 1f) * .8f);

    /// <summary>
    /// Draws the non-space characters of a sprite at a world cell over the sky, keeping its background. Parts behind
    /// the hills are hidden.
    /// </summary>
    private void DrawSprite(string[] rows, int worldX, int worldY, ConsoleColor color, int originX, int originY)
    {
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < rows[row].Length; column++)
        {
            var character = rows[row][column];
            var x = worldX + column;
            var y = worldY + row;
            if (character != ' ' && y < SkyHeight - HillHeight(x))
                canvas.DrawOver(x - originX, y - originY, character, color);
        }
    }
}
