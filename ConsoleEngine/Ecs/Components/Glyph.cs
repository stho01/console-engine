using System;

namespace ConsoleEngine.Ecs.Components;

/// <summary>
/// One character drawn at <see cref="Position"/> by <see cref="Systems.GlyphRenderSystem"/>. 4 bytes: colours are
/// stored as bytes (a <see cref="ConsoleColor"/> is 4 bytes), so 16 glyphs fit in a cache line.
/// </summary>
public struct Glyph(char character, ConsoleColor foreground, ConsoleColor background = ConsoleColor.Black)
{
    public char Character = character;
    private byte _foreground = (byte)foreground;
    private byte _background = (byte)background;

    public ConsoleColor Foreground
    {
        readonly get => (ConsoleColor)_foreground;
        set => _foreground = (byte)value;
    }

    public ConsoleColor Background
    {
        readonly get => (ConsoleColor)_background;
        set => _background = (byte)value;
    }
}
