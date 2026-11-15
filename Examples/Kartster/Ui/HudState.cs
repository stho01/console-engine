namespace Kartster.Ui;

/// <summary>
/// Everything the race HUD shows, filled each frame by whoever runs the race (race rules, karts) and drawn by
/// <see cref="Hud.Draw"/>. Plain data: the HUD reads it and keeps nothing.
/// </summary>
public struct HudState
{
    public float SpeedKmh;

    /// <summary>1-based. Shown clamped to <see cref="LapCount"/>.</summary>
    public int Lap;

    /// <summary>1 or less: no "LAP n/m", only the clock (a point-to-point race).</summary>
    public int LapCount;

    /// <summary>The race clock; stops on the player's finish time.</summary>
    public float RaceSeconds;

    /// <summary>1-based; with <see cref="KartCount"/> shown as "POS 2/4" when there are no standings.</summary>
    public int Position;

    public int KartCount;

    /// <summary>
    /// Rows to show, leaders first (and the player's own row); null or <see cref="StandingCount"/> 0: none.
    /// </summary>
    public HudStanding[] Standings;

    public int StandingCount;

    /// <summary>The power-up held, upper case; null or empty when the slot is empty (then nothing is drawn).</summary>
    public string PowerUp;

    /// <summary>3, 2, 1 before the start; 0 hides it.</summary>
    public int Countdown;

    /// <summary>"GO!" right after the countdown.</summary>
    public bool ShowGo;

    /// <summary>The player has crossed the line: "FINISHED".</summary>
    public bool Finished;
}

/// <summary>One standings row.</summary>
public struct HudStanding
{
    public int Position;
    public string Name;

    /// <summary>The leader (position 1): their race time at the last gate. Everybody else: their gap to them.</summary>
    public float Seconds;

    /// <summary>Drawn in <see cref="UiColors.Selected"/>.</summary>
    public bool IsPlayer;
}
