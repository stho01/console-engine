using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The surface buildings, drawn from rectangles in world cells: the house with its "EternalDigger" sign, door and
/// chimney smoke, the ladder shop with a striped awning, and the wooden frame over the mine shaft. Decoration only;
/// the miner walks in front of them.
/// </summary>
public sealed class HouseRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The last world cell row above the grass: where the buildings stand.</summary>
    private const int Ground = Layout.GroundRow * Layout.TileHeight + Layout.TileHeight - 1;

    // The house (left part, door over the deliver spot) and the shop (right part, over the shop spot).
    private const int HouseLeft = Layout.HouseFirstColumn * Layout.TileWidth;
    private const int ShopLeft = Layout.ShopFirstColumn * Layout.TileWidth;
    private const int ShopRight = (Layout.HouseLastColumn + 1) * Layout.TileWidth - 1;
    private const int HouseRight = ShopLeft - 1;
    private const int WallTop = Ground - 7;
    private const int RoofHeight = 5;
    private const int DoorLeft = (Layout.DeliverFirstColumn + Layout.DeliverLastColumn + 1) * Layout.TileWidth / 2 - 2;
    private const int ChimneyX = HouseRight - 6;

    /// <summary>The house name on its wooden sign, with a plank of padding on both sides.</summary>
    private const string SignBoard = " EternalDigger ";
    private const string ShopText = "LADDERS";
    private const string MineText = "MINE";

    private const float SmokeRise = 1.5f;
    private const int SmokePuffs = 4;
    private const int SmokeHeight = 6;

    private float _time;
    private int _originX;
    private int _originY;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        _originX = (int)MathF.Floor(camera.Position.X);
        _originY = (int)MathF.Floor(camera.Position.Y);
        if (WallTop - RoofHeight - 5 - _originY >= canvas.Height || Ground - _originY < 0)
            return;

        DrawHouse();
        DrawShop();
        DrawMineEntrance();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawHouse()
    {
        // Chimney first, so the roof covers its foot. Smoke rises from it.
        Fill(ChimneyX, WallTop - RoofHeight - 1, ChimneyX + 1, WallTop - 2, '▓', ConsoleColor.Gray, ConsoleColor.DarkGray);
        for (var i = 0; i < SmokePuffs; i++)
        {
            var rise = (_time * SmokeRise + i * SmokeHeight / (float)SmokePuffs) % SmokeHeight;
            var x = ChimneyX + (int)MathF.Round(MathF.Sin(_time + i * 1.7f) * rise * .4f + rise * .3f);
            var y = WallTop - RoofHeight - 2 - (int)rise;
            DrawOver(x, y, rise < SmokeHeight / 2f ? '○' : '°', ConsoleColor.Gray);
        }

        // Roof: a wedge of shingles, one cell narrower per row on both sides.
        for (var row = 0; row < RoofHeight; row++)
        {
            var y = WallTop - 1 - row;
            var inset = row * 2;
            for (var x = HouseLeft + inset; x <= HouseRight - inset; x++)
            {
                var edge = x == HouseLeft + inset || x == HouseRight - inset;
                Draw(x, y, edge ? '▄' : ((x + row) & 1) == 0 ? '▀' : '▄',
                    edge ? ConsoleColor.DarkRed : ConsoleColor.Red, edge ? Backdrop(x, y) : ConsoleColor.DarkRed);
            }
        }

        // Plank walls.
        for (var y = WallTop; y <= Ground; y++)
            Fill(HouseLeft + 1, y, HouseRight, y, (y & 1) == 0 ? '─' : ' ', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);

        // The sign on the wall, under the eaves.
        var signLeft = (HouseLeft + HouseRight + 1 - SignBoard.Length) / 2;
        Draw(signLeft, WallTop, SignBoard, ConsoleColor.Yellow, ConsoleColor.DarkRed);
        Draw(signLeft, WallTop + 1, '▀', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);
        Draw(signLeft + SignBoard.Length - 1, WallTop + 1, '▀', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);

        // Windows and the door (over the deliver spot).
        DrawWindow(HouseLeft + 2, WallTop + 3);
        DrawWindow(HouseRight - 5, WallTop + 3);
        Fill(DoorLeft, Ground - 4, DoorLeft + 3, Ground, '║', ConsoleColor.DarkYellow, ConsoleColor.DarkRed);
        Draw(DoorLeft, Ground - 4, "▄▄▄▄", ConsoleColor.DarkRed, ConsoleColor.DarkYellow);
        Draw(DoorLeft + 3, Ground - 2, '•', ConsoleColor.Yellow, ConsoleColor.DarkRed);
    }

    private void DrawWindow(int x, int y)
    {
        Draw(x, y, "┌┬┐", ConsoleColor.DarkRed, ConsoleColor.Cyan);
        Draw(x, y + 1, "└┴┘", ConsoleColor.DarkRed, ConsoleColor.Cyan);
    }

    private void DrawShop()
    {
        // Striped awning over a counter with ladders for sale.
        for (var x = ShopLeft; x <= ShopRight; x++)
        {
            var stripe = ((x - ShopLeft) / 2 & 1) == 0 ? ConsoleColor.Red : ConsoleColor.White;
            Draw(x, WallTop + 1, '▄', stripe, Backdrop(x, WallTop + 1));
            Draw(x, WallTop + 2, '▀', stripe, ConsoleColor.DarkYellow);
        }

        Fill(ShopLeft, WallTop + 3, ShopRight, Ground, ' ', ConsoleColor.Black, ConsoleColor.DarkYellow);
        Fill(ShopLeft + 2, WallTop + 3, ShopRight - 2, Ground - 2, ' ', ConsoleColor.Black, ConsoleColor.Black);
        for (var x = ShopLeft + 3; x <= ShopRight - 5; x += 4)
        {
            Draw(x, WallTop + 3, "╟─╢", ConsoleColor.DarkYellow, ConsoleColor.Black);
            Draw(x, WallTop + 4, "╟─╢", ConsoleColor.DarkYellow, ConsoleColor.Black);
        }

        Fill(ShopLeft + 1, Ground - 1, ShopRight - 1, Ground - 1, '▀', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);
        Draw((ShopLeft + ShopRight + 1 - ShopText.Length) / 2, Ground, ShopText, ConsoleColor.Yellow, ConsoleColor.DarkRed);
    }

    private void DrawMineEntrance()
    {
        var left = Mine.ShaftColumn * Layout.TileWidth - 1;
        var right = left + Layout.TileWidth + 1;
        var top = Ground - 4;
        Fill(left, top + 1, left, Ground, '║', ConsoleColor.DarkYellow, Backdrop(left, Ground));
        Fill(right, top + 1, right, Ground, '║', ConsoleColor.DarkYellow, Backdrop(right, Ground));
        Draw(left, top, "╔════╗", ConsoleColor.DarkYellow, Backdrop(left, top));
        Draw(left + 1, top - 1, MineText, ConsoleColor.Yellow, ConsoleColor.DarkRed);
    }

    private void Fill(int left, int top, int right, int bottom, char character, ConsoleColor foreground,
        ConsoleColor background)
    {
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
            Draw(x, y, character, foreground, background);
    }

    private void Draw(int x, int y, char character, ConsoleColor foreground, ConsoleColor background) =>
        canvas.Draw(x - _originX, y - _originY, character, foreground, background);

    private void Draw(int x, int y, string text, ConsoleColor foreground, ConsoleColor background) =>
        canvas.Draw(x - _originX, y - _originY, text, foreground, background);

    private void DrawOver(int x, int y, char character, ConsoleColor foreground) =>
        canvas.DrawOver(x - _originX, y - _originY, character, foreground);

    /// <summary>The background already drawn at a world cell (the sky or hills behind the buildings).</summary>
    private ConsoleColor Backdrop(int x, int y) => canvas.BackgroundAt(x - _originX, y - _originY);
}
