using System;
using System.Numerics;
using Kartster.Driving;

namespace Kartster.Components;

/// <summary>
/// A kart on the track: its slot and what it stands on, kept by the <c>KartGroundSystem</c>. The physics body is
/// planar (x, z); the height is this component's (<c>docs/design/kart-physics-plan.md</c> § 11). Its defaults are a
/// kart standing on flat ground.
/// </summary>
public struct Kart
{
    /// <summary>
    /// 0..karts-1, fixed for the race: the starting-grid slot and the index into the per-kart managed state (the
    /// <c>KartPhysicsSystem</c>'s <see cref="KartDynamics"/>, the bots' route plans, the meshes).
    /// </summary>
    public int Index;

    /// <summary>Under the kart after the last step; the next step's tyres grip by it.</summary>
    public Surface Surface;

    /// <summary>World Y of the kart's ground point (its wheels' bottom), metres: on the road or grass, or in the air.</summary>
    public float Height;

    /// <summary>Metres per second, up positive.</summary>
    public float VerticalSpeed;

    /// <summary>
    /// The ground's up direction under the kart, its X and Z (Y = √(1 − |Slope|²)); zero is flat. Eased towards
    /// the surface under the kart, kept while it is in the air.
    /// </summary>
    public Vector2 Slope;

    /// <summary>More than <c>KartGroundSystem.GroundedTolerance</c> above the ground: the wheels do nothing.</summary>
    public bool Airborne;

    /// <summary>The ground's up direction from <see cref="Slope"/>.</summary>
    public readonly Vector3 Normal =>
        new(Slope.X, MathF.Sqrt(MathF.Max(1f - Slope.LengthSquared(), 0f)), Slope.Y);
}
