using ConsoleEngine.Ecs;

namespace Doom.Components;

/// <summary>A projectile. It never hits <see cref="Owner"/>, and its kills count as the owner's.</summary>
public struct Missile
{
    public Entity Owner;
}
