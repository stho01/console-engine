using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// Soft Step contact solver, as in Box2D v3 (Erin Catto, "Solver2D", 2024), without rotation. A step is split into
/// substeps; each substep integrates velocities, warm starts with the impulses so far, solves the contacts with a soft
/// (spring-damper) push-out, moves the bodies, and relaxes (solves again without push-out, so the push-out doesn't add
/// energy). Restitution is applied once at the end of the step.
/// <para>
/// Bodies don't move their proxies during the step: a contact tracks the separation through the bodies'
/// <see cref="SolverBody.DeltaPosition"/> since the step began.
/// </para>
/// </summary>
internal static class ContactSolver
{
    //**********************************************************
    //** public methods
    //**********************************************************

    public static void IntegrateVelocities(Span<SolverBody> bodies, Vector2 gravity, float h)
    {
        for (var i = 0; i < bodies.Length; i++)
        {
            ref var body = ref bodies[i];
            if (body.Type != BodyType.Dynamic)
                continue;

            var damping = 1f / (1f + h * body.LinearDamping);
            body.Velocity = body.Velocity * damping + gravity * (body.GravityScale * h);
        }
    }

    public static void IntegratePositions(Span<SolverBody> bodies, float h)
    {
        for (var i = 0; i < bodies.Length; i++)
        {
            ref var body = ref bodies[i];
            if (body.Type != BodyType.Static)
                body.DeltaPosition += body.Velocity * h;
        }
    }

    /// <summary>Applies the impulses accumulated so far again, so the solver starts close to the answer.</summary>
    public static void WarmStart(Span<SolverBody> bodies, ReadOnlySpan<ContactConstraint> contacts)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref readonly var contact = ref contacts[i];
            ref var a = ref bodies[contact.A];
            ref var b = ref bodies[contact.B];
            var impulse = contact.Normal * contact.NormalImpulse + Tangent(contact.Normal) * contact.TangentImpulse;
            a.Velocity -= impulse * a.InverseMass;
            b.Velocity += impulse * b.InverseMass;
        }
    }

    /// <summary>
    /// One pass over the contacts. With <paramref name="useBias"/> overlapping contacts are pushed apart softly (at most
    /// <paramref name="maxPushSpeed"/>); without it (relax) only the velocities are corrected. Speculative contacts
    /// (still apart) always allow the bodies to close the gap within the substep, but not more.
    /// </summary>
    public static void Solve(Span<SolverBody> bodies, Span<ContactConstraint> contacts, float inverseH,
        float maxPushSpeed, float linearSlop, bool useBias)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref var contact = ref contacts[i];
            ref var a = ref bodies[contact.A];
            ref var b = ref bodies[contact.B];
            var normal = contact.Normal;

            var separation = Vector2.Dot(b.DeltaPosition - a.DeltaPosition, normal) + contact.BaseSeparation;
            var bias = 0f;
            var massScale = 1f;
            var impulseScale = 0f;
            if (separation > 0f)
            {
                bias = separation * inverseH;
            }
            else if (useBias)
            {
                bias = MathF.Max(contact.Softness.BiasRate * MathF.Min(0f, separation + linearSlop), -maxPushSpeed);
                massScale = contact.Softness.MassScale;
                impulseScale = contact.Softness.ImpulseScale;
            }

            var normalSpeed = Vector2.Dot(b.Velocity - a.Velocity, normal);
            var impulse = -contact.NormalMass * massScale * (normalSpeed + bias) - impulseScale * contact.NormalImpulse;
            var newImpulse = MathF.Max(contact.NormalImpulse + impulse, 0f);
            impulse = newImpulse - contact.NormalImpulse;
            contact.NormalImpulse = newImpulse;
            contact.MaxNormalImpulse = MathF.Max(contact.MaxNormalImpulse, impulse);
            a.Velocity -= normal * (impulse * a.InverseMass);
            b.Velocity += normal * (impulse * b.InverseMass);

            // Friction: at most friction * normal impulse along the surface.
            var tangent = Tangent(normal);
            var tangentSpeed = Vector2.Dot(b.Velocity - a.Velocity, tangent);
            var maxFriction = contact.Friction * contact.NormalImpulse;
            var newTangentImpulse = Math.Clamp(contact.TangentImpulse - contact.NormalMass * tangentSpeed,
                -maxFriction, maxFriction);
            var tangentImpulse = newTangentImpulse - contact.TangentImpulse;
            contact.TangentImpulse = newTangentImpulse;
            a.Velocity -= tangent * (tangentImpulse * a.InverseMass);
            b.Velocity += tangent * (tangentImpulse * b.InverseMass);
        }
    }

    /// <summary>
    /// Bounce: contacts that were closing faster than <paramref name="threshold"/> at the start of the step and pushed
    /// during it leave with <c>restitution</c> times that speed.
    /// </summary>
    public static void ApplyRestitution(Span<SolverBody> bodies, Span<ContactConstraint> contacts, float threshold)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref var contact = ref contacts[i];
            if (contact.Restitution == 0f || contact.RelativeVelocity > -threshold || contact.MaxNormalImpulse == 0f)
                continue;

            ref var a = ref bodies[contact.A];
            ref var b = ref bodies[contact.B];
            var normalSpeed = Vector2.Dot(b.Velocity - a.Velocity, contact.Normal);
            var impulse = -contact.NormalMass * (normalSpeed + contact.Restitution * contact.RelativeVelocity);
            var newImpulse = MathF.Max(contact.NormalImpulse + impulse, 0f);
            impulse = newImpulse - contact.NormalImpulse;
            contact.NormalImpulse = newImpulse;
            a.Velocity -= contact.Normal * (impulse * a.InverseMass);
            b.Velocity += contact.Normal * (impulse * b.InverseMass);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The normal turned a quarter clockwise (Box2D's right perpendicular).</summary>
    private static Vector2 Tangent(Vector2 normal) => new(normal.Y, -normal.X);
}

/// <summary>A body's state inside one physics step. Same index as its <see cref="BodyProxy"/>.</summary>
internal struct SolverBody
{
    public Vector2 Velocity;

    /// <summary>How far the body moved since the step began. Added to its position at the end of the step.</summary>
    public Vector2 DeltaPosition;

    /// <summary>0 for static and kinematic bodies: contacts can't change their velocity.</summary>
    public float InverseMass;

    public float GravityScale;
    public float LinearDamping;
    public BodyType Type;
}

/// <summary>A contact between two bodies (by index) for the solver, touching or speculative.</summary>
internal struct ContactConstraint
{
    public int A;
    public int B;

    /// <summary>Where the pair's impulses go back into the <see cref="PairCache"/>.</summary>
    public int CacheSlot;

    /// <summary>From A to B.</summary>
    public Vector2 Normal;

    /// <summary>Separation at the start of the step. Negative when overlapping.</summary>
    public float BaseSeparation;

    /// <summary>1 / (inverse mass A + inverse mass B). Without rotation it's the same along the tangent.</summary>
    public float NormalMass;

    public float Friction;
    public float Restitution;

    /// <summary>Normal speed at the start of the step, for restitution. Negative when closing.</summary>
    public float RelativeVelocity;

    public float NormalImpulse;
    public float TangentImpulse;
    public float MaxNormalImpulse;
    public Softness Softness;
}

/// <summary>
/// A soft constraint as a spring-damper with a frequency and damping ratio, expressed for one substep
/// (Box2D's <c>b2MakeSoft</c>).
/// </summary>
internal readonly struct Softness(float biasRate, float massScale, float impulseScale)
{
    public readonly float BiasRate = biasRate;
    public readonly float MassScale = massScale;
    public readonly float ImpulseScale = impulseScale;

    public static Softness Create(float hertz, float dampingRatio, float h)
    {
        if (hertz == 0f)
            return new Softness(0f, 1f, 0f);

        var omega = MathF.Tau * hertz;
        var a1 = 2f * dampingRatio + h * omega;
        var a2 = h * omega * a1;
        var a3 = 1f / (1f + a2);
        return new Softness(omega / a1, a2 * a3, a3);
    }
}
