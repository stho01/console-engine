using ConsoleEngine.Ecs;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>First in the presentation: animates the night sky and paints it over the whole canvas.</summary>
public sealed class BackdropRenderSystem(Canvas canvas, Backdrop backdrop) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        backdrop.Update(deltaSeconds);
        backdrop.Draw(canvas);
    }
}
