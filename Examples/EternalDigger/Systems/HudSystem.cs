using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The top two screen rows. Row 0: the title, gold, ladders, depth and the bag (ore counts and what they are worth).
/// Row 1: the keys, the rescue progress bar while <c>R</c> is held, or a blinking warning when the miner is
/// underground without ladders. The status text is rebuilt only when a shown value changes.
/// </summary>
public sealed class HudSystem(Canvas canvas) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = " ETERNAL DIGGER ";
    public const string KeyHelp =
        " ←→ walk/dig   ↓ dig   ↑ climb ladder   SPACE place ladder   E deliver/buy   hold R rescue   ESC quit";
    public const string RescueLabel = " RESCUE ";
    public const string RescueNote = " the bag is lost";
    public const string NoLaddersWarning = " NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ";

    private const char OreIcon = '◆';
    private const int RescueBarWidth = 24;
    private const float BlinkSpeed = 4f;
    private const ConsoleColor Bar = ConsoleColor.DarkBlue;

    private static readonly ConsoleColor[] OreColors = [ConsoleColor.DarkYellow, ConsoleColor.White, ConsoleColor.Yellow];

    private Query _miners;
    private string _status = string.Empty;
    private int _shownGold = -1, _shownLadders = -1, _shownDepth = -1, _shownCopper = -1, _shownSilver = -1,
        _shownGoldOre = -1;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            DrawStatus(miner);
            DrawHelp(miner);
            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawStatus(in Miner miner)
    {
        var depth = Math.Max(0, Mine.Depth(miner.Row));
        if (miner.Gold != _shownGold || miner.Ladders != _shownLadders || depth != _shownDepth ||
            miner.CopperOre != _shownCopper || miner.SilverOre != _shownSilver || miner.GoldOre != _shownGoldOre)
        {
            _shownGold = miner.Gold;
            _shownLadders = miner.Ladders;
            _shownDepth = depth;
            _shownCopper = miner.CopperOre;
            _shownSilver = miner.SilverOre;
            _shownGoldOre = miner.GoldOre;
            var bagValue = miner.CopperOre * Tiles.CopperValue + miner.SilverOre * Tiles.SilverValue +
                           miner.GoldOre * Tiles.GoldValue;
            _status = $"  GOLD {miner.Gold:N0}g   LADDERS {miner.Ladders}   DEPTH {depth}m   BAG " +
                      $"{OreIcon}{miner.CopperOre} {OreIcon}{miner.SilverOre} {OreIcon}{miner.GoldOre} = {bagValue:N0}g";
        }

        Fill(0, Bar);
        canvas.Draw(0, 0, Title, ConsoleColor.Black, ConsoleColor.Yellow);
        var ore = 0;
        for (var i = 0; i < _status.Length; i++)
        {
            var isIcon = _status[i] == OreIcon;
            var color = isIcon ? OreColors[Math.Min(ore++, OreColors.Length - 1)] : ConsoleColor.White;
            canvas.Draw(Title.Length + i, 0, _status[i], color, Bar);
        }
    }

    private void DrawHelp(in Miner miner)
    {
        Fill(1, ConsoleColor.Black);
        if (miner.RescueHold > 0f)
        {
            canvas.Draw(0, 1, RescueLabel, ConsoleColor.White, ConsoleColor.DarkRed);
            var filled = (int)(miner.RescueHold / MinerControlSystem.RescueDuration * RescueBarWidth);
            for (var i = 0; i < RescueBarWidth; i++)
                canvas.Draw(RescueLabel.Length + i, 1, i < filled ? '█' : '░', ConsoleColor.Red, ConsoleColor.Black);

            canvas.Draw(RescueLabel.Length + RescueBarWidth, 1, RescueNote, ConsoleColor.DarkGray, ConsoleColor.Black);
            return;
        }

        if (miner.Ladders == 0 && Mine.Depth(miner.Row) > 0)
        {
            var blink = MathF.Sin(_time * BlinkSpeed) > 0f;
            canvas.Draw(0, 1, NoLaddersWarning, blink ? ConsoleColor.White : ConsoleColor.Red,
                blink ? ConsoleColor.DarkRed : ConsoleColor.Black);
            return;
        }

        canvas.Draw(0, 1, KeyHelp, ConsoleColor.Gray, ConsoleColor.Black);
    }

    private void Fill(int y, ConsoleColor background)
    {
        for (var x = 0; x < canvas.Width; x++)
            canvas.Draw(x, y, ' ', ConsoleColor.White, background);
    }
}
