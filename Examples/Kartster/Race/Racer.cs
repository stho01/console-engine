namespace Kartster.Race;

/// <summary>
/// One kart in a <see cref="RaceSession"/>: Kartster's <c>RaceProgressState</c> (gates passed) and
/// <c>RaceParticipant</c> (laps, times, finish) in one. Read it with <see cref="RaceSession.Get"/>.
/// </summary>
public struct Racer
{
    /// <summary>The id the kart was registered with.</summary>
    public int Id;

    /// <summary>A person rather than a bot: with one in the race, it ends only when every racer has finished.</summary>
    public bool IsHuman;

    /// <summary>Has driven through the start line (and not back).</summary>
    public bool LapStarted;

    /// <summary>Laps driven, as the gates say: goes down again when the kart backs over the line.</summary>
    public int CompletedLaps;

    /// <summary>The checkpoint to pass next, 0 to the checkpoint count (then the line is next).</summary>
    public int NextCheckpointIndex;

    /// <summary>Times the kart passed the gate after next with the next still owed. Only goes up.</summary>
    public int MissedCheckpoints;

    /// <summary>The lap the kart is on, from 1; the lap count once finished.</summary>
    public int CurrentLap;

    public float LastLapSeconds;

    /// <summary>0 until a lap is done.</summary>
    public float BestLapSeconds;

    public bool HasFinished;
    public float FinishTimeSeconds;

    /// <summary>1 for the winner; 0 until finished.</summary>
    public int FinishPosition;

    /// <summary>Race position, 1 for the leader, as of the last <see cref="RaceSession.UpdatePositions"/>.</summary>
    public int Position;

    /// <summary>Straight-line metres to the next gate's point, for ranking karts level on gates.</summary>
    public float DistanceToNextGate;

    /// <summary>Highest lap count seen, so a lap taken back and driven again counts once.</summary>
    internal int ReportedLaps;

    /// <summary>Race time at which the current lap began.</summary>
    internal float LapStartSeconds;
}
