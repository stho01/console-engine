using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Components;

/// <summary>Clockwise rotation in radians (screen y points down). 0 faces up.</summary>
public struct Rotation(float radians)
{
    public float Radians = radians;

    /// <summary>Unit vector the entity faces.</summary>
    public readonly Vector2 Heading => new(MathF.Sin(Radians), -MathF.Cos(Radians));
}
