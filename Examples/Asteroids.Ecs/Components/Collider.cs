namespace Asteroids.Ecs.Components;

/// <summary>Hit circle around the entity's <c>Position</c>, in cells.</summary>
public struct Collider(float radius)
{
    public float Radius = radius;
}
