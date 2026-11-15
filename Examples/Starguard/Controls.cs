namespace Starguard;

/// <summary>
/// What the player asks for this frame. <see cref="Systems.ControlsSystem"/> fills it from the keyboard, every other
/// system reads it, so they can be tested without a console.
/// </summary>
public sealed class Controls
{
    /// <summary>-1 left, 0 stay, +1 right.</summary>
    public int Move { get; set; }

    public bool Fire { get; set; }

    /// <summary>Start a new match after game over.</summary>
    public bool Restart { get; set; }
}
