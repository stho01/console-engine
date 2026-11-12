using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Levels;
using Topple.Tests.Fakes;

namespace Topple.Tests;

/// <summary>
/// A level as <c>LevelScene</c> sets it up, without a game or keyboard: the fixed-step systems at 60 Hz, the effect
/// systems, and (for <see cref="Render"/>) the drawing into a <see cref="RenderConsole"/> over a fake console.
/// </summary>
internal sealed class LevelSimulation : IDisposable
{
    public const float Step = 1f / 60f;

    private readonly SystemGroup _fixed;
    private readonly SystemGroup _effects;
    private readonly SystemGroup _presentation;

    public LevelSimulation(LevelPlan plan, Campaign campaign = null)
    {
        World = new World();
        Setup = new LevelSetup(World, plan, ToppleGame.ScreenWidth, ToppleGame.ScreenHeight, seed: 1);
        _fixed = new SystemGroup(World);
        Setup.AddFixedSystems(_fixed);
        _effects = new SystemGroup(World);
        Setup.AddEffectSystems(_effects);

        Console = new RenderConsole(new FakeConsoleHandler(ToppleGame.ScreenWidth, ToppleGame.ScreenHeight));
        _presentation = new SystemGroup(World);
        Setup.AddPresentationSystems(_presentation, Console, new FixedTimestep(),
            campaign ?? new Campaign(LevelBook.Levels.Length));
    }

    public World World { get; }
    public LevelSetup Setup { get; }
    public RenderConsole Console { get; }
    public PhysicsSystem Physics => Setup.Physics;
    public Round Round => Setup.Round;

    public int Count<T>() where T : unmanaged => World.Query(Query.With<T>()).Count;

    /// <summary>Runs the fixed step and the effects, one 60 Hz frame at a time.</summary>
    public void Run(float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            _fixed.Update(Step);
            _effects.Update(Step);
        }
    }

    /// <summary>Draws one frame into <see cref="Console"/>.</summary>
    public void Render()
    {
        Console.Clear();
        _presentation.Update(Step);
    }

    /// <summary>Adds a piece after the level started, for example once the damage grace time is over.</summary>
    public void Place(Piece piece) => Setup.Factory.CreatePiece(World, piece);

    public void Throw(ShotKind kind, Vector2 position, Vector2 velocity)
    {
        var commands = new CommandBuffer();
        BodyFactory.CreateShot(commands, kind, position, velocity);
        commands.Playback(World);
    }

    public void Dispose() => World.Dispose();
}
