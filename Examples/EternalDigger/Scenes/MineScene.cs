using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Saving;
using EternalDigger.Systems;

namespace EternalDigger.Scenes;

/// <summary>
/// The whole game: the <see cref="Mine"/> grid, one miner entity, effect particles, and the systems from
/// <c>Systems/</c>. The order of <c>Add</c> calls is the order they run. Terrain is not made of entities: the mine is
/// a grid that is drawn from the camera's rows, so an endless mine costs nothing per frame. Starts from
/// <paramref name="start"/> (a loaded save, or <see cref="SaveData.NewMine"/>) and saves to <paramref name="savePath"/>.
/// </summary>
public sealed class MineScene(SaveData start, string savePath) : EcsScene<EternalDiggerGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private GameSaver _saver;
    private Query _miners;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Saves right now (Esc). Does nothing before the scene is created.</summary>
    public void Save()
    {
        if (_saver == null)
            return;

        foreach (var chunk in _miners.GetChunks())
        {
            _saver.Save(chunk.GetSpan<Miner>()[0]);
            return;
        }
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var mine = new Mine(start.Seed);
        var elevator = new Elevator();
        var counter = new ShopCounter();
        var campCounter = new ShopCounter(ShopCounter.CampItems);
        var toolCounter = new ShopCounter(ShopCounter.ToolShopItems);
        start.ApplyTo(mine, elevator);
        var random = new Random(start.Seed);
        var effects = new EffectSpawner(random, new ScreenShake());
        var light = new LightMap(Mine.Columns, Layout.ScreenHeight / Layout.TileHeight + 2);
        var canvas = new Canvas(Console);
        var headlamp = new Headlamp();
        var gas = new GasPockets(mine);
        _saver = new GameSaver(savePath, start.Seed, mine, elevator);
        _miners = World.Query(Query.With<Miner>());

        var miner = start.ToMiner();
        World.Create(new Position(Layout.TileToWorld(miner.Column, miner.Row)), miner);

        Simulation.Add(new MinerControlSystem(mine, elevator, effects));
        Simulation.Add(new InteractionSystem(mine, elevator, counter, effects, _saver, campCounter,
            toolCounter));
        Simulation.Add(new MinerMotionSystem(mine, effects));
        Simulation.Add(new ElevatorSystem(elevator));
        Simulation.Add(new DynamiteSystem(mine, effects, gas));
        Simulation.Add(new GasSystem(mine, gas, effects, random));
        Simulation.Add(new CameraSystem(Camera, Console, effects.Shake, random));
        Simulation.Add(new LampSystem(headlamp, effects, random, mine));
        Simulation.Add(new LightingSystem(mine, light, Camera, headlamp));
        Simulation.Add(new GlintSystem(mine, light, Camera, effects, random));
        Simulation.Add(new MistSystem(mine, light, Camera, effects, random));
        Simulation.Add(new ParticlePhysicsSystem(mine));
        Simulation.Add(new ParticleLifetimeSystem());
        Simulation.Add(new FloatingTextSystem());
        Simulation.Add(new SaveSystem(_saver));

        Presentation.Add(new SkyRenderSystem(canvas, Camera));
        Presentation.Add(new TerrainRenderSystem(canvas, Camera, mine, light));
        Presentation.Add(new HouseRenderSystem(canvas, Camera));
        Presentation.Add(new ElevatorRenderSystem(canvas, Camera, elevator));
        Presentation.Add(new CampRenderSystem(canvas, Camera));
        Presentation.Add(new DynamiteRenderSystem(canvas, Camera));
        Presentation.Add(new ParticleRenderSystem(canvas, Camera, light));
        // After the particles, so debris, dust and sparks never cover the miner.
        Presentation.Add(new MinerRenderSystem(canvas, Camera, headlamp));
        Presentation.Add(new FloatingTextRenderSystem(canvas, Camera));
        Presentation.Add(new PromptRenderSystem(canvas, Camera, mine, elevator, counter, campCounter,
            toolCounter));
        Presentation.Add(new HudSystem(canvas, _saver, elevator));
        // Last, over everything: TAB pauses the simulation and shows the inventory.
        Presentation.Add(new InventorySystem(canvas, elevator, Simulation));
    }
}
