using ConsoleEngine.Ecs;

namespace ConsoleEngine.Scripting;

/// <summary>
/// Calls <c>on_render(self)</c> for every script <paramref name="scripts"/> runs, for drawing beyond the entity's
/// <c>Glyph</c> (HUD text, effects). Add it to the presentation group, after the render systems it should draw over.
/// Changes to <c>self</c>'s fields are not copied back.
/// </summary>
public sealed class LuaRenderSystem(LuaScriptSystem scripts) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds) => scripts.Render(deltaSeconds, Commands);
}
