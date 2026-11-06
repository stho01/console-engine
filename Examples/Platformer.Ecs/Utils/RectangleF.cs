namespace Platformer.Ecs.Utils;

/// <summary>An axis-aligned box in world cells. <see cref="ToString"/> matches <c>Examples/Platformer</c>.</summary>
public readonly struct RectangleF(float x, float y, float width, float height)
{
    public float Left { get; } = x;
    public float Top { get; } = y;
    public float Right { get; } = x + width;
    public float Bottom { get; } = y + height;

    public override string ToString() =>
        $"{{ Left = {Left:0.000}, Right = {Right:0.000}, Top = {Top:0.000}, Bottom = {Bottom:0.000} }}";
}
