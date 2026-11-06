using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Platformer.Ecs.Components;

namespace Platformer.Ecs.Systems;

/// <summary>Puts the (first) player's position in the centre of the screen. Runs after the player moved.</summary>
public sealed class CameraFollowSystem(Camera camera, RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _players;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _players = World.Query(Query.With<PlayerBody, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _players.GetChunks())
        {
            if (chunk.Count == 0)
                continue;

            camera.Position = chunk.GetSpan<Position>()[0].Value - console.ScreenCenter;
            return;
        }
    }
}
