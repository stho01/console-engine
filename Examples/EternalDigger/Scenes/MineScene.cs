using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;

namespace EternalDigger.Scenes;

/// <summary>
/// The whole game: the <see cref="Mine"/> grid, one miner entity, effect particles, and the systems from
/// <c>Systems/</c>. The order of <c>Add</c> calls is the order they run. Terrain is not made of entities: the mine is
/// a grid that is drawn from the camera's rows, so an endless mine costs nothing per frame.
/// </summary>
public sealed class MineScene(int seed) : EcsScene<EternalDiggerGame>
{
    protected override void OnCreate()
    {
        var mine = new Mine(seed);
        var random = new Random(seed);
        var effects = new EffectSpawner(random, new ScreenShake());
        var light = new LightMap(Mine.Columns, Layout.ScreenHeight / Layout.TileHeight + 2);
        var canvas = new Canvas(Console);

        CreateMiner();

        Simulation.Add(new MinerControlSystem(mine, effects));
        Simulation.Add(new InteractionSystem(effects));
        Simulation.Add(new MinerMotionSystem(mine, effects));
        Simulation.Add(new CameraSystem(Camera, Console, effects.Shake, random));
        Simulation.Add(new LightingSystem(mine, light, Camera));
        Simulation.Add(new GlintSystem(mine, light, Camera, effects, random));
        Simulation.Add(new MistSystem(mine, light, Camera, effects, random));
        Simulation.Add(new ParticlePhysicsSystem(mine));
        Simulation.Add(new ParticleLifetimeSystem());
        Simulation.Add(new FloatingNumberSystem());

        Presentation.Add(new SkyRenderSystem(canvas, Camera));
        Presentation.Add(new TerrainRenderSystem(canvas, Camera, mine, light));
        Presentation.Add(new HouseRenderSystem(canvas, Camera));
        Presentation.Add(new ParticleRenderSystem(canvas, Camera, light));
        // After the particles, so debris, dust and sparks never cover the miner.
        Presentation.Add(new MinerRenderSystem(canvas, Camera));
        Presentation.Add(new FloatingNumberRenderSystem(canvas, Camera));
        Presentation.Add(new PromptRenderSystem(canvas, Camera));
        Presentation.Add(new HudSystem(canvas));
    }

    private void CreateMiner()
    {
        var spawn = new Vector2(Layout.SpawnColumn, Layout.GroundRow);
        World.Create(
            new Position(Layout.TileToWorld(Layout.SpawnColumn, Layout.GroundRow)),
            new Miner
            {
                Column = Layout.SpawnColumn,
                Row = Layout.GroundRow,
                MoveFrom = spawn,
                FacingX = 1,
                LampDirection = Vector2.UnitX,
                Gold = Shop.StartGold,
                Ladders = Shop.StartLadders
            });
    }
}
