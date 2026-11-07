using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// A small framed prompt above the miner's head while they stand at the house door (<c>DELIVER [E]</c>), the lift
/// sign (<c>BUILD LIFT +10m [E] 200g</c>) or a lift landing (call, get in, ride). At the shop counter it is a
/// three-line panel: the selected item with the Up/Down hint, what the miner has of it, and its price. It bobs gently.
/// A price turns red when the miner can't pay.
/// </summary>
public sealed class PromptRenderSystem(
    Canvas canvas, Camera camera, Mine mine, Elevator elevator, ShopCounter counter) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string DeliverText = " DELIVER [E] ";
    public const string CallLiftText = " CALL LIFT [E] ";
    public const string BoardLiftText = " GET IN [E] ";
    public const string RideText = " ↑↓ RIDE   ←→ GET OUT ";
    public const string LiftBlockedText = " LIFT BLOCKED BY OBSIDIAN ";
    public const string KitUnlocksText = "UNLOCKS DYNAMITE";
    public const string SoldOutText = "SOLD OUT";
    public const string MaxTierText = "MAX";
    public const string FullText = "FULL";
    public const char TierOn = '●';
    public const char TierOff = '○';

    private const string KeyHint = "[E]";
    private const float BobSpeed = 3f;

    private Query _miners;
    private float _time;
    private string _buildLiftText = string.Empty;
    private int _buildLiftPrice = -1;

    // The counter panel, rebuilt only when what it shows changes.
    private string _panelName = string.Empty, _panelOwned = string.Empty, _panelPrice = string.Empty;
    private ShopItem _shownItem;
    private int _shownPrice = int.MinValue, _shownOwned = int.MinValue;
    private bool _shownKit;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            if (miner.IsMoving || elevator.IsCarMoving && miner.IsRiding)
                return;

            var position = chunk.GetSpan<Position>()[0].Value;
            if (miner.IsRiding)
                Draw(RideText, position.X, position.Y, ConsoleColor.White);
            else if (Layout.IsAtDeliverSpot(miner.Column, miner.Row))
                Draw(DeliverText, position.X, position.Y, ConsoleColor.White);
            else if (Layout.IsAtShop(miner.Column, miner.Row))
                DrawCounterPanel(miner, position.X, position.Y);
            else if (Layout.IsAtLiftSign(miner.Column, miner.Row) && !elevator.CanBuildNext(mine))
                Draw(LiftBlockedText, position.X, position.Y, ConsoleColor.Red);
            else if (Layout.IsAtLiftSign(miner.Column, miner.Row))
                Draw(BuildLiftText(), position.X, position.Y, PriceColor(miner.Gold, elevator.NextSegmentPrice));
            else if (miner.Column == Elevator.Column && elevator.IsLanding(miner.Row))
                Draw(elevator.IsCarAt(miner.Row) ? BoardLiftText : CallLiftText, position.X, position.Y,
                    ConsoleColor.White);

            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static ConsoleColor PriceColor(int gold, int price) => gold >= price ? ConsoleColor.White : ConsoleColor.Red;

    /// <summary>The lift sign's text with the next segment's price. Rebuilt only when the price changes.</summary>
    private string BuildLiftText()
    {
        if (elevator.NextSegmentPrice != _buildLiftPrice)
        {
            _buildLiftPrice = elevator.NextSegmentPrice;
            _buildLiftText = $" BUILD LIFT +{Elevator.SegmentMetres}m [E] {_buildLiftPrice}g ";
        }

        return _buildLiftText;
    }

    /// <summary>The counter's panel for the selected item. Its strings are rebuilt only when a value changes.</summary>
    private void DrawCounterPanel(in Miner miner, float minerX, float minerY)
    {
        var item = counter.Selected;
        var price = Shop.Price(item, miner);
        var owned = Shop.Owned(item, miner);
        if (item != _shownItem || price != _shownPrice || owned != _shownOwned || miner.HasBlastingKit != _shownKit)
        {
            _shownItem = item;
            _shownPrice = price;
            _shownOwned = owned;
            _shownKit = miner.HasBlastingKit;
            _panelName = $" ↑ {ShopCounter.Name(item, miner)} ↓ ";
            var upgrade = ShopCounter.IsUpgrade(item);
            _panelOwned = upgrade ? Pips(owned)
                : item == ShopItem.LampOil ? $"OIL {owned}%"
                : owned >= 0 ? $"HAVE {owned}" : KitUnlocksText;
            _panelPrice = price != Shop.SoldOut ? $"{price}g {KeyHint}"
                : upgrade ? MaxTierText
                : item == ShopItem.LampOil ? FullText : SoldOutText;
        }

        var priceColor = price == Shop.SoldOut ? ConsoleColor.DarkGray : PriceColor(miner.Gold, price);
        DrawBox(minerX, minerY, _panelName, ConsoleColor.White, _panelOwned, ConsoleColor.Gray, _panelPrice,
            priceColor);
    }

    /// <summary>Tier pips like <c>●●○</c>.</summary>
    private static string Pips(int tier) =>
        new string(TierOn, tier) + new string(TierOff, Math.Max(0, Upgrades.MaxTier - tier));

    /// <summary>A one-line box with a pointer, its bottom rows above the miner's head.</summary>
    private void Draw(string text, float minerX, float minerY, ConsoleColor textColor) =>
        DrawBox(minerX, minerY, text, textColor, null, textColor, null, textColor);

    /// <summary>
    /// A framed box of one to three centred lines (null lines are left out) with a <c>▼</c> pointer just above the
    /// miner's head. <c>[E]</c> is drawn in the frame colour.
    /// </summary>
    private void DrawBox(float minerX, float minerY, string first, ConsoleColor firstColor, string second,
        ConsoleColor secondColor, string third, ConsoleColor thirdColor)
    {
        var lines = 1 + (second != null ? 1 : 0) + (third != null ? 1 : 0);
        var inner = Math.Max(first.Length, Math.Max(second?.Length ?? 0, third?.Length ?? 0));
        var width = inner + 2;
        var centerX = (int)MathF.Floor(minerX - camera.Position.X) + Layout.TileWidth / 2;
        var x = Math.Clamp(centerX - width / 2, 0, canvas.Width - width);
        var bob = MathF.Sin(_time * BobSpeed) > .6f ? 1 : 0;
        var bottom = (int)MathF.Floor(minerY - camera.Position.Y) - 2 - bob;
        var top = bottom - lines - 1;

        const ConsoleColor frame = ConsoleColor.Yellow;
        const ConsoleColor fill = ConsoleColor.DarkBlue;
        for (var i = 0; i < width; i++)
        {
            var corner = i == 0 || i == width - 1;
            canvas.Draw(x + i, top, corner ? i == 0 ? '┌' : '┐' : '─', frame, fill);
            canvas.Draw(x + i, bottom, corner ? i == 0 ? '└' : '┘' : '─', frame, fill);
        }

        DrawLine(x, top + 1, inner, first, firstColor);
        if (second != null)
            DrawLine(x, top + 2, inner, second, secondColor);
        if (third != null)
            DrawLine(x, top + lines, inner, third, thirdColor);

        canvas.DrawOver(centerX, bottom + 1, '▼', frame);
    }

    /// <summary>One line of a box: the side frames and the text centred on the fill.</summary>
    private void DrawLine(int x, int y, int inner, string text, ConsoleColor color)
    {
        const ConsoleColor frame = ConsoleColor.Yellow;
        const ConsoleColor fill = ConsoleColor.DarkBlue;
        canvas.Draw(x, y, '│', frame, fill);
        canvas.Draw(x + inner + 1, y, '│', frame, fill);
        var start = (inner - text.Length) / 2;
        var keyStart = text.IndexOf(KeyHint, StringComparison.Ordinal);
        for (var i = 0; i < inner; i++)
        {
            var index = i - start;
            var character = index >= 0 && index < text.Length ? text[index] : ' ';
            var isKey = keyStart >= 0 && index >= keyStart && index < keyStart + KeyHint.Length;
            canvas.Draw(x + 1 + i, y, character, isKey ? frame : color, fill);
        }
    }
}
