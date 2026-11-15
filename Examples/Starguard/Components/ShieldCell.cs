namespace Starguard.Components;

/// <summary>One cell of a shield. Each hit costs one point of health and shows a thinner block. At 0 it's gone.</summary>
public struct ShieldCell(byte health)
{
    public byte Health = health;
}
