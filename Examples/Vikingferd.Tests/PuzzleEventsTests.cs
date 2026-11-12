using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Tests;

/// <summary>The events <see cref="PuzzleRules.Step"/> reports for effects.</summary>
public class PuzzleEventsTests
{
    private static readonly string[] Room =
    [
        "########",
        "#......#",
        "#..1...#",
        "#G.%...#",
        "########"
    ];

    [Test]
    public void Step_GormSmashes_ReportsEachSmashedTile()
    {
        var (stage, actors, events) = Load(Room);
        actors[0].Min.X = 2.9f - actors[0].Size.X;
        actors[0].UseRequested = true;

        PuzzleRules.Step(stage, actors, Party.StepSeconds, events);

        Assert.That(events.Count, Is.EqualTo(1));
        Assert.That(events.All[0].Kind, Is.EqualTo(PuzzleEventKind.Smashed));
    }

    [Test]
    public void Step_LeverPulled_ReportsLeverWithItsChannel()
    {
        var (stage, actors, events) = Load(Room);
        actors[0].Min.X = 3.1f;
        actors[0].UseRequested = true;

        PuzzleRules.Step(stage, actors, Party.StepSeconds, events);

        Assert.That(events.All[0].Kind, Is.EqualTo(PuzzleEventKind.LeverPulled));
        Assert.That(events.All[0].Channel, Is.Zero);
    }

    [Test]
    public void Step_Jump_ReportsJumped()
    {
        var (stage, actors, events) = Load(Room);
        actors[0].JumpRequested = true;

        PuzzleRules.Step(stage, actors, Party.StepSeconds, events);

        Assert.That(events.All[0].Kind, Is.EqualTo(PuzzleEventKind.Jumped));
    }

    [Test]
    public void Add_PastCapacity_DropsTheRest()
    {
        var events = new PuzzleEvents();
        for (var i = 0; i < PuzzleEvents.Capacity + 5; i++)
            events.Add(PuzzleEventKind.Jumped, default);

        Assert.That(events.Count, Is.EqualTo(PuzzleEvents.Capacity));
    }

    private static (Stage, Actor[], PuzzleEvents) Load(string[] rows)
    {
        var stage = Stage.Parse(rows, out var spawns);
        var actors = new Actor[spawns.Length];
        for (var i = 0; i < spawns.Length; i++)
            actors[i] = Heroes.Create(spawns[i]);

        // Let everything settle on the floor before the step under test.
        for (var i = 0; i < 30; i++)
            PuzzleRules.Step(stage, actors, Party.StepSeconds);

        return (stage, actors, new PuzzleEvents());
    }
}
