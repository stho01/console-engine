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
        return $"{{ {nameof(Left)} = {Left}, {nameof(Right)} = {Right}, {nameof(Top)} = {Top}, {nameof(Bottom)} = {Bottom} }}";
    }
}