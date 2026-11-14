using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using PhysicsShowcase.Scenes;

namespace PhysicsShowcase;

/// <summary>
/// Every feature of <c>ConsoleEngine.Physics</c> in one scene: a tile level, static, kinematic and dynamic bodies,
/// circles, turning boxes and polygons, friction and bounce, a sensor zone with events, collision layers, a character
/// with <c>MoveAndSlide</c>, a raycast laser, an overlap blast, sleeping and interpolation. Square font cells, so shapes
/// keep their proportions. Everything is set up in <see cref="ShowcaseScene"/>.
/// </summary>
public class PhysicsShowcaseGame() : GameBase(120, 60, fontWidth: 10, fontHeight: 10)
{
    protected override void OnInitialize() => Scenes.Set(new ShowcaseScene());

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
