namespace Kartster.Ui;

/// <summary>What the results screen shows: the track and one row per kart, in finishing order.</summary>
public readonly struct RaceResults(string trackName, ResultRow[] rows)
{
    public string TrackName { get; } = trackName;
    public ResultRow[] Rows { get; } = rows;
}

public struct ResultRow
{
    public int Position;
    public string Name;
    public float TotalSeconds;
    public float BestLapSeconds;

    /// <summary>False: did not finish, shown as "DNF".</summary>
    public bool Finished;

    /// <summary>Drawn in <see cref="UiColors.Selected"/>.</summary>
    public bool IsPlayer;
}
