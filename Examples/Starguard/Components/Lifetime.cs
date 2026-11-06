namespace Starguard.Components;

/// <summary>The entity is destroyed when this reaches 0. Used for explosions.</summary>
public struct Lifetime(float seconds)
{
    public float SecondsLeft = seconds;
}
