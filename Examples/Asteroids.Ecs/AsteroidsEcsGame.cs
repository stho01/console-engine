using Asteroids.Ecs.Scenes;
using ConsoleEngine;

namespace Asteroids.Ecs;

/// <summary>
/// The Asteroids example rebuilt on the ECS: state lives in a world as components, behaviour lives in systems that run
/// over queries. Everything is set up in <see cref="AsteroidsScene"/>. Compare with <c>Examples/Asteroids</c> (one
/// class per game object).
/// </summary>
public class AsteroidsEcsGame(int asteroidCount) : GameBase(200, 200, 4, 4)
{
    protected override void OnInitialize() => Scenes.Set(new AsteroidsScene(asteroidCount));

    protected override void OnUpdate() { }

    protected override void OnRender() { }
}
