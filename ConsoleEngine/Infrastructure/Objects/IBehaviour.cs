using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Infrastructure.Objects;

/// <summary>
/// What a <see cref="GameObject"/> does: written in C#, or a Lua script (<c>ConsoleEngine.Scripting.LuaBehaviour</c>).
/// One instance belongs to one object.
/// </summary>
public interface IBehaviour
{
    /// <summary>Called once, before the first <see cref="Update"/>.</summary>
    void Start(GameObject owner);

    void Update(GameObject owner, TimeSpan delta);

    /// <summary>Extra drawing after the object itself is drawn (HUD text, effects). Most behaviours do nothing.</summary>
    void Render(GameObject owner, RenderConsole console);
}
