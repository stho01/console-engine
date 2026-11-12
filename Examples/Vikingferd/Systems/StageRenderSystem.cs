using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>
/// Paints every tile as 4 x 4 pixels over the backdrop: speckled rock with snow on top and icicles underneath, cracked
/// stone, spikes, the glowing exit doorway, and the switches in their channel colours. Air shows the backdrop.
/// </summary>
public sealed class StageRenderSystem(Canvas canvas, Stage stage) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Size = Layout.TilePixels;

    private readonly Point _origin = Layout.StagePixelOrigin(stage.Columns, stage.Rows);
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        for (var row = 0; row < stage.Rows; row++)
        for (var column = 0; column < stage.Columns; column++)
            DrawTile(column, row);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawTile(int column, int row)
    {
        var x = _origin.X + column * Size;
        var y = _origin.Y + row * Size;
        var accent = Looks.ChannelColors[stage.ChannelAt(column, row)];
        var on = stage.IsOn(column, row);

        switch (stage.KindAt(column, row))
        {
            case TileKind.Rock:
                DrawRock(x, y, column, row);
                break;
            case TileKind.Cracked:
                Looks.Cracked.Draw(canvas, x, y);
                DrawSnow(x, y, column, row);
                break;
            case TileKind.Spikes:
                var cellX = x;
                var cellY = (y >> 1) + 1;
                for (var i = 0; i < Looks.Spikes.Length; i++)
                    canvas.Glyph(cellX + i, cellY, Looks.Spikes[i], Looks.SpikeColor);
                break;
            case TileKind.Exit:
                DrawExit(x, y, column, row);
                break;
            case TileKind.Door:
                (on ? Looks.DoorOpen : Looks.DoorClosed).Draw(canvas, x, y, accent);
                break;
            case TileKind.Bridge:
                (on ? Looks.BridgeSolid : Looks.BridgeOpen).Draw(canvas, x, y, accent);
                break;
            case TileKind.Plate:
                (on ? Looks.PlatePressed : Looks.PlateUp).Draw(canvas, x, y, accent);
                break;
            case TileKind.Lever:
                (on ? Looks.LeverOn : Looks.LeverOff).Draw(canvas, x, y, accent);
                break;
        }
    }

    private void DrawRock(int x, int y, int column, int row)
    {
        // A bevelled stone block: light top-left edge, dark bottom-right edge, a few specks inside.
        for (var py = 0; py < Size; py++)
        for (var px = 0; px < Size; px++)
        {
            var hash = Looks.Hash(x + px, y + py);
            ConsoleColor color;
            if (px == Size - 1 || py == Size - 1)
                color = hash < 170 ? Looks.RockDark : Looks.RockColor;
            else if (px == 0 || py == 0)
                color = hash < 150 ? Looks.RockLight : Looks.RockColor;
            else
                color = hash < 30 ? Looks.RockLight : hash < 50 ? Looks.RockDark : Looks.RockColor;
            canvas.Pixel(x + px, y + py, color);
        }

        DrawSnow(x, y, column, row);

        // Icicles hang into the open tile below.
        if (row + 1 < stage.Rows && !stage.IsSolid(column, row + 1))
        {
            for (var px = 0; px < Size; px++)
            {
                var hash = Looks.Hash(x + px, y, 7);
                if (hash < 70)
                    canvas.Pixel(x + px, y + Size, ConsoleColor.Cyan);
                if (hash < 20)
                    canvas.Pixel(x + px, y + Size + 1, ConsoleColor.DarkCyan);
            }
        }
    }

    /// <summary>A white cap on a solid tile with open air above, a little uneven.</summary>
    private void DrawSnow(int x, int y, int column, int row)
    {
        if (row == 0 || stage.IsSolid(column, row - 1))
            return;

        for (var px = 0; px < Size; px++)
        {
            canvas.Pixel(x + px, y, Looks.Snow);
            if (Looks.Hash(x + px, y, 3) < 110)
                canvas.Pixel(x + px, y + 1, Looks.Snow);
        }
    }

    /// <summary>
    /// A wooden doorway with a warm, flickering light inside, two tiles high: the exit tile and the open tile above
    /// it. Neighbouring exit tiles share one wide doorway (posts only at the ends).
    /// </summary>
    private void DrawExit(int x, int y, int column, int row)
    {
        var leftPost = stage.KindAt(column - 1, row) != TileKind.Exit;
        var rightPost = stage.KindAt(column + 1, row) != TileKind.Exit;
        var hasTop = row > 0 && !stage.IsSolid(column, row - 1);
        var top = hasTop ? y - Size + 1 : y;
        var flicker = (int)(_time * 6f);

        for (var py = top; py < y + Size; py++)
        for (var px = 0; px < Size; px++)
        {
            var post = (px == 0 && leftPost) || (px == Size - 1 && rightPost);
            ConsoleColor color;
            if (py == top)
                color = Looks.ExitWood;
            else if (post)
                color = Looks.ExitWood;
            else
            {
                var hash = Looks.Hash(x + px, py, flicker);
                color = hash < 35 ? ConsoleColor.White : hash < 190 ? Looks.ExitGlow : ConsoleColor.DarkYellow;
            }

            canvas.Pixel(x + px, py, color);
        }
    }
}
