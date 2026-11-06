namespace Asteroids.Ecs.Components;

/// <summary>The entity is destroyed when this reaches 0.</summary>
public struct Lifetime(float seconds)
{
    public float SecondsLeft = seconds;
}
