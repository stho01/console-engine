using System.Collections.Generic;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Saving;

/// <summary>
/// Everything a save holds: the seed, the miner's tile, facing and inventory, the lift, and the tiles that differ
/// from what the seed generates. The mine itself is never stored, it is regenerated from the seed. Built only when saving or
/// loading, never per frame.
/// </summary>
public sealed class SaveData
{
    //**********************************************************
    //** props
    //**********************************************************

    public int Seed { get; init; }
    public int Column { get; init; }
    public int Row { get; init; }

    /// <summary>-1 left, 1 right.</summary>
    public int FacingX { get; init; } = 1;

    public int Gold { get; init; }
    public int Ladders { get; init; }
    public int CopperOre { get; init; }
    public int SilverOre { get; init; }
    public int GoldOre { get; init; }
    public int GemOre { get; init; }

    /// <summary>Rare finds in the bag, and ever found (for the trophy shelf).</summary>
    public int Fossils { get; init; }
    public int Relics { get; init; }
    public int FossilsFound { get; init; }
    public int RelicsFound { get; init; }
    public bool HasBlastingKit { get; init; }

    /// <summary>Upgrade tiers, 0 to <see cref="Upgrades.MaxTier"/>.</summary>
    public int ShovelTier { get; init; }
    public int LampTier { get; init; }
    public int BagTier { get; init; }
    public int DetectorTier { get; init; }

    /// <summary>Lamp oil. Saves from before lamp oil load with a full tank.</summary>
    public float Oil { get; init; } = LampOil.TankSize;

    /// <summary>Sticks carried. Sticks whose fuse was burning when the game saved are counted back in.</summary>
    public int Dynamite { get; init; }

    /// <summary>Lift segments built (<see cref="Elevator.SegmentMetres"/> m each). 0 = no lift.</summary>
    public int ElevatorSegments { get; init; }

    /// <summary>The landing the lift car stands at (or was heading for).</summary>
    public int ElevatorCarRow { get; init; } = Elevator.TopRow;

    /// <summary>Tiles that differ from the generated mine, top row first.</summary>
    public List<TileChange> Changes { get; init; } = [];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The start of a new mine: the spawn tile, start gold and ladders, an empty bag, no changed tiles.</summary>
    public static SaveData NewMine(int seed) => new()
    {
        Seed = seed,
        Column = Layout.SpawnColumn,
        Row = Layout.GroundRow,
        Gold = Shop.StartGold,
        Ladders = Shop.StartLadders,
        Oil = LampOil.TankSize
    };

    /// <summary>Takes the miner and the mine as they are now. <paramref name="changes"/> is filled and kept.</summary>
    /// <remarks>A miner riding the lift is saved standing at the landing the car is heading for.</remarks>
    public static SaveData Capture(int seed, in Miner miner, Mine mine, Elevator elevator, List<TileChange> changes)
    {
        changes.Clear();
        mine.CollectChanges(changes);
        return new SaveData
        {
            Seed = seed,
            Column = miner.Column,
            Row = miner.IsRiding ? elevator.CarTargetRow : miner.Row,
            FacingX = miner.FacingX < 0 ? -1 : 1,
            Gold = miner.Gold,
            Ladders = miner.Ladders,
            CopperOre = miner.CopperOre,
            SilverOre = miner.SilverOre,
            GoldOre = miner.GoldOre,
            GemOre = miner.GemOre,
            Fossils = miner.Fossils,
            Relics = miner.Relics,
            FossilsFound = miner.FossilsFound,
            RelicsFound = miner.RelicsFound,
            HasBlastingKit = miner.HasBlastingKit,
            ShovelTier = miner.ShovelTier,
            LampTier = miner.LampTier,
            BagTier = miner.BagTier,
            DetectorTier = miner.DetectorTier,
            Oil = miner.Oil,
            Dynamite = miner.Dynamite + miner.LitDynamite,
            ElevatorSegments = elevator.Segments,
            ElevatorCarRow = elevator.CarTargetRow,
            Changes = changes
        };
    }

    /// <summary>A standing miner on the saved tile, with the saved inventory.</summary>
    public Miner ToMiner() => new()
    {
        Column = Column,
        Row = Row,
        MoveFrom = new Vector2(Column, Row),
        FacingX = FacingX,
        LampDirection = new Vector2(FacingX, 0f),
        Gold = Gold,
        Ladders = Ladders,
        CopperOre = CopperOre,
        SilverOre = SilverOre,
        GoldOre = GoldOre,
        GemOre = GemOre,
        Fossils = Fossils,
        Relics = Relics,
        FossilsFound = FossilsFound,
        RelicsFound = RelicsFound,
        HasBlastingKit = HasBlastingKit,
        Dynamite = Dynamite,
        ShovelTier = ShovelTier,
        LampTier = LampTier,
        BagTier = BagTier,
        DetectorTier = DetectorTier,
        Oil = Oil
    };

    /// <summary>Puts the saved tiles back into a mine made from <see cref="Seed"/>, and the lift back.</summary>
    public void ApplyTo(Mine mine, Elevator elevator)
    {
        for (var i = 0; i < Changes.Count; i++)
            mine.Apply(Changes[i]);

        elevator.Restore(ElevatorSegments, ElevatorCarRow);
    }
}
