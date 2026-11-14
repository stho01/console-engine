namespace Kartster.Bots;

/// <summary>
/// What a bot remembers about traffic between steps, kept by the <see cref="BotDriverSystem"/> per kart slot (the
/// <c>Bot</c> component is not the bots' to change): the lane it is easing towards, a lean on a kart beside it, its
/// wander and its own random numbers, seeded by slot so every race drives the same.
/// </summary>
public struct BotTrafficState
{
    public bool IsStarted;

    /// <summary>Metres right of the centerline it aims for now, moved towards the wanted lane at a limited rate.</summary>
    public float Offset;

    /// <summary>Kart slot it is leaning on, or -1.</summary>
    public int LeanTarget;

    /// <summary>Seconds left of the lean.</summary>
    public float LeanSeconds;

    /// <summary>Seconds before it may think about leaning on somebody again.</summary>
    public float LeanCooldownSeconds;

    /// <summary>The two wander waves, turns 0..1.</summary>
    public float WanderPhase;
    public float WanderPhase2;

    /// <summary>xorshift32 state, never 0.</summary>
    public uint Random;

    /// <summary>A bot in kart slot <paramref name="index"/> starting on <paramref name="laneOffset"/>.</summary>
    public static BotTrafficState Start(int index, float laneOffset)
    {
        var state = new BotTrafficState
        {
            IsStarted = true,
            Offset = laneOffset,
            LeanTarget = -1,
            Random = (uint)(index + 1) * 0x9E3779B9u | 1u,
        };
        state.WanderPhase = state.NextUnit();
        state.WanderPhase2 = state.NextUnit();
        return state;
    }

    /// <summary>The next random number, 0..1.</summary>
    public float NextUnit()
    {
        var x = Random;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        Random = x;
        return (x >> 8) * (1f / (1 << 24));
    }
}
