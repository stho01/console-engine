using System;
using ConsoleEngine.Ecs;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;
using Vikingferd.Systems;

namespace Vikingferd.Scenes;

/// <summary>
/// One attempt at a level: the <see cref="Stage"/> (tiles are a grid, not entities), one entity per hero and crate,
/// particle entities for effects, and the systems from <c>Systems/</c>. Input is read every frame; the rules run in
/// a fixed 120 Hz step so jumps are the same height at any frame rate. Everything is drawn into a pixel
/// <see cref="Canvas"/> that is written to the console last. The order of <c>Add</c> calls is the order they run.
/// Restart, next level and quit are handled by <see cref="VikingferdGame"/>, which builds a new scene.
/// </summary>
public sealed class LevelScene(int levelIndex) : EcsScene<VikingferdGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float StepSeconds = 1f / 120f;
    private const int MaxStepsPerFrame = 12;

    //**********************************************************
    //** props
    //**********************************************************

    public int LevelIndex { get; } = levelIndex;

    /// <summary>Null until the scene is loaded.</summary>
    public Expedition Expedition { get; private set; }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var stage = Stage.Parse(Levels.All[LevelIndex].Rows, out var spawns);
        Expedition = new Expedition(LevelIndex, stage);
        FixedTimestep = new FixedTimestep(StepSeconds, MaxStepsPerFrame);
        var canvas = new Canvas(Console.Width, Console.Height);
        var origin = Layout.StagePixelOrigin(stage.Columns, stage.Rows);

        for (var i = 0; i < spawns.Length; i++)
            World.Create(Heroes.Create(spawns[i]));

        FixedSimulation.Add(new PuzzleSystem(Expedition));

        Simulation.Add(new KeyboardSystem(Expedition));
        Simulation.Add(new PartyControlSystem(Expedition));
        Simulation.Add(new EffectSystem(Expedition, new Random(LevelIndex)));
        Simulation.Add(new ParticleSystem(stage));

        Presentation.Add(new BackdropRenderSystem(canvas, new Backdrop(canvas.Width, canvas.PixelHeight)));
        Presentation.Add(new StageRenderSystem(canvas, stage));
        Presentation.Add(new ActorRenderSystem(canvas, Expedition));
        Presentation.Add(new ParticleRenderSystem(canvas, origin));
        Presentation.Add(new HudSystem(canvas, Expedition));
        Presentation.Add(new CanvasFlushSystem(canvas, Console));
    }
}
