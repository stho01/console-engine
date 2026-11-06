using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Components;

/// <summary>World position in cells. Drawn at <c>Value - Camera.Position</c>.</summary>
public struct Position(Vector2 value)
{
    public Vector2 Value = value;
}
