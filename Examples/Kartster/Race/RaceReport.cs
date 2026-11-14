using System;
using Kartster.Ui;

namespace Kartster.Race;

/// <summary>
/// What a <see cref="RaceSession"/> tells the player: the HUD each frame (<see cref="FillHud"/>, allocation free),
/// when the race is over for them (<see cref="Update"/>) and the results (<see cref="Results"/>).
/// Racers are named by their id (<c>Kart.Index</c>) in <paramref name="names"/>.
/// </summary>
/// <param name="names">Driver names by racer id, built once.</param>
/// <param name="playerId">The player's racer id.</param>
public sealed class RaceReport(RaceSession session, string[] names, int playerId)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Standings rows on the HUD: the leaders, plus the player's own row when further back.</summary>
    public const int TopStandings = 3;

    /// <summary>How long "GO!" shows after the countdown.</summary>
    public const float GoSeconds = 1f;

    /// <summary>Kartster's <c>RacingScreen.ResultsDelaySeconds</c>: "FINISHED" stays this long before the results.</summary>
    public const float ResultsDelaySeconds = 4f;

    private readonly HudStanding[] _standings = new HudStanding[TopStandings + 1];
    private float _finishedSeconds;

    //**********************************************************
    //** props
    //**********************************************************

    public RaceSession Session => session;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Everything on the HUD but the power-up (null until wave 3): the player's speed, lap, clock (frozen on their
    /// finish time), position, the top <see cref="TopStandings"/> with names and gaps (the leader's race time at its
    /// last gate), the countdown, "GO!" and "FINISHED".
    /// </summary>
    public void FillHud(ref HudState hud, float speedKmh)
    {
        ref readonly var player = ref session.Get(playerId);
        hud.SpeedKmh = speedKmh;
        hud.Lap = player.CurrentLap;
        hud.LapCount = session.LapCount;
        hud.RaceSeconds = player.HasFinished ? player.FinishTimeSeconds : session.RaceTimeSeconds;
        hud.Position = player.Position;
        hud.KartCount = session.Count;
        hud.Countdown = session.Phase == RacePhase.Countdown ? (int)MathF.Ceiling(session.CountdownRemainingSeconds) : 0;
        hud.ShowGo = session.Phase == RacePhase.Racing && session.RaceTimeSeconds < GoSeconds;
        hud.Finished = player.HasFinished;
        hud.PowerUp = null;

        var rows = Math.Min(TopStandings, session.Count);
        for (var i = 0; i < rows; i++)
            SetStanding(i, session.GetAtPosition(i + 1));
        if (player.Position > rows)
            SetStanding(rows++, player);

        hud.Standings = _standings;
        hud.StandingCount = rows;
    }

    /// <summary>
    /// Counts the time since the player finished (or the race ended); <see cref="ResultsDelaySeconds"/> later it stops
    /// the race, whoever is still out unfinished (DNF), and says the results are due.
    /// </summary>
    /// <remarks>
    /// shortcut: Kartster's autopilot takes over a finished player and fast-forwards the bots left
    /// (<c>FinishRemaining</c>); here they just don't finish. Run the fixed step on for them if DNFs bother players.
    /// </remarks>
    /// <returns>True once the results should show.</returns>
    public bool Update(float seconds)
    {
        if (session.Phase != RacePhase.Finished && !session.Get(playerId).HasFinished)
            return false;

        _finishedSeconds += seconds;
        if (_finishedSeconds < ResultsDelaySeconds)
            return false;

        session.Stop();
        session.UpdatePositions();
        return true;
    }

    /// <summary>The finishing order on <paramref name="trackName"/>, one row per kart. Allocates; call once.</summary>
    public RaceResults Results(string trackName)
    {
        var rows = new ResultRow[session.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            ref readonly var racer = ref session.GetAtPosition(i + 1);
            rows[i] = new ResultRow
            {
                Position = i + 1,
                Name = names[racer.Id],
                TotalSeconds = racer.FinishTimeSeconds,
                BestLapSeconds = racer.BestLapSeconds,
                Finished = racer.HasFinished,
                IsPlayer = racer.Id == playerId,
            };
        }

        return new RaceResults(trackName, rows);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void SetStanding(int row, in Racer racer)
    {
        ref var standing = ref _standings[row];
        standing.Position = racer.Position;
        standing.Name = names[racer.Id];
        standing.Seconds = racer.Position == 1
            ? racer.HasFinished ? racer.FinishTimeSeconds : racer.GateSeconds
            : session.GapToLeader(racer.Id);
        standing.IsPlayer = racer.Id == playerId;
    }
}
