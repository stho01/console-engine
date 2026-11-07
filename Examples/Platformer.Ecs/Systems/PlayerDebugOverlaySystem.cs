using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Rendering;
using Platformer.Ecs.Components;

namespace Platformer.Ecs.Systems;

/// <summary>
/// In debug mode, prints the player's position, velocity, airborne flag and what its last move hit in the top-left
/// corner (the first three lines as in <c>Examples/Platformer</c>). Runs first in the presentation group, so sprites
/// and tiles drawn later cover it. The strings are built every frame, which is acceptable for a debug overlay.
/// </summary>
public sealed class PlayerDebugOverlaySystem(PlatformerEcsGame game, RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _players;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _players = World.Query(
        Query.With<PlayerBody, Position, Velocity>().With<CollisionProbe>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (!game.IsDebugMode)
            return;

        foreach (var chunk in _players.GetChunks())
        {
            if (chunk.Count == 0)
                continue;

            var body = chunk.GetSpan<PlayerBody>()[0];
            var probe = chunk.GetSpan<CollisionProbe>()[0];

            console.Draw(0, 0, $"POS: {chunk.GetSpan<Position>()[0].Value}");
            console.Draw(0, 1, $"VEL: {chunk.GetSpan<Velocity>()[0].Value}");
            console.Draw(0, 2, $"AIR: {body.IsAirborne}");
            console.Draw(0, 3, $"HIT: {probe.Collisions}");
            return;
        }
    }
}
