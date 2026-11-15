using Microsoft.Xna.Framework;

namespace Kartster.Race;

/// <summary>
/// A kart's race, mirrored from its <see cref="Racer"/> in the <see cref="RaceSession"/> by the
/// <see cref="RaceSystem"/> every fixed step, for the HUD and the bots to read. Add it (default) to every kart in the
/// race.
/// </summary>
public struct RaceProgress
{
    /// <summary>Where the kart was at the last race step (physics frame); the next step's gate test starts here.</summary>
    public Vector2 LastPosition;

    /// <summary>False until the first race step, and after <see cref="Forget"/>.</summary>
    public bool HasLastPosition;

    /// <summary>The lap the kart is on, from 1.</summary>
    public int Lap;

    public int CompletedLaps;
    public bool LapStarted;

    /// <summary>The checkpoint to pass next; the checkpoint count when the line is next.</summary>
    public int NextCheckpointIndex;

    public int MissedCheckpoints;

    /// <summary>1 for the leader.</summary>
    public int Position;

    public float LastLapSeconds;
    public float BestLapSeconds;
    public bool Finished;
    public float FinishTimeSeconds;
    public int FinishPosition;

    /// <summary>
    /// Call after putting the kart somewhere by hand (back on the track, on the grid): the jump is not driving and
    /// crosses no gate. Kartster's <c>RaceProgressTracker.Forget</c>.
    /// </summary>
    public void Forget() => HasLastPosition = false;
}
