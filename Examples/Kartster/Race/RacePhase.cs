namespace Kartster.Race;

/// <summary>Where a race is, as Kartster's <c>RacePhase</c>.</summary>
public enum RacePhase
{
    /// <summary>On the grid, nobody may drive, the clock has not started.</summary>
    Countdown,

    /// <summary>The clock runs and laps count.</summary>
    Racing,

    /// <summary>Over: the clock is frozen and nothing counts any more.</summary>
    Finished,
}
