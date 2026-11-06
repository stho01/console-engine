using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace TerraForM.Ecs.Systems;

/// <summary>Keeps the rover in the middle of the playfield, the screen area above the HUD.</summary>
public sealed class CameraFollowSystem(Camera camera, RenderConsole console, Level level) : EntitySystem
{
    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds) =>
        camera.Position = Target(World.Get<Position>(level.Rover).Value, console);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Camera position that shows <paramref name="roverPosition"/> in the middle of the playfield.</summary>
    public static Vector2 Target(Vector2 roverPosition, RenderConsole console) =>
        roverPosition - new Vector2(console.ScreenCenter.X, HudSystem.Top / 2f);
}
