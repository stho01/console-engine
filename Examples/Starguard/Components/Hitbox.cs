namespace Starguard.Components;

/// <summary>The cells an entity occupies, counted from its <c>Position</c> (top-left) to the right and down.</summary>
public struct Hitbox(int width, int height)
{
    public int Width = width;
    public int Height = height;
}
