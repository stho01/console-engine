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
    OilFlask,
    Pickaxe,
    /// <summary>The deep camp at 100 m, bought once at the surface (<see cref="EternalDigger.DeepCamp"/>).</summary>
    DeepCamp,
    /// <summary>The camp's counter buys the bag (<see cref="EternalDigger.DeepCamp.SellBag"/>).</summary>
    SellBag
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
        ShopItem.Ladder, ShopItem.LampOil, ShopItem.OilFlask, ShopItem.Shovel, ShopItem.Pickaxe, ShopItem.Lamp, ShopItem.Bag,
        ShopItem.Detector, ShopItem.DeepCamp, ShopItem.Blasting
    ];

    /// <summary>The deep camp's counter: the bag first, then supplies at a markup.</summary>
    public static readonly ShopItem[] CampItems =
        [ShopItem.SellBag, ShopItem.Ladder, ShopItem.LampOil, ShopItem.OilFlask, ShopItem.Blasting];

    private readonly ShopItem[] _items;

    private int _selected;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <summary>The surface counter (<see cref="Items"/>).</summary>
    public ShopCounter() : this(Items)
    {
    }

    /// <summary>A counter selling <paramref name="items"/>, the first one selected.</summary>
    public ShopCounter(ShopItem[] items) => _items = items;

    //**********************************************************
    //** props
    //**********************************************************

    public ShopItem Selected => _items[_selected];

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Previous() => _selected = (_selected + _items.Length - 1) % _items.Length;

    public void Next() => _selected = (_selected + 1) % _items.Length;

    /// <summary>Selects an item if the counter sells it.</summary>
    public void Select(ShopItem item)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i] == item)
                _selected = i;
        }
    }

    public static bool IsUpgrade(ShopItem item) => item is >= ShopItem.Shovel and <= ShopItem.Detector or ShopItem.Pickaxe;

    /// <summary>The upgrade line an upgrade item buys. Only for <see cref="IsUpgrade"/> items.</summary>
    public static UpgradeLine Line(ShopItem item) =>
        item == ShopItem.Pickaxe ? UpgradeLine.Pickaxe : (UpgradeLine)(item - ShopItem.Shovel);

    /// <summary>The selected item's name on the panel.</summary>
    public static string Name(ShopItem item, in Miner miner) => item switch
    {
        _ when IsUpgrade(item) => Upgrades.Name(Line(item)),
        ShopItem.LampOil => LampOil.FillName,
        ShopItem.OilFlask => LampOil.FlaskName,
        ShopItem.DeepCamp => DeepCamp.Name,
        ShopItem.SellBag => DeepCamp.SellBagName,
        ShopItem.Blasting => miner.HasBlastingKit ? Shop.DynamiteName : Shop.BlastingKitName,
        _ => Shop.LadderName
    };
}
