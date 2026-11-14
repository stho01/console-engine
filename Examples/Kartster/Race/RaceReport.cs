using System;
using Kartster.Ui;

namespace Kartster.Race;

/// <summary>
/// What a <see cref="RaceSession"/> tells the player: the HUD each frame (<see cref="FillHud"/>, allocation free).
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

    private readonly HudStanding[] _standings = new HudStanding[TopStandings + 1];

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
