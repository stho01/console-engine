using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The inventory screen: <c>TAB</c> opens and closes a panel in the middle of the screen with everything the miner
/// carries (gold, ladders, oil and flasks, dynamite, the bag with its weight and worth), the upgrade tiers, the lift and
/// the rare finds ever dug up. While it is open the game is paused: every <paramref name="simulation"/> system is
/// disabled, so nothing moves and no oil burns; drawing goes on. Runs last in Presentation, over the HUD. The lines are
/// rebuilt only when a shown value changes.
/// </summary>
public sealed class InventorySystem(Canvas canvas, Elevator elevator, SystemGroup simulation) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = " INVENTORY ";
    public const string CloseHint = " TAB close ";
    public const string PausedText = "PAUSED";

    /// <summary>Inner width of the panel in cells.</summary>
    public const int PanelWidth = 58;

    private const ConsoleColor Frame = ConsoleColor.Yellow;
    private const ConsoleColor Fill = ConsoleColor.DarkBlue;
    private const char OreIcon = '◆';

    private static readonly ConsoleColor[] OreColors =
        [ConsoleColor.DarkYellow, ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Magenta];

    private readonly string[] _lines = new string[18];
    private Query _miners;
    private int _shownHash;
    private bool _built;

    //**********************************************************
    //** props
    //**********************************************************

    public bool IsOpen { get; private set; }

    /// <summary>The panel's text lines as last built, for tests. Empty strings are blank lines.</summary>
    public ReadOnlySpan<string> Lines => _lines;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.TAB).Pressed)
            Toggle();

        if (!IsOpen)
            return;

        foreach (var chunk in _miners.GetChunks())
        {
            Draw(chunk.GetSpan<Miner>()[0]);
            return;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Opens or closes the screen, pausing or resuming the simulation.</summary>
    public void Toggle()
    {
        IsOpen = !IsOpen;
        var systems = simulation.RunOrder;
        for (var i = 0; i < systems.Count; i++)
            systems[i].Enabled = !IsOpen;
    }

    /// <summary>Draws the panel for the miner, centred on the screen.</summary>
    public void Draw(in Miner miner)
    {
        Build(miner);
        var width = PanelWidth + 2;
        var height = _lines.Length + 2;
        var left = (canvas.Width - width) / 2;
        var top = Math.Max(Layout.HudHeight + 1, (canvas.Height - height) / 2);

        for (var x = 0; x < width; x++)
        {
            var corner = x == 0 || x == width - 1;
            canvas.Draw(left + x, top, corner ? x == 0 ? '╔' : '╗' : '═', Frame, Fill);
            canvas.Draw(left + x, top + height - 1, corner ? x == 0 ? '╚' : '╝' : '═', Frame, Fill);
        }

        canvas.Draw(left + 2, top, Title, ConsoleColor.Black, Frame);
        canvas.Draw(left + width - CloseHint.Length - 2, top, CloseHint, ConsoleColor.Black, Frame);
        canvas.Draw(left + width - PausedText.Length - 2, top + height - 1, PausedText, ConsoleColor.Gray, Fill);
        for (var i = 0; i < _lines.Length; i++)
            DrawLine(left, top + 1 + i, _lines[i], i);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawLine(int left, int y, string text, int index)
    {
        canvas.Draw(left, y, '║', Frame, Fill);
        canvas.Draw(left + PanelWidth + 1, y, '║', Frame, Fill);
        var heading = text.Length > 0 && text[0] != ' ';
        var ore = 0;
        for (var i = 0; i < PanelWidth; i++)
        {
            var character = i < text.Length ? text[i] : ' ';
            var color = character switch
            {
                OreIcon => OreColors[Math.Min(ore++, OreColors.Length - 1)],
                '§' or '¥' or '¡' or 'δ' => ConsoleColor.Yellow,
                '●' => ConsoleColor.Yellow,
                '○' => ConsoleColor.DarkGray,
                _ => heading ? ConsoleColor.Yellow : index == 0 ? ConsoleColor.Gray : ConsoleColor.White
            };
            canvas.Draw(left + 1 + i, y, character, color, Fill);
        }
    }

    /// <summary>Rebuilds the lines when anything they show has changed. Allocates only then.</summary>
    private void Build(in Miner miner)
    {
        var hash = Signature(miner);
        if (_built && hash == _shownHash)
            return;

        _built = true;
        _shownHash = hash;
        var weight = Shop.BagWeight(miner);
        var capacity = Shop.BagCapacity(miner);
        var dynamite = miner.HasBlastingKit ? $"¡{miner.Dynamite}" : "no blasting kit";
        var lift = elevator.IsBuilt ? $"{elevator.Depth}m" : "not built";
        _lines[0] = "";
        _lines[1] = $" GOLD      {miner.Gold,8:N0}g        LADDERS   {miner.Ladders}";
        _lines[2] = $" OIL       {LampOil.Percent(miner.Oil),8}%        FLASKS    δ{miner.OilFlasks}/{LampOil.MaxFlasks}";
        _lines[3] = $" DYNAMITE  {dynamite,-17} LIFT      {lift}";
        _lines[4] = "";
        _lines[5] = $"BAG{(Shop.IsHeavy(miner) ? "  HEAVY" : ""),-30}weight {weight}/{capacity}";
        _lines[6] = BagLine(OreIcon, "copper", miner.CopperOre, Tiles.CopperValue);
        _lines[7] = BagLine(OreIcon, "silver", miner.SilverOre, Tiles.SilverValue);
        _lines[8] = BagLine(OreIcon, "gold", miner.GoldOre, Tiles.GoldValue);
        _lines[9] = BagLine(OreIcon, "gem", miner.GemOre, Tiles.GemValue);
        _lines[10] = BagLine('§', "fossil", miner.Fossils, Tiles.FossilValue);
        _lines[11] = BagLine('¥', "relic", miner.Relics, Tiles.RelicValue);
        _lines[12] = $"   {"",-30}worth  {Shop.BagValue(miner):N0}g";
        _lines[13] = "";
        _lines[14] = "UPGRADES";
        _lines[15] = $"   {UpgradeText(UpgradeLine.Shovel, miner)}  {UpgradeText(UpgradeLine.Pickaxe, miner)}  " +
                     UpgradeText(UpgradeLine.Lamp, miner);
        _lines[16] = $"   {UpgradeText(UpgradeLine.Bag, miner)}  {UpgradeText(UpgradeLine.Detector, miner)}";
        _lines[17] = $"FOUND FOR GOOD   §{miner.FossilsFound} fossils   ¥{miner.RelicsFound} relics";
    }

    private static string BagLine(char icon, string name, int count, int value) =>
        $"   {icon} {name,-8}{count,4} × {value,4}g = {count * value,7:N0}g";

    private static string UpgradeText(UpgradeLine line, in Miner miner)
    {
        var tier = Upgrades.Tier(line, miner);
        return $"{Upgrades.Name(line),-9}{new string('●', tier)}{new string('○', Upgrades.MaxTier - tier)}";
    }

    /// <summary>A hash of every value the panel shows; computed every frame while open, without allocating.</summary>
    private int Signature(in Miner miner) => HashCode.Combine(
        HashCode.Combine(miner.Gold, miner.Ladders, LampOil.Percent(miner.Oil), miner.OilFlasks, miner.Dynamite,
            miner.HasBlastingKit, elevator.Depth),
        HashCode.Combine(miner.CopperOre, miner.SilverOre, miner.GoldOre, miner.GemOre, miner.Fossils, miner.Relics,
            miner.BagTier),
        HashCode.Combine(miner.ShovelTier, miner.PickaxeTier, miner.LampTier, miner.DetectorTier, miner.FossilsFound,
            miner.RelicsFound));
}
