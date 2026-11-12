using ConsoleEngine.Physics;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using Topple.Components;
using Topple.Levels;

namespace Topple.Tests;

/// <summary>Every level in <see cref="LevelBook"/>, simulated headless: towers must stand until they are hit.</summary>
public class LevelTests
{
    private static readonly int[] LevelIndices = [0, 1, 2, 3];

    [Test]
    public void LevelBook_HasOneTestCasePerLevel() =>
        Assert.That(LevelIndices, Has.Length.EqualTo(LevelBook.Levels.Length));

    [TestCaseSource(nameof(LevelIndices))]
    public void Pieces_InEveryLevel_DoNotStartInsideTheGround(int level)
    {
        var plan = LevelBook.Levels[level];
        var tiles = LevelBuilder.CreateTiles(plan, ToppleGame.ScreenWidth, ToppleGame.ScreenHeight);
        var margin = new Vector2(.01f);

        Assert.Multiple(() =>
        {
            foreach (var piece in plan.Pieces)
            {
                Assert.That(tiles.Overlaps(piece.Center - piece.HalfSize + margin, piece.Center + piece.HalfSize - margin),
                    Is.False, $"{piece.Shape} {piece.Material} at {piece.Center}");
            }
        });
    }

    [TestCaseSource(nameof(LevelIndices))]
    public void Run_EveryLevelUntouched_NothingBreaksForEightSeconds(int level)
    {
        var plan = LevelBook.Levels[level];
        using var simulation = new LevelSimulation(plan);
        var blocks = simulation.Count<Block>();
        var crystals = simulation.Count<Crystal>();

        simulation.Run(8f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.Count<Block>(), Is.EqualTo(blocks), "blocks");
            Assert.That(simulation.Count<Crystal>(), Is.EqualTo(crystals), "crystals");
            Assert.That(simulation.Round.Score, Is.Zero, "score");
            Assert.That(crystals, Is.GreaterThan(0), "a level needs a target");
        });
    }

    /// <summary>Blocks on a moving ferry stay awake, so only levels without one.</summary>
    [TestCaseSource(nameof(LevelIndices))]
    public void Run_EveryLevelWithoutFerry_TowersFallAsleep(int level)
    {
        var plan = LevelBook.Levels[level];
        Assume.That(plan.Ferry, Is.Null);

        using var simulation = new LevelSimulation(plan);
        simulation.Run(4f);

        var awake = 0;
        foreach (var chunk in simulation.World.Query(Query.With<Block, RigidBody>()).GetChunks())
        {
            var bodies = chunk.GetSpan<RigidBody>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!bodies[i].IsSleeping)
                    awake++;
            }
        }

        Assert.That(awake, Is.Zero);
    }
}
