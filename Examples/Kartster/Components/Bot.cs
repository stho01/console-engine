namespace Kartster.Components;

/// <summary>
/// A kart the <c>BotDriverSystem</c> drives: how well, where on the road, and what it is doing now. Its route plan
/// lives in the system, by <see cref="Kart.Index"/>.
/// </summary>
public struct Bot
{
    /// <summary>0 (careful) .. 1 (quick): scales throttle, corner speed, braking and lookahead.</summary>
    public float Skill;

    /// <summary>Metres right of the centerline it aims for, so bots don't drive in one file.</summary>
    public float LaneOffset;

    /// <summary>Nearest route-plan sample last step; -1 before the first.</summary>
    public int Sample;

    /// <summary>Seconds asking for throttle and going nowhere.</summary>
    public float StuckSeconds;

    /// <summary>Seconds of reversing left, after being stuck.</summary>
    public float ReverseSeconds;

    /// <summary>Seconds off the road so far.</summary>
    public float OffRoadSeconds;
}
