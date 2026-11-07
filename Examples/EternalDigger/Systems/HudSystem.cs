using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Saving;

namespace EternalDigger.Systems;

/// <summary>
/// The top two screen rows. Row 0: the title, gold, ladders, the lift's depth (once built), dynamite (once the kit is
/// bought), the oil gauge, depth and the bag (ore counts and what they are worth). Row 1: the keys, the rescue progress
/// bar while <c>R</c> is held, the fuse bar while <c>E</c> is held to plant dynamite, or a blinking warning when the
/// miner is underground without ladders. After each save, a fading <c>SAVED</c> (or a red <c>SAVE FAILED</c>) at the
/// right end of row 1. The status text is rebuilt only when a shown value changes.
/// </summary>
public sealed class HudSystem(Canvas canvas, GameSaver saver, Elevator elevator) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = " ETERNAL DIGGER ";
    public const string KeyHelp =
        " ←→ walk/dig   ↓ dig   ↑ climb   SPACE ladder   E use/buy   hold E dynamite   hold R rescue   ESC save & quit";
    public const string RescueLabel = " RESCUE ";
    public const string RescueNote = " the bag is lost";
    public const string NoLaddersWarning = " NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ";
    public const string PlantLabel = " LIGHTING FUSE ";
    public const string SavedText = " SAVED ";
    public const string SaveFailedText = " SAVE FAILED";

    /// <summary>Seconds the save hint shows: the first half in grey, the second in dark grey.</summary>
    public const float SavedHintSeconds = 1.5f;

    private const char OreIcon = '◆';
    private const char LadderIcon = '╟';
    private const char LadderIconEnd = '╢';
    private const char DynamiteIcon = '¡';
    private const char OilFull = '▮';
    private const char OilEmpty = '▯';
    private const char FindIcon = '✦';

    /// <summary>Cells in the oil gauge.</summary>
    public const int OilGaugeCells = 8;
    private const int RescueBarWidth = 24;
    private const float BlinkSpeed = 4f;
    private const ConsoleColor Bar = ConsoleColor.DarkBlue;

    private static readonly ConsoleColor[] OreColors =
        [ConsoleColor.DarkYellow, ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Magenta];

    private Query _miners;
    private string _status = string.Empty;
    private int _shownGold = -1, _shownLadders = -1, _shownDepth = -1, _shownCopper = -1, _shownSilver = -1,
        _shownGoldOre = -1, _shownGems = -1, _shownLift = -1, _shownDynamite = -1, _shownOilCells = -1, _shownFinds = -1;
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
            miner.GemOre != _shownGems || elevator.Depth != _shownLift || DynamiteShown(miner) != _shownDynamite ||
            OilCells(miner.Oil) != _shownOilCells || miner.RareFinds != _shownFinds)
        {
            _shownFinds = miner.RareFinds;
            _shownOilCells = OilCells(miner.Oil);
            _shownDynamite = DynamiteShown(miner);
            _shownGems = miner.GemOre;
            _shownLift = elevator.Depth;
            _shownGold = miner.Gold;
            _shownLadders = miner.Ladders;
            _shownDepth = depth;
            _shownCopper = miner.CopperOre;
            _shownSilver = miner.SilverOre;
            _shownGoldOre = miner.GoldOre;
            var bagValue = Shop.BagValue(miner);
            var lift = elevator.IsBuilt ? $"  LIFT {elevator.Depth}m" : string.Empty;
            var dynamite = miner.HasBlastingKit ? $"  {DynamiteIcon}{miner.Dynamite}" : string.Empty;
            var gauge = new string(OilFull, _shownOilCells) + new string(OilEmpty, OilGaugeCells - _shownOilCells);
            _status = $"  GOLD {miner.Gold:N0}g  {LadderIcon}{LadderIconEnd}{miner.Ladders}{lift}{dynamite}  OIL {gauge}" +
                      $"  DEPTH {depth}m  BAG {OreIcon}{miner.CopperOre} {OreIcon}{miner.SilverOre} " +
                      $"{OreIcon}{miner.GoldOre} {OreIcon}{miner.GemOre} {FindIcon}{miner.RareFinds} = {bagValue:N0}g";
        }

        Fill(0, Bar);
        canvas.Draw(0, 0, Title, ConsoleColor.Black, ConsoleColor.Yellow);
        var ore = 0;
        var oilColor = OilColor(miner.Oil);
        for (var i = 0; i < _status.Length; i++)
        {
            var character = _status[i];
            var color = character switch
            {
                OreIcon => OreColors[Math.Min(ore++, OreColors.Length - 1)],
                LadderIcon or LadderIconEnd => ConsoleColor.Yellow,
                DynamiteIcon => ConsoleColor.Red,
                FindIcon => ConsoleColor.Yellow,
                OilFull or OilEmpty => oilColor,
                _ => ConsoleColor.White
            };
            canvas.Draw(Title.Length + i, 0, character, color, Bar);
        }
    }

    private void DrawHelp(in Miner miner)
    {
        Fill(1, ConsoleColor.Black);
        if (miner.RescueHold > 0f)
        {
            DrawBar(RescueLabel, miner.RescueHold / MinerControlSystem.RescueDuration, ConsoleColor.Red,
                ConsoleColor.DarkRed);
            canvas.Draw(RescueLabel.Length + RescueBarWidth, 1, RescueNote, ConsoleColor.DarkGray, ConsoleColor.Black);
            return;
        }

        if (miner.PlantHold > 0f)
        {
            DrawBar(PlantLabel, miner.PlantHold / MinerControlSystem.PlantDuration, ConsoleColor.Yellow,
                ConsoleColor.DarkRed);
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

    /// <summary>A label and a progress bar on row 1.</summary>
    private void DrawBar(string label, float progress, ConsoleColor barColor, ConsoleColor labelBackground)
    {
        canvas.Draw(0, 1, label, ConsoleColor.White, labelBackground);
        var filled = (int)(progress * RescueBarWidth);
        for (var i = 0; i < RescueBarWidth; i++)
            canvas.Draw(label.Length + i, 1, i < filled ? '█' : '░', barColor, ConsoleColor.Black);
    }

    /// <summary>Filled cells of the oil gauge: any oil left shows at least one.</summary>
    public static int OilCells(float oil) =>
        oil <= 0f ? 0 : Math.Clamp((int)MathF.Ceiling(oil / LampOil.TankSize * OilGaugeCells), 1, OilGaugeCells);

    /// <summary>White when there is plenty, yellow from half, blinking red when low.</summary>
    private ConsoleColor OilColor(float oil)
    {
        if (LampOil.IsLow(oil))
            return MathF.Sin(_time * BlinkSpeed) > 0f ? ConsoleColor.Red : ConsoleColor.DarkRed;

        return oil < LampOil.TankSize / 2f ? ConsoleColor.Yellow : ConsoleColor.White;
    }

    /// <summary>The dynamite count the HUD shows, -1 before the kit is bought.</summary>
    private static int DynamiteShown(in Miner miner) => miner.HasBlastingKit ? miner.Dynamite : -1;

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
