using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Topple.Effects;
using Topple.Rendering;
using Topple.Scenes;
using Topple.Systems;

namespace Topple.Levels;

/// <summary>
/// Everything one attempt at a level is made of, created from its <see cref="LevelPlan"/>, and the systems that run it.
/// <see cref="LevelScene"/> adds them all plus the input systems; the tests use the parts that need no keyboard.
/// </summary>
public sealed class LevelSetup
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Cells/s².</summary>
    public const float Gravity = 30f;

    public const float PhysicsCellSize = 4f;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <summary>Builds the level's tiles and bodies in <paramref name="world"/>.</summary>
    public LevelSetup(World world, LevelPlan plan, int width, int height, int seed)
    {
        Plan = plan;
        Random = new Random(seed);
        Tiles = LevelBuilder.CreateTiles(plan, width, height);
        Physics = new PhysicsSystem(PhysicsCellSize) { Gravity = new Vector2(0f, Gravity), TileGrid = Tiles };
        Shapes = new ShapeLibrary();
        Factory = new BodyFactory(Physics, Shapes);
        LevelBuilder.Spawn(world, Factory, plan);

        Backdrop = new Backdrop(width, height, Tiles, plan, seed);
        Round = new Round(plan.Stones, plan.Bombs);
        Catapult = new Catapult(LevelBuilder.CatapultPivot);
        Shake = new ScreenShake(Random);
        Effects = new EffectSpawner(Random, Shake);
        Wrecker = new Wrecker(Round, Effects);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public LevelPlan Plan { get; }
    public Random Random { get; }
    public TileGrid Tiles { get; }
    public PhysicsSystem Physics { get; }
    public ShapeLibrary Shapes { get; }
    public BodyFactory Factory { get; }
    public Backdrop Backdrop { get; }
    public Round Round { get; }
    public Catapult Catapult { get; }
    public ScreenShake Shake { get; }
    public EffectSpawner Effects { get; }
    public Wrecker Wrecker { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Physics, damage, shots and water, in the fixed step.</summary>
    public void AddFixedSystems(SystemGroup group) => FixedStep.AddSystems(group, Physics, Wrecker, Effects);

    /// <summary>The frame-time systems that need no input: effects and debris.</summary>
    public void AddEffectSystems(SystemGroup group)
    {
        group.Add(new TrailSystem(Effects));
        group.Add(new DebrisSystem());
        group.Add(new PuffSystem());
        group.Add(new BurstSystem());
    }

    /// <summary>Drawing, back to front.</summary>
    public void AddPresentationSystems(SystemGroup group, RenderConsole console, FixedTimestep timestep,
        Campaign campaign)
    {
        var canvas = new Canvas(console);
        group.Add(new InterpolationSystem(timestep));
        group.Add(new ShakeSystem(canvas, Shake));
        group.Add(new SkySystem(canvas, Backdrop));
        group.Add(new WaterRenderSystem(canvas, Plan));
        group.Add(new CatapultRenderSystem(canvas, Catapult, Physics, Round));
        group.Add(new BlockRenderSystem(canvas, Shapes));
        group.Add(new CrystalRenderSystem(canvas));
        group.Add(new BurstRenderSystem(canvas));
        group.Add(new PuffRenderSystem(canvas));
        group.Add(new GlyphOverRenderSystem(canvas));
        group.Add(new HudSystem(canvas, Plan, campaign, Round, Catapult, Physics));
        group.Add(new BannerSystem(canvas, campaign, Round));
    }
}
