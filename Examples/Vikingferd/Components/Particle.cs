using System;
using Microsoft.Xna.Framework;

namespace Vikingferd.Components;

/// <summary>One pixel of an effect (debris, dust, sparks). Positions in tiles, like <see cref="Actor"/>.</summary>
public struct Particle
{
    public Vector2 Position;

    /// <summary>Tiles per second.</summary>
    public Vector2 Velocity;

    /// <summary>Tiles/s² downwards; negative rises (dust).</summary>
    public float Gravity;

    /// <summary>Seconds left. The particle is destroyed at 0.</summary>
    public float Life;

    public ConsoleColor Color;

    /// <summary>Drawn in this colour for the last third of its life (fading out).</summary>
    public ConsoleColor FadeColor;

    public float MaxLife;
}
