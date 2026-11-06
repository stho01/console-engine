using System;
using ConsoleEngine.Ecs.Rendering;

namespace ConsoleEngine.Ecs.Components;

/// <summary>
/// A closed polygon drawn around <see cref="Position"/> by <see cref="Systems.OutlineRenderSystem"/>, rotated by
/// <see cref="Rotation"/> if the entity has one. Components can't hold arrays, so the vertices live in an
/// <see cref="OutlineLibrary"/> and this stores their id.
/// </summary>
public struct Outline(int id, char character, ConsoleColor color)
{
    public int Id = id;
    public char Character = character;
    private byte _color = (byte)color;

    public ConsoleColor Color
    {
        readonly get => (ConsoleColor)_color;
        set => _color = (byte)value;
    }
}
