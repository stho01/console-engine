using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using Starguard.Assets;
using Starguard.Systems;

namespace Starguard.Scenes;

/// <summary>
/// One match: the cannon, four shields and the first wave as entities, the game's systems from <c>Systems/</c> and the
/// engine's movement and render systems. The order of <c>Add</c> calls is the order they run.
/// </summary>
public sealed class BattleScene : EcsScene<StarguardGame>
{
    protected override void OnCreate()
    {
        var playfield = new Playfield(Console.Width, Console.Height);
        var random = new Random();
        var sprites = new SpriteSheet(new SpriteLibrary());
        var spawner = new Spawner(sprites, playfield);
        var match = new Match();
        var formation = new Formation();
        var controls = new Controls();

        // The spawner records into a command buffer. Play it back now, so the first frame already sees the aliens.
        spawner.CreateCannon(Simulation.Commands);
        spawner.CreateShields(Simulation.Commands);
        formation.Reset(spawner.CreateFormation(Simulation.Commands, match.Wave));
        Simulation.Commands.Playback(World);

        Simulation.Add(new ControlsSystem(controls));
        Simulation.Add(new RestartSystem(Game, match, controls));
        Simulation.Add(new CannonSystem(match, controls, spawner, playfield));
        Simulation.Add(new FormationSystem(match, formation, playfield));
        Simulation.Add(new AlienFireSystem(match, random));
        Simulation.Add(new SaucerSystem(match, spawner, random));
        Simulation.Add(new MovementSystem());
        Simulation.Add(new CollisionSystem(match, spawner));
        Simulation.Add(new BoundsSystem(playfield));
        Simulation.Add(new LifetimeSystem());
        Simulation.Add(new WaveSystem(match, formation, spawner));

        // Sprites first, so shots and shield glyphs are drawn on top of them.
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites.Library));
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
        Presentation.Add(new HudSystem(Game, match, playfield));
    }
}
