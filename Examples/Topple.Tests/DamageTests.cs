using Microsoft.Xna.Framework;
using NUnit.Framework;
using Topple.Components;
using Topple.Levels;
using Topple.Systems;

namespace Topple.Tests;

/// <summary>Hits in a small headless level: flat ground, one piece, and a pit of water on the right.</summary>
public class DamageTests
{
    private const float Ground = LevelBuilder.GroundTop;
    private const float AfterSettle = ImpactDamageSystem.SettleSeconds + .2f;

    [Test]
    public void Throw_StoneIntoWoodColumn_BreaksIt()
    {
        using var simulation = Simulate(Column(Material.Wood, 40f));
        simulation.Run(AfterSettle);

        simulation.Throw(ShotKind.Stone, new Vector2(30f, Ground - 3f), new Vector2(45f, 0f));
        simulation.Run(1f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.Count<Block>(), Is.Zero);
            Assert.That(simulation.Round.Score, Is.EqualTo(Materials.Get(Material.Wood).Score));
        });
    }

    [Test]
    public void Throw_SlowStoneAgainstStoneColumn_LeavesItStanding()
    {
        using var simulation = Simulate(Column(Material.Stone, 40f));
        simulation.Run(AfterSettle);

        simulation.Throw(ShotKind.Stone, new Vector2(36f, Ground - 3f), new Vector2(12f, 0f));
        simulation.Run(1f);

        Assert.That(simulation.Count<Block>(), Is.EqualTo(1));
    }

    [Test]
    public void Throw_BombBesideGlassColumn_BlastBreaksIt()
    {
        using var simulation = Simulate(Column(Material.Glass, 40f));
        simulation.Run(AfterSettle);

        simulation.Throw(ShotKind.Bomb, new Vector2(37f, Ground - 2f), Vector2.Zero);
        simulation.Run(1f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.Count<Shot>(), Is.Zero, "the bomb went off");
            Assert.That(simulation.Count<Block>(), Is.Zero, "the glass broke");
        });
    }

    [Test]
    public void Run_CrystalDroppedFromHigh_Breaks()
    {
        using var simulation = Simulate();
        simulation.Run(AfterSettle);

        simulation.Place(Crystal(40f, Ground - 10f));
        simulation.Run(2f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.Count<Crystal>(), Is.Zero);
            Assert.That(simulation.Round.Score, Is.EqualTo(Materials.Get(Material.Crystal).Score));
        });
    }

    [Test]
    public void Run_CrystalDroppedLow_Survives()
    {
        using var simulation = Simulate();
        simulation.Run(AfterSettle);

        simulation.Place(Crystal(40f, Ground - 2f));
        simulation.Run(2f);

        Assert.That(simulation.Count<Crystal>(), Is.EqualTo(1));
    }

    [Test]
    public void Run_CrystalFallingIntoWater_Sinks()
    {
        using var simulation = Simulate(Crystal(100f, Ground - 3f));

        simulation.Run(1f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.Count<Crystal>(), Is.Zero);
            Assert.That(simulation.Round.Score, Is.EqualTo(Materials.Get(Material.Crystal).Score));
        });
    }

    [Test]
    public void Run_StoneComingToRest_IsRemoved()
    {
        using var simulation = Simulate();
        simulation.Throw(ShotKind.Stone, new Vector2(40f, Ground - 1f), Vector2.Zero);

        simulation.Run(3f);

        Assert.That(simulation.Count<Shot>(), Is.Zero);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static LevelSimulation Simulate(params Piece[] pieces) =>
        new(new LevelPlan
        {
            Name = "TEST",
            Stones = 3,
            Pits = [new TileArea(95, 50, 105, 56)],
            Pieces = pieces
        });

    private static Piece Column(Material material, float x) =>
        new(PieceShape.Box, material, new Vector2(x + .5f, Ground - 3f), new Vector2(.5f, 3f));

    private static Piece Crystal(float x, float bottom) =>
        new(PieceShape.Crystal, Material.Crystal, new Vector2(x + 1f, bottom - 1f), Vector2.One);
}
