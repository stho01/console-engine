using System;
using Microsoft.Xna.Framework;

namespace EternalDigger.Mining;

/// <summary>
/// Finds and burns gas pockets: air tiles with <see cref="TileCell.HasGas"/>, which the mine generates in caves from
/// <see cref="Tiles.GasDepth"/>. A pocket is the gas tiles connected to each other (side by side, up to
/// <see cref="MaxPocket"/> burn at once). Burning clears the gas for good; the tiles become ordinary changed tiles.
/// Uses preallocated buffers, so it never allocates.
/// </summary>
public sealed class GasPockets(Mine mine)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The most gas tiles one flare burns. A bigger pocket burns the rest on the next flare.</summary>
    public const int MaxPocket = 24;

    private readonly Point[] _burned = new Point[MaxPocket];
    private readonly Point[] _open = new Point[MaxPocket * 4];

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The tiles the last <see cref="Ignite"/> burned.</summary>
    public ReadOnlySpan<Point> Burned => _burned.AsSpan(0, BurnedCount);

    public int BurnedCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The nearest gas tile within <paramref name="radius"/> tiles of a tile's centre, if there is one.</summary>
    public bool TryFindNear(int column, int row, float radius, out Point found)
    {
        found = default;
        var reach = (int)MathF.Ceiling(radius);
        var best = float.MaxValue;
        for (var r = row - reach; r <= row + reach; r++)
        for (var c = column - reach; c <= column + reach; c++)
        {
            var distanceSquared = (c - column) * (c - column) + (r - row) * (r - row);
            if (distanceSquared > radius * radius || distanceSquared >= best || !Mine.IsInside(c, r) ||
                !mine.At(c, r).HasGas)
                continue;

            best = distanceSquared;
            found = new Point(c, r);
        }

        return best < float.MaxValue;
    }

    /// <summary>
    /// Burns the pocket that <paramref name="column"/>, <paramref name="row"/> belongs to: clears the gas of up to
    /// <see cref="MaxPocket"/> connected tiles and lists them in <see cref="Burned"/>. Returns how many burned.
    /// </summary>
    public int Ignite(int column, int row)
    {
        BurnedCount = 0;
        if (!Mine.IsInside(column, row) || !mine.At(column, row).HasGas)
            return 0;

        var open = 0;
        mine.At(column, row).HasGas = false;
        _open[open++] = new Point(column, row);
        while (open > 0 && BurnedCount < MaxPocket)
        {
            var tile = _open[--open];
            _burned[BurnedCount++] = tile;
            open = Spread(tile.X - 1, tile.Y, open);
            open = Spread(tile.X + 1, tile.Y, open);
            open = Spread(tile.X, tile.Y - 1, open);
            open = Spread(tile.X, tile.Y + 1, open);
        }

        // Queued but over the limit: they keep their gas for the next flare.
        for (var i = 0; i < open; i++)
            mine.At(_open[i].X, _open[i].Y).HasGas = true;

        return BurnedCount;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Queues a neighbouring gas tile (clearing its gas so it is only queued once).</summary>
    private int Spread(int column, int row, int open)
    {
        if (open >= _open.Length || !Mine.IsInside(column, row))
            return open;

        ref var cell = ref mine.At(column, row);
        if (!cell.HasGas)
            return open;

        cell.HasGas = false;
        _open[open] = new Point(column, row);
        return open + 1;
    }
}
