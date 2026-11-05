using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Systems;
using Swarm.Systems;

namespace Swarm.Scenes;

/// <summary>
/// The whole stress test. The attractor, movement and bounce systems are added out of order on purpose: their
/// <c>[UpdateBefore]</c>/<c>[UpdateAfter]</c> attributes sort them to attractor → movement → bounce.
/// </summary>
public sealed class SwarmScene(int particleCount) : EcsScene<SwarmGame>
{
    protected override void OnCreate()
    {
        var spawner = new ParticleSpawner(Console.Width, Console.Height);
        spawner.Create(World, particleCount);

        Simulation.Add(new SpawnControlSystem(spawner));
        Simulation.Add(new BounceSystem(Console.Width, Console.Height));
        Simulation.Add(new MovementSystem());
        Simulation.Add(new AttractorSystem(Console.Width, Console.Height));
        Simulation.Add(new ColorBySpeedSystem());

        Presentation.Add(new GlyphRenderSystem(Console, Camera));
        Presentation.Add(new HudSystem(Console));
    }
}
