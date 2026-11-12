using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Writes the tower's <see cref="PixelCanvas"/> into the console, two pixels per cell. Runs after everything that
/// paints pixels and before the text drawn over them (messages, HUD).
/// </summary>
public sealed class CanvasPresentSystem(PixelCanvas canvas, RenderConsole console) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds) => canvas.Present(console);
}
