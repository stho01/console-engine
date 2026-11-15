namespace Kartster.Modes;

/// <summary>What the race on the select-track menu is: against the bots, or alone against the clock and a ghost.</summary>
public enum RaceMode
{
    /// <summary>The player and <c>TrackScene.DefaultBotCount</c> bots.</summary>
    Race,

    /// <summary>The player alone, with the best lap so far as a ghost (<see cref="TimeTrial"/>).</summary>
    TimeTrial
}
