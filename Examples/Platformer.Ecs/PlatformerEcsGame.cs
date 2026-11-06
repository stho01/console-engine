using ConsoleEngine;
using Platformer.Ecs.Scenes;

namespace Platformer.Ecs;

/// <summary>
/// The Platformer example rebuilt on the ECS: state lives in a world as components, behaviour lives in systems that
/// run over queries. Everything is set up in <see cref="PlatformerScene"/>. Compare with <c>Examples/Platformer</c>
/// (one class per game object).
/// </summary>
public class PlatformerEcsGame() : GameBase(70, 35, 10, 10)
{
    /// <summary>Shows player state, collision probes and tile coordinates. F1 toggles it.</summary>
    public bool IsDebugMode { get; set; }

    protected override void OnInitialize() => Scenes.Set(new PlatformerScene());

    protected override void OnUpdate() { }

    protected override void OnRender() { }
}
