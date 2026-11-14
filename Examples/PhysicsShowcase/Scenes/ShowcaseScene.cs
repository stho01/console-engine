using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;
using PhysicsShowcase.Systems;

namespace PhysicsShowcase.Scenes;

/// <summary>
/// The showcase. Physics, the moving platform, the player and the event counter run in the fixed step (60 Hz); input,
/// dropping, the blast and the switches every frame; drawing (with interpolation first) in presentation.
/// </summary>
public sealed class ShowcaseScene : EcsScene<PhysicsShowcaseGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Cells/s².</summary>
    private const float Gravity = 40f;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var width = Console.Width;
        var height = Console.Height;
        var random = new Random();
        var outlines = new OutlineLibrary();
        var state = new ShowcaseState();

        var tiles = Level.CreateTiles(width, height);
        var physics = new PhysicsSystem(cellSize: 3f) { Gravity = new Vector2(0f, Gravity), TileGrid = tiles };
        state.Zone = Level.CreateProps(World, outlines, width, height);
        CreatePlayer(new Vector2(5f, height - 3.5f - CharacterMover.SkinWidth));

        var factory = new BodyFactory(physics, outlines, random);
        var interpolation = new InterpolationSystem(FixedTimestep);

        FixedSimulation.Add(physics);
        FixedSimulation.Add(new PatrolSystem());
        FixedSimulation.Add(new EventCounterSystem(physics, state));
        FixedSimulation.Add(new PlayerMoveSystem(physics));

        Simulation.Add(new PlayerInputSystem());
        Simulation.Add(new DropSystem(factory, random, minX: 4f, maxX: width - 4f, dropY: Level.Top + 3f));
        Simulation.Add(new BlastSystem(physics, random));
        Simulation.Add(new SwitchSystem(physics, interpolation, state));

        Presentation.Add(interpolation);
        Presentation.Add(new TintSystem());
        Presentation.Add(new TileRenderSystem(Console, tiles));
        Presentation.Add(new OutlineRenderSystem(Console, Camera, outlines));
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
        Presentation.Add(new LaserSystem(Console, physics));
        Presentation.Add(new HudSystem(Console, physics, state));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The player is a static obstacle for the bodies (a collider, no rigid body); it moves itself.</summary>
    private void CreatePlayer(Vector2 center) =>
        World.Create(
            new Position(center),
            Collider.Box(new Vector2(.5f, .5f)) with { Category = Layers.Solid },
            new Glyph('@', ConsoleColor.White),
            new Player { Facing = 1f });
}
