using Kartster.Driving;

namespace Kartster.Components;

/// <summary>A kart on the track: its slot and what it stands on, kept by the <c>KartGroundSystem</c>.</summary>
public struct Kart
{
    /// <summary>
    /// 0..karts-1, fixed for the race: the starting-grid slot and the index into the per-kart managed state (the
    /// <c>KartPhysicsSystem</c>'s <see cref="KartDynamics"/>, the bots' route plans, the meshes).
    /// </summary>
    public int Index;

    /// <summary>Under the kart after the last step; the next step's tyres grip by it.</summary>
    public Surface Surface;

    /// <summary>World Y of the road or grass under the kart, metres.</summary>
    public float Height;
}
