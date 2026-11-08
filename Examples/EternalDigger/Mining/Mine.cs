using System;
using System.Collections.Generic;

namespace EternalDigger.Mining;

/// <summary>
/// The world grid: sky, the surface row, and an underground that goes down forever. Rows are generated from the seed
/// the first time they are touched, in blocks of <see cref="RowsPerBlock"/>, so the same seed always gives the same
/// mine. Columns 0 and <see cref="Columns"/> - 1 below the surface are bedrock. All coordinates are tile columns and
/// rows; row 0 is the top of the sky.
/// </summary>
public sealed class Mine
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Columns = 30;

    /// <summary>The grass row. Depth 0. Rows above it are sky.</summary>
    public const int SurfaceRow = 10;

    /// <summary>The column of the mine entrance: the grass and the row below it are dug out and have ladders.</summary>
    public const int ShaftColumn = 21;

    /// <summary>Rows generated at a time. Generating allocates, so it happens rarely and never per frame.</summary>
    public const int RowsPerBlock = 64;

    private static TileCell _outside;

    private readonly int _seed;
    private TileCell[] _cells = new TileCell[RowsPerBlock * Columns];
    private int _generatedRows;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Mine(int seed)
    {
        _seed = seed;
        Generate(RowsPerBlock);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Rows generated so far. Grows as deeper rows are touched.</summary>
    public int GeneratedRows => _generatedRows;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Metres below the grass. Negative in the sky.</summary>
    public static int Depth(int row) => row - SurfaceRow;

    public static bool IsInside(int column, int row) => (uint)column < Columns && row >= 0;

    /// <summary>
    /// The tile at a column and row. Generates rows down to <paramref name="row"/> if needed. Outside the mine (left,
    /// right or above the sky) it returns a scratch cell: bedrock beside the mine (also next to the sky, so the miner
    /// can't walk off the screen), air above it. Writes to that cell are discarded.
    /// </summary>
    public ref TileCell At(int column, int row)
    {
        if (!IsInside(column, row))
        {
            _outside = new TileCell { Type = (uint)column < Columns ? TileType.Air : TileType.Bedrock, Seen = true };
            return ref _outside;
        }

        if (row >= _generatedRows)
            Generate(row + 1);

        return ref _cells[row * Columns + column];
    }

    public TileType TypeAt(int column, int row) => At(column, row).Type;

    public bool IsSolid(int column, int row) => Tiles.IsSolid(TypeAt(column, row));

    public bool HasLadder(int column, int row) => At(column, row).HasLadder;

    /// <summary>Whether a miner in this (air) tile stands still: on solid ground, on a ladder, or on top of one.</summary>
    public bool IsSupported(int column, int row) =>
        HasLadder(column, row) || IsSolid(column, row + 1) || HasLadder(column, row + 1);

    /// <summary>
    /// One shovel hit. The tile breaks (becomes air) on its last hit; a better pickaxe needs
    /// <paramref name="fewerHits"/> fewer (<see cref="Tiles.HitsToBreak"/>).
    /// </summary>
    public DigResult Hit(int column, int row, int fewerHits = 0)
    {
        if (!IsInside(column, row))
            return DigResult.Nothing;

        ref var cell = ref At(column, row);
        if (!Tiles.IsBreakable(cell.Type))
            return DigResult.Nothing;

        var type = cell.Type;
        var hitsToBreak = Tiles.HitsToBreak(type, Depth(row), fewerHits);
        cell.Damage++;
        if (cell.Damage < hitsToBreak)
            return new DigResult(DigOutcome.Damaged, type, cell.Damage, hitsToBreak);

        cell = new TileCell { Type = TileType.Air, Seen = true };
        return new DigResult(DigOutcome.Broken, type, hitsToBreak, hitsToBreak);
    }

    /// <summary>Puts a ladder in a dug-out tile below the surface. False if it is solid, in the sky, or has one.</summary>
    public bool TryPlaceLadder(int column, int row)
    {
        if (!IsInside(column, row) || row <= SurfaceRow)
            return false;

        ref var cell = ref At(column, row);
        if (cell.Type != TileType.Air || cell.HasLadder)
            return false;

        cell.HasLadder = true;
        return true;
    }

    /// <summary>
    /// Adds every generated tile that differs from what the seed generates (dug, damaged, ladder placed or seen) to
    /// <paramref name="changes"/>, top row first. Regenerates each row to compare, so it is for saving only, never
    /// per frame.
    /// </summary>
    public void CollectChanges(List<TileChange> changes)
    {
        for (var row = 0; row < _generatedRows; row++)
        for (var column = 0; column < Columns; column++)
        {
            var cell = _cells[row * Columns + column];
            if (!IsSame(cell, Create(column, row)))
                changes.Add(new TileChange(column, row, cell));
        }
    }

    /// <summary>Puts a saved tile back. Generates rows down to it first. Changes outside the mine are ignored.</summary>
    public void Apply(in TileChange change)
    {
        if (IsInside(change.Column, change.Row))
            At(change.Column, change.Row) = change.Cell;
    }

    /// <summary>
    /// The depth of the obsidian wall's last row in a column: <see cref="Tiles.WallDepth"/> plus 2–4, so the wall is
    /// <see cref="Tiles.WallMinThickness"/> to <see cref="Tiles.WallMaxThickness"/> tiles thick.
    /// </summary>
    public int WallBottomDepth(int column)
    {
        var range = Tiles.WallMaxThickness - Tiles.WallMinThickness + 1;
        var thickness = Tiles.WallMinThickness + Math.Min(range - 1, (int)(Noise(column, 0, 41) * range));
        return Tiles.WallDepth + thickness - 1;
    }

    /// <summary>
    /// A value in [0, 1) that only depends on the seed and the arguments. For textures and effects that must not
    /// flicker.
    /// </summary>
    public float Noise(int x, int y, int salt) => Noise01(x, y, _seed + salt * 7919);

    //**********************************************************
    //** generation
    //**********************************************************

    private void Generate(int rowCount)
    {
        var target = (rowCount + RowsPerBlock - 1) / RowsPerBlock * RowsPerBlock;
        if (target * Columns > _cells.Length)
            Array.Resize(ref _cells, Math.Max(target * Columns, _cells.Length * 2));

        for (var row = _generatedRows; row < target; row++)
        for (var column = 0; column < Columns; column++)
            _cells[row * Columns + column] = Create(column, row);

        _generatedRows = target;
    }

    private TileCell Create(int column, int row)
    {
        if (row < SurfaceRow)
            return new TileCell { Type = TileType.Air };

        var depth = Depth(row);
        if (column == ShaftColumn && depth <= 1)
            return new TileCell { Type = TileType.Air, HasLadder = true };

        if (row == SurfaceRow)
            return new TileCell { Type = TileType.Grass };

        if (column == 0 || column == Columns - 1)
            return new TileCell { Type = TileType.Bedrock };

        if (depth < Tiles.WallDepth)
            return new TileCell { Type = Underground(column, row, depth) };

        var wallBottom = WallBottomDepth(column);
        var type = depth <= wallBottom ? TileType.Obsidian : BelowTheWall(column, row, depth - wallBottom);
        return new TileCell { Type = type };
    }

    private TileType Underground(int column, int row, int depth)
    {
        // Ores come in veins: a coarse 2 x 2 noise picks the vein, a fine one leaves gaps in it. Deeper veins are richer.
        var goldChance = Math.Min(.02f + (depth - Tiles.GoldDepth) * .0008f, .06f);
        var silverChance = Math.Min(.04f + (depth - Tiles.SilverDepth) * .001f, .08f);
        if (depth >= Tiles.FossilDepth && Noise(column, row, 61) < Tiles.FossilChance)
            return TileType.Fossil;
        if (depth >= Tiles.GoldDepth && IsVein(column, row, 11, goldChance))
            return TileType.Gold;
        if (depth >= Tiles.SilverDepth && IsVein(column, row, 13, silverChance))
            return TileType.Silver;
        if (depth >= Tiles.CopperDepth && IsVein(column, row, 17, .08f))
            return TileType.Copper;

        if (depth >= Tiles.CaveDepth && (Noise(column >> 1, row, 19) + Noise(column, row >> 1, 23)) * .5f < .06f)
            return TileType.Air;

        // Blobby rock: half coarse, half fine noise.
        var rock = Noise(column >> 1, row, 29) * .5f + Noise(column, row, 31) * .5f;
        var granite = depth >= Tiles.GraniteDepth ? Math.Min((depth - Tiles.GraniteDepth) * .01f, .45f) : 0f;
        var stone = Math.Clamp(.1f + depth * .012f, .1f, .7f);
        if (rock < granite)
            return TileType.Granite;

        return rock < granite + stone ? TileType.Stone : TileType.Dirt;
    }

    private static bool IsSame(in TileCell a, in TileCell b) =>
        a.Type == b.Type && a.Damage == b.Damage && a.HasLadder == b.HasLadder && a.Seen == b.Seen;

    /// <summary>Act 2: slate with quartzite blobs, gem veins, rare relics and caves. No act 1 rock or ore.</summary>
    private TileType BelowTheWall(int column, int row, int depthBelowWall)
    {
        if (Noise(column, row, 67) < Tiles.RelicChance)
            return TileType.Relic;

        if (IsVein(column, row, 37, Math.Min(.04f + depthBelowWall * .0005f, .07f)))
            return TileType.Gem;

        if ((Noise(column >> 1, row, 43) + Noise(column, row >> 1, 47)) * .5f < .06f)
            return TileType.Air;

        var rock = Noise(column >> 1, row, 53) * .5f + Noise(column, row, 59) * .5f;
        return rock < Math.Min(.25f + depthBelowWall * .004f, .5f) ? TileType.Quartzite : TileType.Slate;
    }

    private bool IsVein(int column, int row, int salt, float chance) =>
        Noise(column >> 1, row >> 1, salt) < chance * 1.6f && Noise(column, row, salt + 1) < .6f;

    private static float Noise01(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE3Du;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h >> 8) * (1f / (1 << 24));
        }
    }
}
