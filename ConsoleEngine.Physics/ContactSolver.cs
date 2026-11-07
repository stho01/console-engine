using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Soft Step contact solver, as in Box2D v3 (Erin Catto, "Solver2D", 2024). A step is split into substeps; each substep
/// integrates velocities, warm starts with the impulses so far, solves the contacts with a soft (spring-damper)
/// push-out, moves the bodies, and relaxes (solves again without push-out, so the push-out doesn't add energy).
/// Restitution is applied once at the end of the step.
/// <para>
/// Bodies don't move their shapes during the step: a contact point keeps its anchors (relative to each body's origin)
/// and tracks the separation through the bodies' <see cref="BodyState.DeltaPosition"/> and
/// <see cref="BodyState.DeltaRotation"/> since the step began. As in Box2D, the substep loops only touch the 32-byte
/// <see cref="BodyState"/>; masses are copied into each contact, and the rest of a body is in <see cref="BodySim"/>.
/// </para>
/// </summary>
internal static class ContactSolver
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Gravity and damping for awake dynamic bodies.</summary>
    public static void IntegrateVelocities(Span<BodyState> states, ReadOnlySpan<BodySim> sims, Vector2 gravity,
        float h)
    {
        for (var i = 0; i < sims.Length; i++)
        {
            ref readonly var sim = ref sims[i];
            if (sim.Type != BodyType.Dynamic || sim.IsSleeping)
                continue;

            ref var state = ref states[i];
            state.Velocity = state.Velocity * (1f / (1f + h * sim.LinearDamping)) + gravity * (sim.GravityScale * h);
            if (sim.AngularDamping != 0f)
                state.AngularVelocity *= 1f / (1f + h * sim.AngularDamping);
        }
    }

    /// <summary>
    /// Moves every body by its velocity. Static and sleeping bodies have zero velocity, so they need no check.
    /// </summary>
    public static void IntegratePositions(Span<BodyState> states, float h)
    {
        for (var i = 0; i < states.Length; i++)
        {
            ref var state = ref states[i];
            state.DeltaPosition += state.Velocity * h;
            if (state.AngularVelocity == 0f)
                continue;

            // Box2D's b2IntegrateRotation: q += h * w * perp(q), normalised. No sin/cos per substep.
            var angle = h * state.AngularVelocity;
            var q = state.DeltaRotation;
            var turned = new Vector2(q.X - angle * q.Y, q.Y + angle * q.X);
            state.DeltaRotation = turned * (1f / turned.Length());
        }
    }

    /// <summary>Applies the impulses accumulated so far again, so the solver starts close to the answer.</summary>
    public static void WarmStart(Span<BodyState> states, ReadOnlySpan<ContactConstraint> contacts)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref readonly var contact = ref contacts[i];
            ref var a = ref states[contact.A];
            ref var b = ref states[contact.B];
            var tangent = Tangent(contact.Normal);
            var impulse = contact.Normal * contact.Point0.NormalImpulse + tangent * contact.Point0.TangentImpulse;
            if (contact.PointCount == 2)
            {
                var second = contact.Normal * contact.Point1.NormalImpulse + tangent * contact.Point1.TangentImpulse;
                if (!contact.IsLinear)
                    ApplyImpulse(ref a, ref b, contact, contact.Point1, second);
                else
                    impulse += second;
            }

            if (contact.IsLinear)
                ApplyLinearImpulse(ref a, ref b, contact, impulse);
            else
                ApplyImpulse(ref a, ref b, contact, contact.Point0, impulse);
        }
    }

    /// <summary>
    /// One pass over the contacts. With <paramref name="useBias"/> overlapping points are pushed apart softly (at most
    /// <paramref name="maxPushSpeed"/>); without it (relax) only the velocities are corrected. Speculative points (still
    /// apart) always allow the bodies to close the gap within the substep, but not more.
    /// </summary>
    public static void Solve(Span<BodyState> states, Span<ContactConstraint> contacts, float inverseH,
        float maxPushSpeed, float linearSlop, bool useBias)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref var contact = ref contacts[i];
            ref var a = ref states[contact.A];
            ref var b = ref states[contact.B];
            if (contact.IsLinear)
            {
                SolveLinear(ref a, ref b, ref contact, inverseH, maxPushSpeed, linearSlop, useBias);
                continue;
            }

            SolveNormal(ref a, ref b, contact, ref contact.Point0, inverseH, maxPushSpeed, linearSlop, useBias);
            if (contact.PointCount == 2)
                SolveNormal(ref a, ref b, contact, ref contact.Point1, inverseH, maxPushSpeed, linearSlop, useBias);

            var tangent = Tangent(contact.Normal);
            SolveFriction(ref a, ref b, contact, ref contact.Point0, tangent);
            if (contact.PointCount == 2)
                SolveFriction(ref a, ref b, contact, ref contact.Point1, tangent);
        }
    }

    /// <summary>
    /// Bounce: points that were closing faster than <paramref name="threshold"/> at the start of the step and pushed
    /// during it leave with <c>restitution</c> times that speed.
    /// </summary>
    public static void ApplyRestitution(Span<BodyState> states, Span<ContactConstraint> contacts, float threshold)
    {
        for (var i = 0; i < contacts.Length; i++)
        {
            ref var contact = ref contacts[i];
            if (contact.Restitution == 0f)
                continue;

            ref var a = ref states[contact.A];
            ref var b = ref states[contact.B];
            RestitutionPoint(ref a, ref b, contact, ref contact.Point0, threshold);
            if (contact.PointCount == 2)
                RestitutionPoint(ref a, ref b, contact, ref contact.Point1, threshold);
        }
    }

    /// <summary>
    /// <c>1 / (1/mA + 1/mB + (rA × d)²/IA + (rB × d)²/IB)</c>: the mass the point has along direction
    /// <paramref name="direction"/>. 0 if neither body can move.
    /// </summary>
    public static float EffectiveMass(in ContactConstraint contact, Vector2 anchorA, Vector2 anchorB,
        Vector2 direction)
    {
        var rnA = ConvexPolygon.Cross(anchorA, direction);
        var rnB = ConvexPolygon.Cross(anchorB, direction);
        var k = contact.InverseMassA + contact.InverseMassB +
                contact.InverseInertiaA * rnA * rnA + contact.InverseInertiaB * rnB * rnB;
        return k > 0f ? 1f / k : 0f;
    }

    /// <summary>The normal turned a quarter (Box2D's right perpendicular).</summary>
    public static Vector2 Tangent(Vector2 normal) => new(normal.Y, -normal.X);

    /// <summary>Velocity of B's anchor relative to A's: <c>vB + wB × rB - vA - wA × rA</c>.</summary>
    public static Vector2 RelativeVelocity(in BodyState a, in BodyState b, Vector2 anchorA, Vector2 anchorB) =>
        b.Velocity + Perp(anchorB) * b.AngularVelocity - a.Velocity - Perp(anchorA) * a.AngularVelocity;

    //**********************************************************
    //** internals
    //**********************************************************

    private static void SolveNormal(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        ref ContactPoint point, float inverseH, float maxPushSpeed, float linearSlop, bool useBias)
    {
        // Current separation from how far both bodies moved and turned since the step began.
        var turnedA = ConvexPolygon.Rotate(point.AnchorA, a.DeltaRotation.X, a.DeltaRotation.Y);
        var turnedB = ConvexPolygon.Rotate(point.AnchorB, b.DeltaRotation.X, b.DeltaRotation.Y);
        var normal = contact.Normal;
        var separation = Vector2.Dot(b.DeltaPosition - a.DeltaPosition + turnedB - turnedA, normal) +
                         point.AdjustedSeparation;

        var bias = 0f;
        var massScale = 1f;
        var impulseScale = 0f;
        if (separation > 0f)
        {
            bias = separation * inverseH;
        }
        else if (useBias)
        {
            var softness = contact.Softness;
            bias = MathF.Max(softness.BiasRate * MathF.Min(0f, separation + linearSlop), -maxPushSpeed);
            massScale = softness.MassScale;
            impulseScale = softness.ImpulseScale;
        }

        var normalSpeed = Vector2.Dot(RelativeVelocity(a, b, point.AnchorA, point.AnchorB), normal);
        var impulse = -point.NormalMass * massScale * (normalSpeed + bias) - impulseScale * point.NormalImpulse;
        var newImpulse = MathF.Max(point.NormalImpulse + impulse, 0f);
        impulse = newImpulse - point.NormalImpulse;
        point.NormalImpulse = newImpulse;
        point.MaxNormalImpulse = MathF.Max(point.MaxNormalImpulse, impulse);
        ApplyImpulse(ref a, ref b, contact, point, normal * impulse);
    }

    /// <summary>
    /// <see cref="SolveNormal"/> and <see cref="SolveFriction"/> for a contact whose bodies can't turn (no inertia, no
    /// spin): every rotation term is zero, so only the linear velocities and how far the bodies moved count. The same
    /// result with a fraction of the work, for the most common contacts.
    /// </summary>
    private static void SolveLinear(ref BodyState a, ref BodyState b, ref ContactConstraint contact, float inverseH,
        float maxPushSpeed, float linearSlop, bool useBias)
    {
        var normal = contact.Normal;
        var moved = Vector2.Dot(b.DeltaPosition - a.DeltaPosition, normal);
        SolveLinearNormal(ref a, ref b, contact, ref contact.Point0, moved, inverseH, maxPushSpeed, linearSlop, useBias);
        if (contact.PointCount == 2)
        {
            SolveLinearNormal(ref a, ref b, contact, ref contact.Point1, moved, inverseH, maxPushSpeed, linearSlop,
                useBias);
        }

        var tangent = Tangent(normal);
        SolveLinearFriction(ref a, ref b, contact, ref contact.Point0, tangent);
        if (contact.PointCount == 2)
            SolveLinearFriction(ref a, ref b, contact, ref contact.Point1, tangent);
    }

    private static void SolveLinearNormal(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        ref ContactPoint point, float moved, float inverseH, float maxPushSpeed, float linearSlop, bool useBias)
    {
        var separation = moved + point.AdjustedSeparation;
        var bias = 0f;
        var massScale = 1f;
        var impulseScale = 0f;
        if (separation > 0f)
        {
            bias = separation * inverseH;
        }
        else if (useBias)
        {
            var softness = contact.Softness;
            bias = MathF.Max(softness.BiasRate * MathF.Min(0f, separation + linearSlop), -maxPushSpeed);
            massScale = softness.MassScale;
            impulseScale = softness.ImpulseScale;
        }

        var normal = contact.Normal;
        var normalSpeed = Vector2.Dot(b.Velocity - a.Velocity, normal);
        var impulse = -point.NormalMass * massScale * (normalSpeed + bias) - impulseScale * point.NormalImpulse;
        var newImpulse = MathF.Max(point.NormalImpulse + impulse, 0f);
        impulse = newImpulse - point.NormalImpulse;
        point.NormalImpulse = newImpulse;
        point.MaxNormalImpulse = MathF.Max(point.MaxNormalImpulse, impulse);
        ApplyLinearImpulse(ref a, ref b, contact, normal * impulse);
    }

    private static void SolveLinearFriction(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        ref ContactPoint point, Vector2 tangent)
    {
        var tangentSpeed = Vector2.Dot(b.Velocity - a.Velocity, tangent);
        var maxFriction = contact.Friction * point.NormalImpulse;
        var newImpulse = Math.Clamp(point.TangentImpulse - point.TangentMass * tangentSpeed, -maxFriction, maxFriction);
        var impulse = newImpulse - point.TangentImpulse;
        point.TangentImpulse = newImpulse;
        ApplyLinearImpulse(ref a, ref b, contact, tangent * impulse);
    }

    private static void ApplyLinearImpulse(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        Vector2 impulse)
    {
        a.Velocity -= impulse * contact.InverseMassA;
        b.Velocity += impulse * contact.InverseMassB;
    }

    /// <summary>Coulomb friction: at most friction × normal impulse along the surface.</summary>
    private static void SolveFriction(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        ref ContactPoint point, Vector2 tangent)
    {
        var tangentSpeed = Vector2.Dot(RelativeVelocity(a, b, point.AnchorA, point.AnchorB), tangent);
        var maxFriction = contact.Friction * point.NormalImpulse;
        var newImpulse = Math.Clamp(point.TangentImpulse - point.TangentMass * tangentSpeed, -maxFriction, maxFriction);
        var impulse = newImpulse - point.TangentImpulse;
        point.TangentImpulse = newImpulse;
        ApplyImpulse(ref a, ref b, contact, point, tangent * impulse);
    }

    private static void RestitutionPoint(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        ref ContactPoint point, float threshold)
    {
        if (point.RelativeVelocity > -threshold || point.MaxNormalImpulse == 0f)
            return;

        var normal = contact.Normal;
        var normalSpeed = Vector2.Dot(RelativeVelocity(a, b, point.AnchorA, point.AnchorB), normal);
        var impulse = -point.NormalMass * (normalSpeed + contact.Restitution * point.RelativeVelocity);
        var newImpulse = MathF.Max(point.NormalImpulse + impulse, 0f);
        impulse = newImpulse - point.NormalImpulse;
        point.NormalImpulse = newImpulse;
        ApplyImpulse(ref a, ref b, contact, point, normal * impulse);
    }

    private static void ApplyImpulse(ref BodyState a, ref BodyState b, in ContactConstraint contact,
        in ContactPoint point, Vector2 impulse)
    {
        a.Velocity -= impulse * contact.InverseMassA;
        a.AngularVelocity -= contact.InverseInertiaA * ConvexPolygon.Cross(point.AnchorA, impulse);
        b.Velocity += impulse * contact.InverseMassB;
        b.AngularVelocity += contact.InverseInertiaB * ConvexPolygon.Cross(point.AnchorB, impulse);
    }

    /// <summary><c>w × r = w * (-r.y, r.x)</c>.</summary>
    private static Vector2 Perp(Vector2 r) => new(-r.Y, r.X);
}

/// <summary>
/// The part of a body the substep loops read and write, 32 bytes (Box2D's <c>b2BodyState</c>). Same index as its
/// <see cref="BodyProxy"/>. Static and sleeping bodies keep zero velocity.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 32)]
internal struct BodyState
{
    public Vector2 Velocity;
    public float AngularVelocity;

    /// <summary>How far the body moved since the step began. Added to its position at the end of the step.</summary>
    public Vector2 DeltaPosition;

    /// <summary>How far it turned since the step began, as (cos, sin).</summary>
    public Vector2 DeltaRotation;

    public static BodyState AtRest => new() { DeltaRotation = Vector2.UnitX };
}

/// <summary>The rest of a body for one step: read once per step, or per substep for gravity; not in contact loops.</summary>
internal struct BodySim
{
    public BodyType Type;

    /// <summary>Not simulated this step.</summary>
    public bool IsSleeping;

    /// <summary>0 for static and kinematic bodies: contacts can't change their velocity.</summary>
    public float InverseMass;

    /// <summary>0 for bodies that don't turn.</summary>
    public float InverseInertia;

    public float GravityScale;
    public float LinearDamping;
    public float AngularDamping;

    /// <summary>The collider's friction and restitution, read when contacts are created.</summary>
    public float Friction;

    public float Restitution;

    /// <summary>How long the body has been nearly still, in seconds.</summary>
    public float SleepTime;
}

/// <summary>One point of a <see cref="ContactConstraint"/>.</summary>
internal struct ContactPoint
{
    /// <summary>The point relative to body A's origin, at the start of the step.</summary>
    public Vector2 AnchorA;

    public Vector2 AnchorB;

    /// <summary>Separation at the start of the step minus <c>dot(AnchorB - AnchorA, normal)</c> (Box2D).</summary>
    public float AdjustedSeparation;

    public float NormalMass;
    public float TangentMass;
    public float NormalImpulse;
    public float TangentImpulse;
    public float MaxNormalImpulse;

    /// <summary>Normal speed at the start of the step, for restitution. Negative when closing.</summary>
    public float RelativeVelocity;

    /// <summary>Manifold point id, for matching impulses with the previous step.</summary>
    public int Id;
}

/// <summary>A contact between two bodies (by index) for the solver, touching or speculative.</summary>
internal struct ContactConstraint
{
    public int A;
    public int B;

    /// <summary>Where the pair's impulses go back into the <see cref="PairCache"/>.</summary>
    public int CacheSlot;

    public int PointCount;

    /// <summary>
    /// Neither body can turn (no inertia) or spins: the solver can leave out every rotation term
    /// (<see cref="ContactSolver"/>'s linear path). <see cref="ContactPoint.AdjustedSeparation"/> is then the plain
    /// separation at the start of the step.
    /// </summary>
    public bool IsLinear;

    /// <summary>From A to B.</summary>
    public Vector2 Normal;

    public float InverseMassA;
    public float InverseInertiaA;
    public float InverseMassB;
    public float InverseInertiaB;
    public float Friction;
    public float Restitution;
    public Softness Softness;
    public ContactPoint Point0;
    public ContactPoint Point1;
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
