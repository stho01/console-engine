using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Components;

/// <summary>Cells per second. Applied to <see cref="Position"/> by <see cref="Systems.MovementSystem"/>.</summary>
public struct Velocity(Vector2 value)
{
    public Vector2 Value = value;
}
