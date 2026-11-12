using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Components;

/// <summary>
/// Draws the entity this far from its <see cref="Position"/> and <see cref="Rotation"/>, without moving it. The engine's
/// render systems add it when the entity has one. Used for smoothing (the physics package's interpolation fills it so
/// bodies glide between fixed steps) and for effects such as screen shake.
/// </summary>
public struct RenderOffset
{
    public Vector2 Position;
    public float Radians;
}
