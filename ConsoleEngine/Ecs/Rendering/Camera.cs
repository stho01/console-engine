using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Rendering;

/// <summary>The world position shown in the top-left cell of the screen.</summary>
public sealed class Camera
{
    public Vector2 Position { get; set; }

    public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition - Position;
}
