using System.Text;

namespace Platformer.Utils;

public record RectangleF(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Right => X + Width;
    public float Top = Y;
    public float Bottom = Y + Height;

    public override string ToString()
    {
        return $"{{ {nameof(Left)} = {Left:0.000}, {nameof(Right)} = {Right:0.000}, {nameof(Top)} = {Top:0.000}, {nameof(Bottom)} = {Bottom:0.000} }}";
    }
}