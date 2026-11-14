using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class LightMapTests
{
    private const int Seed = 99;
    private const int MaxRows = 30;

    /// <summary>A row far below daylight.</summary>
    private const int Row = Mine.SurfaceRow + 50;

    private const int Column = 5;

    [Test]
    public void Compute_SurfaceRows_AreBright()
    {
        var (mine, light) = Create();

        light.Compute(mine, 0, MaxRows, new Vector2(Column + .5f, Row + .5f), Vector2.UnitX);

        Assert.That(light.Get(20, 3), Is.EqualTo(LightMap.Bright));
        Assert.That(light.Get(20, Mine.SurfaceRow), Is.EqualTo(LightMap.Bright));
    }

    [Test]
    public void Get_OutsideTheWindow_IsDark()
    {
        var (mine, light) = Create();

        light.Compute(mine, 0, 5, new Vector2(Column + .5f, 2.5f), Vector2.UnitX);

        Assert.That(light.Get(Column, 6), Is.EqualTo(LightMap.Dark));
        Assert.That(light.Get(-1, 2), Is.EqualTo(LightMap.Dark));
    }

    [Test]
    public void Compute_SolidNeighbourOfTheLamp_IsBright()
    {
        var (mine, light) = Create();
        RockAround(mine);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column - 1, Row), Is.EqualTo(LightMap.Bright));
        Assert.That(light.Get(Column, Row + 1), Is.EqualTo(LightMap.Bright));
    }

    [Test]
    public void Compute_TunnelAheadInTheCone_IsBrightAndSeen()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 4);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 3, Row), Is.EqualTo(LightMap.Bright));
        Assert.That(mine.At(Column + 3, Row).Seen, Is.True);
    }

    [Test]
    public void Compute_TileBehindSolidRock_IsDark()
    {
        var (mine, light) = Create();
        RockAround(mine);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 3, Row), Is.EqualTo(LightMap.Dark));
        Assert.That(mine.At(Column + 3, Row).Seen, Is.False);
    }

    [Test]
    public void Compute_TunnelBehindTheMiner_IsDark()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column - 4, Column - 1);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column - 3, Row), Is.EqualTo(LightMap.Dark));
    }

    [Test]
    public void Compute_FarEndOfALongTunnel_GetsDimmer()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 9);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 5, Row), Is.EqualTo(LightMap.Lit));
        Assert.That(light.Get(Column + 7, Row), Is.EqualTo(LightMap.Dim));
        Assert.That(light.Get(Column + 9, Row), Is.EqualTo(LightMap.Dark));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static (Mine Mine, LightMap Light) Create() => (new Mine(Seed), new LightMap(Mine.Columns, MaxRows));

    private static void ComputeAt(Mine mine, LightMap light, Vector2 direction) =>
        light.Compute(mine, Row - MaxRows / 2, MaxRows, new Vector2(Column + .5f, Row + .5f), direction);

    /// <summary>Solid stone around the miner's tile (which is air), so caves in the seed can't change the result.</summary>
    private static void RockAround(Mine mine)
    {
        for (var row = Row - 10; row <= Row + 10; row++)
        for (var column = 1; column < Mine.Columns - 1; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Stone };

        mine.At(Column, Row) = new TileCell { Type = TileType.Air };
    }

    private static void DigTunnel(Mine mine, int fromColumn, int toColumn)
    {
        for (var column = fromColumn; column <= toColumn; column++)
            mine.At(column, Row) = new TileCell { Type = TileType.Air };
    }
}
