using Asteroids.Ecs.Components;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace Asteroids.Ecs.Systems;

/// <summary>Centres the camera on the (first) ship.</summary>
public sealed class CameraSystem(Camera camera, RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _ships;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _ships = World.Query(Query.With<Ship, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _ships.GetChunks())
        {
            camera.Position = chunk.GetSpan<Position>()[0].Value - console.ScreenCenter;
            return;
        }
    }
}
