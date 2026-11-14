using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The surface buildings, drawn from rectangles in world cells: the house with its "EternalDigger" sign, door and
/// chimney smoke, the ladder shop with a striped awning, and the wooden frame over the mine shaft. Decoration only;
/// the miner walks in front of them. They show the miner's <see cref="Progress"/>: a flag, lit windows and a second
/// floor at gold milestones, a trophy case beside the door with a glyph per rare find, a <c>RECORD</c> plank over the
/// mine entrance, and the tool shop stall with its toolsmith (boarded up until the miner has been below the wall).
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
    private const string ShopText = "SHOP";
    private const string MineText = "MINE";
    private const string LiftText = " LIFT ";
    private const string BlastingText = "BLASTING";
    private const string BlastingDisplay = " ¡ ¡ ¡ ";
    private const int ShedLeft = Layout.BlastingFirstColumn * Layout.TileWidth;
    private const int ShedRight = (Layout.BlastingLastColumn + 1) * Layout.TileWidth - 1;
    private const int LiftSignLeft = Layout.LiftSignFirstColumn * Layout.TileWidth + 1;

    /// <summary>Cells the second floor adds under the roof (house level 3).</summary>
    public const int UpperFloorHeight = 4;

    /// <summary>The trophy case beside the door: two shelves of this many finds each.</summary>
    public const int ShelfWidth = 5;
    public const int TrophySlots = ShelfWidth * 2;

    public const string ToolsText = "TOOLS";
    public const string ClosedText = "CLOSED";
    public const char FossilTrophy = '§';
    public const char RelicTrophy = '¥';

    /// <summary>
    /// The toolsmith's bottom row, 4 x 2 like the miner with a face above: the arms swap every
    /// <see cref="IdleFrameSeconds"/>.
    /// </summary>
    public static readonly string[] ToolsmithFrames = ["/█\\", "\\█/"];

    public const float IdleFrameSeconds = .6f;

    private const int ShelfLeft = DoorLeft - ShelfWidth;
    private const int StallLeft = Layout.ToolShopColumn * Layout.TileWidth;
    private const int StallRight = (Layout.ToolsmithColumn + 1) * Layout.TileWidth - 1;
    private const int ToolsmithLeft = Layout.ToolsmithColumn * Layout.TileWidth;

    private const float SmokeRise = 1.5f;
    private const int SmokePuffs = 4;
    private const int SmokeHeight = 6;

    private float _time;
    private int _originX;
    private int _originY;

    private Query _miners;
    private int _houseLevel;
    private int _fossils;
    private int _relics;
    private bool _toolShopOpen;
    private int _shownRecord;
    private string _recordText = string.Empty;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        _originX = (int)MathF.Floor(camera.Position.X);
        _originY = (int)MathF.Floor(camera.Position.Y);
        if (WallTop - UpperFloorHeight - RoofHeight - 8 - _originY >= canvas.Height || Ground - _originY < 0)
            return;

        ReadProgress();
        DrawHouse();
        DrawTrophyCase();
        DrawShop();
        DrawToolShop();
        DrawMineEntrance();
        DrawRecordSign();
        DrawLiftSign();
        DrawBlastingShed();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The miner's progress this frame, read once.</summary>
    private void ReadProgress()
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            _houseLevel = Progress.HouseLevel(miner.GoldEarned);
            _fossils = miner.FossilsFound;
            _relics = miner.RelicsFound;
            _toolShopOpen = Progress.IsToolShopOpen(miner);
            if (miner.DepthRecord != _shownRecord)
            {
                _shownRecord = miner.DepthRecord;
                _recordText = $" RECORD {_shownRecord}m ";
            }

            return;
        }
    }

    private void DrawHouse()
    {
        // A second floor lifts the roof (and chimney) by its height.
        var roofBase = _houseLevel >= 3 ? WallTop - UpperFloorHeight : WallTop;

        // Chimney first, so the roof covers its foot. Smoke rises from it.
        Fill(ChimneyX, roofBase - RoofHeight - 1, ChimneyX + 1, roofBase - 2, '▓', ConsoleColor.Gray, ConsoleColor.DarkGray);
        for (var i = 0; i < SmokePuffs; i++)
        {
            var rise = (_time * SmokeRise + i * SmokeHeight / (float)SmokePuffs) % SmokeHeight;
            var x = ChimneyX + (int)MathF.Round(MathF.Sin(_time + i * 1.7f) * rise * .4f + rise * .3f);
            var y = roofBase - RoofHeight - 2 - (int)rise;
            DrawOver(x, y, rise < SmokeHeight / 2f ? '○' : '°', ConsoleColor.Gray);
        }

        // Roof: a wedge of shingles, one cell narrower per row on both sides.
        for (var row = 0; row < RoofHeight; row++)
        {
            var y = roofBase - 1 - row;
            var inset = row * 2;
            for (var x = HouseLeft + inset; x <= HouseRight - inset; x++)
            {
                var edge = x == HouseLeft + inset || x == HouseRight - inset;
                Draw(x, y, edge ? '▄' : ((x + row) & 1) == 0 ? '▀' : '▄',
                    edge ? ConsoleColor.DarkRed : ConsoleColor.Red, edge ? Backdrop(x, y) : ConsoleColor.DarkRed);
            }
        }

        if (_houseLevel >= 1)
            DrawFlag(roofBase - RoofHeight);

        // Plank walls (from the upper floor's top with a second floor).
        for (var y = roofBase; y <= Ground; y++)
            Fill(HouseLeft + 1, y, HouseRight, y, (y & 1) == 0 ? '─' : ' ', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);

        // The sign on the wall, under the eaves.
        var signLeft = (HouseLeft + HouseRight + 1 - SignBoard.Length) / 2;
        Draw(signLeft, WallTop, SignBoard, ConsoleColor.Yellow, ConsoleColor.DarkRed);
        Draw(signLeft, WallTop + 1, '▀', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);
        Draw(signLeft + SignBoard.Length - 1, WallTop + 1, '▀', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);

        // Windows and the door (over the deliver spot). The upper floor gets two more.
        if (_houseLevel >= 3)
        {
            DrawWindow(HouseLeft + 4, roofBase + 1);
            DrawWindow(HouseRight - 7, roofBase + 1);
        }

        DrawWindow(HouseLeft + 2, WallTop + 3);
        DrawWindow(HouseRight - 5, WallTop + 3);
        Fill(DoorLeft, Ground - 4, DoorLeft + 3, Ground, '║', ConsoleColor.DarkYellow, ConsoleColor.DarkRed);
        Draw(DoorLeft, Ground - 4, "▄▄▄▄", ConsoleColor.DarkRed, ConsoleColor.DarkYellow);
        Draw(DoorLeft + 3, Ground - 2, '•', ConsoleColor.Yellow, ConsoleColor.DarkRed);
    }

    /// <summary>Windows glow yellow from house level 2, as if a lamp burns inside.</summary>
    private void DrawWindow(int x, int y)
    {
        var glass = _houseLevel >= 2 ? ConsoleColor.Yellow : ConsoleColor.Cyan;
        Draw(x, y, "┌┬┐", ConsoleColor.DarkRed, glass);
        Draw(x, y + 1, "└┴┘", ConsoleColor.DarkRed, glass);
    }

    /// <summary>A pole on the roof's peak with a flag that flutters (house level 1).</summary>
    private void DrawFlag(int peakY)
    {
        var poleX = (HouseLeft + HouseRight) / 2;
        Fill(poleX, peakY - 3, poleX, peakY - 1, '│', ConsoleColor.Gray, Backdrop(poleX, peakY - 1));
        var flutter = ((int)(_time * 3f) & 1) == 0;
        DrawOver(poleX + 1, peakY - 3, flutter ? '▀' : '▄', ConsoleColor.Yellow);
        DrawOver(poleX + 2, peakY - 3, flutter ? '▄' : '▀', ConsoleColor.Yellow);
    }

    /// <summary>
    /// The trophy case left of the door: two shelves, relics (<c>¥</c>) first, then fossils (<c>§</c>), one glyph per find
    /// ever dug up. With more than <see cref="TrophySlots"/> finds the last slot shows <c>+</c>.
    /// </summary>
    private void DrawTrophyCase()
    {
        var total = _relics + _fossils;
        for (var slot = 0; slot < TrophySlots; slot++)
        {
            var x = ShelfLeft + slot % ShelfWidth;
            var y = Ground - 4 + slot / ShelfWidth * 2;
            var character = slot >= total ? ' '
                : slot == TrophySlots - 1 && total > TrophySlots ? '+'
                : slot < _relics ? RelicTrophy : FossilTrophy;
            Draw(x, y, character, slot < _relics ? ConsoleColor.Yellow : ConsoleColor.White, ConsoleColor.Black);
            Draw(x, y + 1, '▀', ConsoleColor.DarkYellow, ConsoleColor.DarkRed);
        }
    }

    /// <summary>A plank with the depth record over the mine entrance, once the miner has been underground.</summary>
    private void DrawRecordSign()
    {
        if (_shownRecord <= 0)
            return;

        var center = Mine.ShaftColumn * Layout.TileWidth + Layout.TileWidth / 2;
        Draw(center - _recordText.Length / 2, Ground - 6, _recordText, ConsoleColor.White, ConsoleColor.DarkYellow);
    }

    /// <summary>
    /// The tool shop: a stall with a green-striped awning and a <c>TOOLS</c> board. Open, the toolsmith stands behind a
    /// crate counter, idling between two frames; closed, the front is boarded up.
    /// </summary>
    private void DrawToolShop()
    {
        for (var x = StallLeft; x <= StallRight; x++)
        {
            var stripe = ((x - StallLeft) / 2 & 1) == 0 ? ConsoleColor.DarkGreen : ConsoleColor.White;
            Draw(x, Ground - 5, '▄', stripe, Backdrop(x, Ground - 5));
            Draw(x, Ground - 4, '▀', stripe, Backdrop(x, Ground - 4));
        }

        Draw((StallLeft + StallRight + 1 - ToolsText.Length) / 2, Ground - 6, ToolsText, ConsoleColor.White,
            ConsoleColor.DarkGreen);
        Fill(StallLeft, Ground - 3, StallLeft, Ground, '│', ConsoleColor.DarkYellow, Backdrop(StallLeft, Ground));
        Fill(StallRight, Ground - 3, StallRight, Ground, '│', ConsoleColor.DarkYellow, Backdrop(StallRight, Ground));
        if (!_toolShopOpen)
        {
            Fill(StallLeft + 1, Ground - 3, StallRight - 1, Ground, '▒', ConsoleColor.DarkYellow, ConsoleColor.Black);
            Draw((StallLeft + StallRight + 1 - ClosedText.Length) / 2, Ground - 2, ClosedText, ConsoleColor.White,
                ConsoleColor.DarkGray);
            return;
        }

        var frame = ToolsmithFrames[(int)(_time / IdleFrameSeconds) & 1];
        Draw(ToolsmithLeft + 1, Ground - 1, '☺', ConsoleColor.White, Backdrop(ToolsmithLeft + 1, Ground - 1));
        for (var i = 0; i < frame.Length; i++)
            Draw(ToolsmithLeft + i, Ground, frame[i], frame[i] == '█' ? ConsoleColor.DarkGreen : ConsoleColor.Green,
                Backdrop(ToolsmithLeft + i, Ground));
        Fill(StallLeft + 1, Ground, ToolsmithLeft - 1, Ground, '▄', ConsoleColor.DarkYellow,
            Backdrop(StallLeft + 1, Ground));
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

    /// <summary>A plank on two posts beside the mine entrance, where lift segments are bought.</summary>
    private void DrawLiftSign()
    {
        Draw(LiftSignLeft, Ground - 3, LiftText, ConsoleColor.White, ConsoleColor.DarkYellow);
        Fill(LiftSignLeft + 1, Ground - 2, LiftSignLeft + 1, Ground, '│', ConsoleColor.DarkYellow,
            Backdrop(LiftSignLeft + 1, Ground));
        Fill(LiftSignLeft + LiftText.Length - 2, Ground - 2, LiftSignLeft + LiftText.Length - 2, Ground, '│',
            ConsoleColor.DarkYellow, Backdrop(LiftSignLeft + LiftText.Length - 2, Ground));
    }

    /// <summary>A small red-roofed shed with a BLASTING board and sticks in the window, where dynamite is sold.</summary>
    private void DrawBlastingShed()
    {
        Fill(ShedLeft, Ground - 6, ShedRight, Ground - 6, '▄', ConsoleColor.DarkRed, Backdrop(ShedLeft, Ground - 6));
        Fill(ShedLeft, Ground - 5, ShedRight, Ground - 5, '▀', ConsoleColor.Red, ConsoleColor.DarkRed);
        Fill(ShedLeft + 1, Ground - 4, ShedRight - 1, Ground, '▒', ConsoleColor.DarkYellow, ConsoleColor.Black);
        Fill(ShedLeft, Ground - 4, ShedLeft, Ground, '│', ConsoleColor.DarkYellow, ConsoleColor.Black);
        Fill(ShedRight, Ground - 4, ShedRight, Ground, '│', ConsoleColor.DarkYellow, ConsoleColor.Black);
        var boardX = (ShedLeft + ShedRight + 1 - BlastingText.Length) / 2;
        Draw(boardX, Ground - 4, BlastingText, ConsoleColor.Yellow, ConsoleColor.DarkRed);
        var windowX = (ShedLeft + ShedRight + 1 - BlastingDisplay.Length) / 2;
        Draw(windowX, Ground - 2, BlastingDisplay, ConsoleColor.Red, ConsoleColor.Black);
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
