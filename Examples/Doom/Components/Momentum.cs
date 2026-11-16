namespace Doom.Components;

/// <summary>Speed in world units per tic. Things that move by themselves have it: the player, monsters, missiles.</summary>
public struct Momentum
{
    public float X;
    public float Y;
    public float Z;
}
