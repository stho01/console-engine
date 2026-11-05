using Microsoft.Xna.Framework;

namespace Asteroids.Ecs;

/// <summary>The area entities live in. Anything that leaves one edge comes back on the opposite edge.</summary>
public readonly struct Playfield(Vector2 min, Vector2 size)
{
    public Vector2 Min { get; } = min;
    public Vector2 Size { get; } = size;

    public Vector2 Wrap(Vector2 position)
    {
        if (position.X < Min.X) position.X += Size.X;
        else if (position.X >= Min.X + Size.X) position.X -= Size.X;

        if (position.Y < Min.Y) position.Y += Size.Y;
        else if (position.Y >= Min.Y + Size.Y) position.Y -= Size.Y;

        return position;
    }
}
