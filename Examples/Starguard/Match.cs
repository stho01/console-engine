namespace Starguard;

/// <summary>
/// The state of one game that isn't an entity: score, lives, wave and whether it's over. Shared by the systems that
/// change it. A restart creates a new scene and so a new match.
/// </summary>
public sealed class Match
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int StartingLives = 3;

    /// <summary>How long the wrecked cannon stays on the ground before it is rebuilt.</summary>
    public const float RespawnSeconds = 1.5f;

    //**********************************************************
    //** props
    //**********************************************************

    public int Score { get; private set; }
    public int Lives { get; private set; } = StartingLives;
    public int Wave { get; private set; } = 1;
    public bool IsOver { get; private set; }

    /// <summary>Seconds until the cannon is rebuilt. Above 0 the cannon can't move or fire and can't be hit.</summary>
    public float RespawnSecondsLeft { get; set; }

    public bool CannonDown => RespawnSecondsLeft > 0f;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void AddPoints(int points) => Score += points;

    /// <summary>Costs a life. The last one ends the match, any other starts the respawn countdown.</summary>
    public void HitCannon()
    {
        Lives--;
        if (Lives <= 0)
            IsOver = true;
        else
            RespawnSecondsLeft = RespawnSeconds;
    }

    /// <summary>The aliens reached the cannon's row: the match ends whatever lives are left.</summary>
    public void Invade()
    {
        Lives = 0;
        IsOver = true;
    }

    public void NextWave() => Wave++;
}
