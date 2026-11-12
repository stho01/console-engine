using System;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

public class TileGridTests
{
    [Test]
    public void IsSolid_OutsideGrid_False()
    {
        var grid = new TileGrid(2, 2, new Vector2(4, 4));
        grid.SetSolid(0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(grid.IsSolid(0, 0), Is.True);
            Assert.That(grid.IsSolid(-1, 0), Is.False);
            Assert.That(grid.IsSolid(0, 2), Is.False);
        });
    }

    [Test]
    public void ColumnOfAndRowOf_WithOriginAndNonSquareTiles_Floor()
    {
        var grid = new TileGrid(10, 10, new Vector2(4, 2), origin: new Vector2(-8, 0));

        Assert.Multiple(() =>
        {
            Assert.That(grid.ColumnOf(-8f), Is.EqualTo(0));
            Assert.That(grid.ColumnOf(-8.1f), Is.EqualTo(-1));
            Assert.That(grid.RowOf(3.9f), Is.EqualTo(1));
            Assert.That(grid.TileMin(1, 1), Is.EqualTo(new Vector2(-4, 2)));
        });
    }

    [Test]
    public void Overlaps_BoxTouchingTileEdge_False()
    {
        var grid = new TileGrid(3, 3, new Vector2(4, 4));
        grid.SetSolid(1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Overlaps(new Vector2(0, 4), new Vector2(4, 8)), Is.False, "left of the tile");
            Assert.That(grid.Overlaps(new Vector2(8, 4), new Vector2(12, 8)), Is.False, "right of the tile");
            Assert.That(grid.Overlaps(new Vector2(3.9f, 4), new Vector2(4.1f, 8)), Is.True);
        });
    }

    [Test]
    public void SetSolid_OutsideGrid_Throws()
    {
        var grid = new TileGrid(2, 2, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetSolid(2, 0));
    }

    [Test]
    public void Ctor_InvalidTileSize_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TileGrid(2, 2, new Vector2(0, 1)));
}
