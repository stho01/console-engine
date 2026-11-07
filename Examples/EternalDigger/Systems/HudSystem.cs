using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Saving;

namespace EternalDigger.Systems;

/// <summary>
/// The top two screen rows. Row 0: the title, gold, ladders, the lift's depth (once built), depth and the bag (ore
/// counts and what they are worth). Row 1: the keys, the rescue progress bar while <c>R</c> is held, or a blinking
/// warning when the miner is underground without ladders. After each save, a fading <c>SAVED</c> (or a red
/// <c>SAVE FAILED</c>) at the right end of row 1. The status text is rebuilt only when a shown value changes.
/// </summary>
public sealed class HudSystem(Canvas canvas, GameSaver saver, Elevator elevator) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = " ETERNAL DIGGER ";
    public const string KeyHelp =
        " ←→ walk/dig   ↓ dig   ↑ climb ladder   SPACE place ladder   E deliver/buy   hold R rescue   ESC save & quit";
    public const string RescueLabel = " RESCUE ";
    public const string RescueNote = " the bag is lost";
    public const string NoLaddersWarning = " NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ";
    public const string SavedText = " SAVED ";
    public const string SaveFailedText = " SAVE FAILED";

    /// <summary>Seconds the save hint shows: the first half in grey, the second in dark grey.</summary>
    public const float SavedHintSeconds = 1.5f;

    private const char OreIcon = '◆';
    private const int RescueBarWidth = 24;
    private const float BlinkSpeed = 4f;
    private const ConsoleColor Bar = ConsoleColor.DarkBlue;

    private static readonly ConsoleColor[] OreColors =
        [ConsoleColor.DarkYellow, ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Magenta];

    private Query _miners;
    private string _status = string.Empty;
    private int _shownGold = -1, _shownLadders = -1, _shownDepth = -1, _shownCopper = -1, _shownSilver = -1,
        _shownGoldOre = -1, _shownGems = -1, _shownLift = -1;
    private float _time;
    private int _shownSaves;
    private float _savedHint;

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
            DrawSavedHint(deltaSeconds);
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
            miner.CopperOre != _shownCopper || miner.SilverOre != _shownSilver || miner.GoldOre != _shownGoldOre ||
            miner.GemOre != _shownGems || elevator.Depth != _shownLift)
        {
            _shownGems = miner.GemOre;
            _shownLift = elevator.Depth;
            _shownGold = miner.Gold;
            _shownLadders = miner.Ladders;
            _shownDepth = depth;
            _shownCopper = miner.CopperOre;
            _shownSilver = miner.SilverOre;
            _shownGoldOre = miner.GoldOre;
            var bagValue = Shop.BagValue(miner);
            var lift = elevator.IsBuilt ? $"   LIFT {elevator.Depth}m" : string.Empty;
            _status = $"  GOLD {miner.Gold:N0}g   LADDERS {miner.Ladders}{lift}   DEPTH {depth}m   BAG " +
                      $"{OreIcon}{miner.CopperOre} {OreIcon}{miner.SilverOre} {OreIcon}{miner.GoldOre} " +
                      $"{OreIcon}{miner.GemOre} = {bagValue:N0}g";
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

    private void DrawSavedHint(float deltaSeconds)
    {
        if (saver.Saves != _shownSaves)
        {
            _shownSaves = saver.Saves;
            _savedHint = SavedHintSeconds;
        }

        if (_savedHint <= 0f)
            return;

        _savedHint -= deltaSeconds;
        var text = saver.LastSaveFailed ? SaveFailedText : SavedText;
        var color = saver.LastSaveFailed ? ConsoleColor.Red
            : _savedHint > SavedHintSeconds / 2f ? ConsoleColor.Gray : ConsoleColor.DarkGray;
        canvas.Draw(canvas.Width - text.Length, 1, text, color, ConsoleColor.Black);
    }

    private void Fill(int y, ConsoleColor background)
    {
        for (var x = 0; x < canvas.Width; x++)
            canvas.Draw(x, y, ' ', ConsoleColor.White, background);
    }
}
