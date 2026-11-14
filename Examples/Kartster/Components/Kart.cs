using Kartster.Driving;

namespace Kartster.Components;

/// <summary>The kart the player drives: what it stands on, kept by the <c>KartGroundSystem</c>.</summary>
public struct Kart
{
    /// <summary>Under the kart after the last step; the next step's tyres grip by it.</summary>
    public Surface Surface;

    /// <summary>World Y of the road or grass under the kart, metres.</summary>
    public float Height;
}
