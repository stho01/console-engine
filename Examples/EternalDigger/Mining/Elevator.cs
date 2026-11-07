using System;
using EternalDigger.Components;

namespace EternalDigger.Mining;

/// <summary>What buying an elevator segment did.</summary>
public enum ElevatorBuild : byte
{
    Built,
    /// <summary>Not enough gold for <see cref="Elevator.NextSegmentPrice"/>.</summary>
    TooPoor,
    /// <summary>A tile in the next segment can't be dug out by the builders (unbroken obsidian).</summary>
    Blocked
}

/// <summary>
/// The lift in the mine shaft (<see cref="Column"/>). Bought in <see cref="SegmentMetres"/> m segments, each of which
/// digs that stretch of the shaft out (ore found on the way goes into the buyer's bag; ladders stay). It has two
/// landings: the surface (<see cref="TopRow"/>) and the bottom of the last segment (<see cref="BottomRow"/>). The car
/// rides between them at <see cref="RideDuration"/> s per tile. All rows are tile rows.
/// </summary>
public sealed class Elevator
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Metres (tiles) each purchase adds.</summary>
    public const int SegmentMetres = 10;

    /// <summary>Gold for the first segment.</summary>
    public const int FirstSegmentPrice = 200;

    /// <summary>Each further segment costs this much more than the one before.</summary>
    public const int SegmentPriceStep = 100;

    /// <summary>Seconds per tile for the car.</summary>
    public const float RideDuration = .04f;

    /// <summary>The lift runs down the mine shaft.</summary>
    public const int Column = Mine.ShaftColumn;

    /// <summary>The surface landing: the walk row above the shaft.</summary>
    public const int TopRow = Layout.GroundRow;

    //**********************************************************
    //** props
    //**********************************************************

    public int Segments { get; private set; }
    public bool IsBuilt => Segments > 0;

    /// <summary>Metres below the grass the lift reaches.</summary>
    public int Depth => Segments * SegmentMetres;

    /// <summary>The bottom landing's row. Only meaningful once <see cref="IsBuilt"/>.</summary>
    public int BottomRow => Mine.SurfaceRow + Depth;

    public int NextSegmentPrice => FirstSegmentPrice + Segments * SegmentPriceStep;

    /// <summary>The car's row, fractional while it moves.</summary>
    public float CarRow { get; private set; } = TopRow;

    /// <summary>The landing the car is at or heading for.</summary>
    public int CarTargetRow { get; private set; } = TopRow;

    public bool IsCarMoving => CarRow != CarTargetRow;

    /// <summary>The car was called to a landing and hasn't picked anyone up there yet.</summary>
    public bool IsCalled { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool IsLanding(int row) => IsBuilt && (row == TopRow || row == BottomRow);

    /// <summary>The car is standing still at this row.</summary>
    public bool IsCarAt(int row) => !IsCarMoving && CarTargetRow == row;

    /// <summary>
    /// Builds the next segment for <see cref="NextSegmentPrice"/>: digs out the shaft rows below the current bottom,
    /// putting ore into the miner's bag.
    /// </summary>
    public ElevatorBuild TryBuild(Mine mine, ref Miner miner)
    {
        var price = NextSegmentPrice;
        if (miner.Gold < price)
            return ElevatorBuild.TooPoor;

        var firstRow = Mine.SurfaceRow + Depth + 1;
        var lastRow = firstRow + SegmentMetres - 1;
        for (var row = firstRow; row <= lastRow; row++)
        {
            if (!CanDigOut(mine.TypeAt(Column, row)))
                return ElevatorBuild.Blocked;
        }

        for (var row = firstRow; row <= lastRow; row++)
        {
            ref var cell = ref mine.At(Column, row);
            Shop.Collect(ref miner, cell.Type);
            cell = new TileCell { Type = TileType.Air, HasLadder = cell.HasLadder, Seen = cell.Seen };
        }

        miner.Gold -= price;
        Segments++;
        return ElevatorBuild.Built;
    }

    /// <summary>Sends the car to a landing. A call (<paramref name="isCall"/>) picks up whoever waits there.</summary>
    public void Send(int landing, bool isCall = false)
    {
        if (!IsLanding(landing))
            return;

        CarTargetRow = landing;
        IsCalled = isCall;
    }

    /// <summary>Clears the call once the caller is aboard.</summary>
    public void PickedUp() => IsCalled = false;

    /// <summary>Moves the car towards its landing. Returns true in the frame it arrives.</summary>
    public bool Update(float deltaSeconds)
    {
        if (!IsCarMoving)
            return false;

        var step = deltaSeconds / RideDuration;
        CarRow = CarRow < CarTargetRow
            ? Math.Min(CarTargetRow, CarRow + step)
            : Math.Max(CarTargetRow, CarRow - step);
        return !IsCarMoving;
    }

    /// <summary>Puts a loaded lift back: its segments and the landing the car stands at.</summary>
    public void Restore(int segments, int carRow)
    {
        Segments = Math.Max(0, segments);
        CarTargetRow = IsLanding(carRow) ? carRow : TopRow;
        CarRow = CarTargetRow;
        IsCalled = false;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The builders dig out anything but the mine's unbreakable side walls and surface.</summary>
    private static bool CanDigOut(TileType type) => type == TileType.Air || Tiles.IsBreakable(type);
}
