using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>Last in the presentation: writes the finished canvas into the console's frame buffer.</summary>
public sealed class CanvasFlushSystem(Canvas canvas, RenderConsole console) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds) => canvas.Flush(console);
}
