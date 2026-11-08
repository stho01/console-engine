using EternalDigger.Components;

namespace EternalDigger;

/// <summary>What the shop counter sells. The blasting slot sells the kit until it is owned, then sticks.</summary>
public enum ShopItem : byte
{
    Ladder,
    LampOil,
    Shovel,
    Lamp,
    Bag,
    Detector,
    Blasting,
    /// <summary>A flask of lamp oil carried in the pack (<see cref="LampOil.TryPour"/>).</summary>
    OilFlask
}

/// <summary>
/// The shop counter: which item is selected. Up/Down at the counter move the selection (it wraps), <c>E</c> buys the
/// selected item through <see cref="Shop.TryBuy"/>. Ladders are selected first, so <c>E</c> alone still buys a ladder.
/// The selection isn't saved.
/// </summary>
public sealed class ShopCounter
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The counter's items, top to bottom.</summary>
    public static readonly ShopItem[] Items =
    [
        ShopItem.Ladder, ShopItem.LampOil, ShopItem.OilFlask, ShopItem.Shovel, ShopItem.Lamp, ShopItem.Bag,
        ShopItem.Detector, ShopItem.Blasting
    ];

    private int _selected;

    //**********************************************************
    //** props
    //**********************************************************

    public ShopItem Selected => Items[_selected];

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Previous() => _selected = (_selected + Items.Length - 1) % Items.Length;

    public void Next() => _selected = (_selected + 1) % Items.Length;

    /// <summary>Selects an item if the counter sells it.</summary>
    public void Select(ShopItem item)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            if (Items[i] == item)
                _selected = i;
        }
    }

    public static bool IsUpgrade(ShopItem item) => item is >= ShopItem.Shovel and <= ShopItem.Detector;

    /// <summary>The upgrade line an upgrade item buys. Only for <see cref="IsUpgrade"/> items.</summary>
    public static UpgradeLine Line(ShopItem item) => (UpgradeLine)(item - ShopItem.Shovel);

    /// <summary>The selected item's name on the panel.</summary>
    public static string Name(ShopItem item, in Miner miner) => item switch
    {
        _ when IsUpgrade(item) => Upgrades.Name(Line(item)),
        ShopItem.LampOil => LampOil.FillName,
        ShopItem.OilFlask => LampOil.FlaskName,
        ShopItem.Blasting => miner.HasBlastingKit ? Shop.DynamiteName : Shop.BlastingKitName,
        _ => Shop.LadderName
    };
}
