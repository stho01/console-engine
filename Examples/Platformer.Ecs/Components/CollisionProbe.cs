using ConsoleEngine.Physics;

namespace Platformer.Ecs.Components;

/// <summary>What the last move of a <see cref="PlayerBody"/> ran into. Only read by the debug overlay.</summary>
public struct CollisionProbe
{
    public SlideCollisions Collisions;
}
