using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Systems;

/// <summary>
/// Each kart's height (<c>docs/design/kart-physics-plan.md</c> § 11): gravity, then one-sided support by what is
/// under it (<see cref="Track.TryGetGround"/>, looked for from <see cref="StepHeight"/> over the kart, so a bridge's
/// deck over it does not count), as Kartster's <c>KartGroundConstraint</c>: put back on the surface when it sinks
/// into it, with the vertical speed that follows the surface; left to gravity when the surface falls away faster
/// (over a crest). Also keeps every kart inside the ground square and on the terrain, and notes the surface and its
/// slope for the next step. Runs in the fixed step, after the <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class KartGroundSystem(Track track) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>
    /// Metres over the kart the ground is looked for from: a kerb up to this high is climbed, and a grounded kart
    /// leaving the road for ground up to this much lower is put down on it. A bridge is far higher.
    /// </summary>
    public const float StepHeight = 0.5f;

    /// <summary>Metres above the ground at which the wheels stop working: Kartster's <c>GroundedToleranceMeters</c>.</summary>
    public const float GroundedTolerance = 0.05f;

    /// <summary>Seconds in which the kart's tilt follows the ground: Kartster's <c>UprightingSeconds</c>.</summary>
    public const float UprightingSeconds = 0.06f;

    private const float MinNormalY = 0.2f; // the steepest ground the vertical speed is taken from

    private Query _karts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var center = track.GroundCenter;
        var edge = track.GroundHalfSize - 1;
        var settle = 1f - MathF.Exp(-deltaSeconds / UprightingSeconds);
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var position = ref positions[i].Value;
                ref var velocity = ref velocities[i].Value;
                ref var state = ref karts[i];

                // At the edge the kart stops going outwards, so it slides along it.
                var x = Math.Clamp(position.X, center.X - edge, center.X + edge);
                var z = Math.Clamp(position.Y, center.Z - edge, center.Z + edge);
                if (x != position.X) velocity.X = 0f;
                if (z != position.Y) velocity.Y = 0f;

                var wasGrounded = !state.Airborne;
                state.VerticalSpeed -= KartLoadTransfer.Gravity * deltaSeconds;
                var height = state.Height + state.VerticalSpeed * deltaSeconds;
                var top = height + StepHeight;
                if (!track.TryGetGround(x, z, top, out var ground, out var normal, out var onRoad))
                {
                    // Past the terrain's edge: as at the square's edge, back on the axis that left it.
                    var previousX = x - velocity.X * deltaSeconds;
                    var previousZ = z - velocity.Y * deltaSeconds;
                    if (track.TryGetGround(x, previousZ, top, out _, out _, out _))
                    {
                        z = previousZ;
                        velocity.Y = 0f;
                    }
                    else
                    {
                        x = previousX;
                        velocity.X = 0f;
                        if (!track.TryGetGround(x, z, top, out _, out _, out _))
                        {
                            z = previousZ;
                            velocity.Y = 0f;
                        }
                    }

                    if (!track.TryGetGround(x, z, top, out ground, out normal, out onRoad))
                        ground = height; // nowhere to stand (put down off the terrain): stays where it is
                }

                position = new XnaVector2(x, z);

                // The road edge is a kerb, not a cliff: a grounded kart going onto lower ground stays down on it.
                if (height <= ground || (wasGrounded && !onRoad && height - ground <= StepHeight))
                {
                    height = ground;
                    // Along the surface: the horizontal velocity is the physics body's.
                    state.VerticalSpeed = -(normal.X * velocity.X + normal.Z * velocity.Y) / MathF.Max(normal.Y, MinNormalY);
                    state.Airborne = false;
                }
                else
                {
                    state.Airborne = height - ground > GroundedTolerance;
                }

                state.Height = height;
                if (!state.Airborne)
                    state.Slope += (new System.Numerics.Vector2(normal.X, normal.Z) - state.Slope) * settle;
                state.Surface = onRoad ? Surface.Asphalt : Surface.Grass;
            }
        }
    }
}
