using System;

namespace PhysicsSandbox.Components;

/// <summary>Draws the entity's box collider as a filled rectangle of <see cref="Character"/>.</summary>
public struct Block(char character, ConsoleColor color)
{
    public char Character = character;
    public ConsoleColor Color = color;
}
