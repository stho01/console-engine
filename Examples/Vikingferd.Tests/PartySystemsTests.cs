using ConsoleEngine.Ecs;
using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Systems;

namespace Vikingferd.Tests;

/// <summary>
/// <see cref="PartyControlSystem"/> and <see cref="PuzzleSystem"/> in a real world, wired like
/// <c>LevelScene</c> but with scripted <see cref="PartyInput"/> instead of the keyboard.
/// </summary>
public class PartySystemsTests
{
    private const float FrameSeconds = 1f / 120f;

    private World _world;
    private Expedition _expedition;
    private SystemGroup _fixed;
    private SystemGroup _simulation;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        var stage = Stage.Parse(Levels.All[0].Rows, out var spawns);
        _expedition = new Expedition(0, stage);
        foreach (var spawn in spawns)
            _world.Create(Heroes.Create(spawn));

        _fixed = new SystemGroup(_world);
        _fixed.Add(new PuzzleSystem(_expedition));
        _simulation = new SystemGroup(_world);
        _simulation.Add(new PartyControlSystem(_expedition));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Run_OnlyTheActiveHeroMoves()
    {
        var gormStart = HeroX(ActorKind.Gorm);
        var ylvaStart = HeroX(ActorKind.Ylva);

        Play(new PartyInput { Horizontal = 1 }, frames: 60);

        Assert.That(HeroX(ActorKind.Ylva), Is.GreaterThan(ylvaStart + 2f));
        Assert.That(HeroX(ActorKind.Gorm), Is.EqualTo(gormStart).Within(.001f));
    }

    [Test]
    public void NextHero_Tab_HandsControlToTheNextHero()
    {
        Play(new PartyInput { NextHero = true }, frames: 1);
        var gormStart = HeroX(ActorKind.Gorm);

        Play(new PartyInput { Horizontal = 1 }, frames: 60);

        Assert.That(_expedition.ActiveHero, Is.EqualTo(ActorKind.Gorm));
        Assert.That(HeroX(ActorKind.Gorm), Is.GreaterThan(gormStart + 1f));
    }

    [Test]
    public void SelectHero_Key3_ControlsSigrid()
    {
        Play(new PartyInput { SelectHero = 3 }, frames: 1);

        Assert.That(_expedition.ActiveHero, Is.EqualTo(ActorKind.Sigrid));
    }

    [Test]
    public void Run_AfterTheAttemptFailed_NobodyMoves()
    {
        _expedition.Apply(StepResult.Failed(ActorKind.Gorm, FailCause.Spikes));
        var ylvaStart = HeroX(ActorKind.Ylva);

        Play(new PartyInput { Horizontal = 1 }, frames: 60);

        Assert.That(HeroX(ActorKind.Ylva), Is.EqualTo(ylvaStart));
        Assert.That(_expedition.Outcome, Is.EqualTo(Outcome.Failed));
    }

    /// <summary>One fixed step per frame, then the input, like <c>EcsScene</c> runs them.</summary>
    private void Play(PartyInput input, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            _fixed.Update(FrameSeconds);
            _expedition.Input = input;
            _simulation.Update(FrameSeconds);
        }
    }

    private float HeroX(ActorKind kind)
    {
        foreach (var chunk in _world.Query(Query.With<Actor>()).GetChunks())
        {
            var actors = chunk.GetSpan<Actor>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (actors[i].Kind == kind)
                    return actors[i].Min.X;
            }
        }

        return float.NaN;
    }
}
