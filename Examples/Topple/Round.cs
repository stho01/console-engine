using Topple.Components;

namespace Topple;

public enum RoundPhase : byte
{
    Playing,
    Won,
    Lost
}

/// <summary>
/// The rules of one attempt at a level: the shots left, the score, and when it is won or lost. Won as soon as no crystal
/// is left. Lost when the shots are used up, none is still flying and the towers have come to rest (every block asleep),
/// or <see cref="GiveUpSeconds"/> after the last shot if something keeps moving.
/// </summary>
public sealed class Round(int stones, int bombs)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Bonus points per shot not used when the level is won.</summary>
    public const int ShotBonus = 300;

    /// <summary>Time after a shot before the round can be lost, so a shot that was just thrown counts.</summary>
    public const float MinWaitSeconds = 1f;

    public const float GiveUpSeconds = 8f;

    //**********************************************************
    //** props
    //**********************************************************

    public int Stones { get; private set; } = stones;
    public int Bombs { get; private set; } = bombs;
    public RoundPhase Phase { get; private set; }

    /// <summary>Points for what was broken in this round.</summary>
    public int Score { get; private set; }

    /// <summary>Points for the shots left, set when the round is won.</summary>
    public int Bonus { get; private set; }

    public int FinalScore => Score + Bonus;

    /// <summary>Crystals left, as of the last <see cref="Update"/>.</summary>
    public int Crystals { get; private set; }

    public float SecondsSinceShot { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool HasAmmo(ShotKind kind) => (kind == ShotKind.Stone ? Stones : Bombs) > 0;

    /// <summary>Takes one shot of the kind, if there is one left and the round is still on.</summary>
    public bool TryUseShot(ShotKind kind)
    {
        if (Phase != RoundPhase.Playing || !HasAmmo(kind))
            return false;

        if (kind == ShotKind.Stone)
            Stones--;
        else
            Bombs--;
        SecondsSinceShot = 0f;
        return true;
    }

    public void AddWreck(Material material) => Score += Materials.Get(material).Score;

    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="crystals">Crystals still in the level.</param>
    /// <param name="shotsInFlight">Shots that still exist.</param>
    /// <param name="awakeBlocks">Blocks the physics hasn't put to sleep.</param>
    public void Update(float deltaSeconds, int crystals, int shotsInFlight, int awakeBlocks)
    {
        SecondsSinceShot += deltaSeconds;
        Crystals = crystals;
        if (Phase != RoundPhase.Playing)
            return;

        if (crystals == 0)
        {
            Phase = RoundPhase.Won;
            Bonus = (Stones + Bombs) * ShotBonus;
            return;
        }

        if (Stones + Bombs > 0 || shotsInFlight > 0 || SecondsSinceShot < MinWaitSeconds)
            return;

        if (awakeBlocks == 0 || SecondsSinceShot >= GiveUpSeconds)
            Phase = RoundPhase.Lost;
    }
}
