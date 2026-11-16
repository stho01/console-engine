namespace Doom.Play;

/// <summary>What a thing is doing; picks the system that runs it each tic.</summary>
public enum ThingState
{
    /// <summary>Decorations and pickups: loops its idle frames.</summary>
    Static,

    /// <summary>A monster waiting to see or hear the player.</summary>
    Idle,

    Chase,
    Attack,
    Pain,

    /// <summary>A lost soul flying at its target.</summary>
    Charge,

    /// <summary>A projectile in flight.</summary>
    Flying,

    /// <summary>Playing its death or explosion frames.</summary>
    Dying,

    /// <summary>A corpse showing its last death frame.</summary>
    Dead
}
