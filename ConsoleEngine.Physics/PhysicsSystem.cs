using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// One physics step. Add it first to <c>EcsScene.FixedSimulation</c>, and the systems that react to its events after
/// it in the same group. Per step, following Box2D v3:
/// <list type="number">
/// <item>Copies every <see cref="Collider"/> into dense lists (<see cref="BodyProxy"/>, <see cref="SolverBody"/>),
/// with a margin of how far the body can move this step.</item>
/// <item>Finds bounding boxes (grown by the margin) that overlap with a <see cref="SpatialHashGrid"/>, drops pairs the
/// rules below exclude, and measures the separation (<see cref="Collision"/>).</item>
/// <item>Compares the pairs with the previous step's (<see cref="PairCache"/>): begin/end events, warm-start impulses.
/// </item>
/// <item>Solves the contacts in <see cref="SubstepCount"/> substeps (<see cref="ContactSolver"/>), then restitution.
/// </item>
/// <item>Writes the new <c>Position</c> and <c>Velocity</c> back.</item>
/// </list>
/// Rigid bodies without a collider are only integrated. Pair rules: the filters must accept each other
/// (<see cref="Collider.Category"/>/<see cref="Collider.Mask"/>), two static bodies are never tested, two sensors never
/// detect each other, and a non-sensor pair needs a dynamic body.
/// </summary>
public sealed class PhysicsSystem : EntitySystem, IPhysicsEvents
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>A bit bigger than a typical console sprite. Pass the size of a typical body instead.</summary>
    public const float DefaultCellSize = 8f;

    /// <summary>
    /// Overlap the solver leaves alone, in cells (Box2D's linear slop, with 1 cell as Box2D's length unit). It keeps
    /// resting contacts touching instead of jittering between touching and apart.
    /// </summary>
    public const float LinearSlop = .005f;

    /// <summary>Pairs closer than this count as touching (Box2D's speculative distance: 4 × the slop).</summary>
    public const float TouchDistance = 4f * LinearSlop;

    private readonly SpatialHashGrid _grid;
    private readonly PairCache _pairCache = new();
    private readonly ArrayBuffer<BodyProxy> _proxies = new();
    private readonly ArrayBuffer<SolverBody> _bodies = new();
    private readonly ArrayBuffer<Surface> _surfaces = new();
    private readonly ArrayBuffer<Entity> _entities = new();
    private readonly ArrayBuffer<BodyPair> _candidates = new();
    private readonly ArrayBuffer<ContactConstraint> _contacts = new();
    private readonly ArrayBuffer<ContactEvent> _contactBegins = new();
    private readonly ArrayBuffer<ContactEvent> _contactEnds = new();
    private readonly ArrayBuffer<SensorEvent> _sensorBegins = new();
    private readonly ArrayBuffer<SensorEvent> _sensorEnds = new();

    private Query _bodiesWithoutCollider;
    private Query _colliders;
    private int _substepCount = 4;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="cellSize">
    /// Broadphase cell size in cells. About the size of a typical body: much smaller puts big bodies in many cells,
    /// much bigger tests many far-apart pairs.
    /// </param>
    public PhysicsSystem(float cellSize = DefaultCellSize) => _grid = new SpatialHashGrid(cellSize);

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Acceleration of dynamic bodies in cells/s², times <see cref="RigidBody.GravityScale"/>. Default 0.</summary>
    public Vector2 Gravity { get; set; }

    public float CellSize => _grid.CellSize;

    /// <summary>Solver substeps per step. More is stiffer and more accurate, and costs more. Box2D's default: 4.</summary>
    public int SubstepCount
    {
        get => _substepCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _substepCount = value;
        }
    }

    /// <summary>
    /// Stiffness of contacts as a spring frequency (Box2D's default: 30), capped at a quarter of the substep rate.
    /// Contacts with a static or kinematic body use twice this.
    /// </summary>
    public float ContactHertz { get; set; } = 30f;

    /// <summary>Damping ratio of the contact spring (Box2D's default: 10, heavily over-damped).</summary>
    public float ContactDampingRatio { get; set; } = 10f;

    /// <summary>Fastest speed, cells/s, at which overlapping bodies are pushed apart (Box2D's default: 3).</summary>
    public float ContactPushMaxSpeed { get; set; } = 3f;

    /// <summary>Slower impacts than this, cells/s, don't bounce (Box2D's default: 1).</summary>
    public float RestitutionThreshold { get; set; } = 1f;

    /// <summary>Colliders in the last step.</summary>
    public int BodyCount => _proxies.Count;

    /// <summary>Contacts the solver worked on in the last step, touching or speculative.</summary>
    public int ContactCount => _contacts.Count;

    /// <summary>Pairs touching in the last step, contacts and sensor overlaps.</summary>
    public int TouchingPairCount => _pairCache.TouchingCount;

    // The event lists are valid until the next step. Read them from systems after this one in the same group: in a
    // group that runs every frame they would be read again in frames without a step.

    /// <summary>Non-sensor pairs that started touching in the last step.</summary>
    public ReadOnlySpan<ContactEvent> ContactBeginEvents => _contactBegins.AsSpan();

    /// <summary>Non-sensor pairs that stopped touching in the last step, or lost a body.</summary>
    public ReadOnlySpan<ContactEvent> ContactEndEvents => _contactEnds.AsSpan();

    /// <summary>Colliders that started overlapping a sensor in the last step.</summary>
    public ReadOnlySpan<SensorEvent> SensorBeginEvents => _sensorBegins.AsSpan();

    /// <summary>Colliders that stopped overlapping a sensor in the last step, or a pair that lost a body.</summary>
    public ReadOnlySpan<SensorEvent> SensorEndEvents => _sensorEnds.AsSpan();

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _bodiesWithoutCollider = World.Query(Query.With<Position, Velocity, RigidBody>().Without<Collider>());
        _colliders = World.Query(Query.With<Position, Collider>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var h = deltaSeconds / _substepCount;
        var integrate = new IntegrateJob { Gravity = Gravity, H = h, SubstepCount = _substepCount };
        _bodiesWithoutCollider.ForEach<IntegrateJob, Position, Velocity, RigidBody>(ref integrate);

        Gather(deltaSeconds);

        _candidates.Clear();
        _grid.Build(_proxies.AsSpan());
        _grid.FindPairs(_proxies.AsSpan(), _candidates);

        _contacts.Clear();
        _contactBegins.Clear();
        _contactEnds.Clear();
        _sensorBegins.Clear();
        _sensorEnds.Clear();
        _pairCache.BeginStep(_candidates.Count);
        CreateContacts(h);
        _pairCache.CollectEnded(_contactEnds, _sensorEnds);

        Solve(h);
        Scatter();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Reads the collider chunks front to back. A dynamic or kinematic body without <c>Velocity</c> is handled as
    /// static. The margin is how far the body can get this step: its speed plus what gravity adds, times the step.
    /// </summary>
    private void Gather(float deltaSeconds)
    {
        _proxies.Clear();
        _bodies.Clear();
        _surfaces.Clear();
        _entities.Clear();
        var count = _colliders.Count;
        _proxies.EnsureCapacity(count);
        _bodies.EnsureCapacity(count);
        _surfaces.EnsureCapacity(count);
        _entities.EnsureCapacity(count);

        foreach (var chunk in _colliders.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var colliders = chunk.GetSpan<Collider>();
            var entities = chunk.Entities;
            var rigidBodies = chunk.Has<RigidBody>() ? chunk.GetSpan<RigidBody>() : default;
            var velocities = chunk.Has<Velocity>() ? chunk.GetSpan<Velocity>() : default;
            var canMove = !rigidBodies.IsEmpty && !velocities.IsEmpty;

            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var collider = ref colliders[i];
                var body = new SolverBody { Type = BodyType.Static };
                if (canMove && rigidBodies[i].Type != BodyType.Static)
                {
                    ref readonly var rigidBody = ref rigidBodies[i];
                    body.Type = rigidBody.Type;
                    body.Velocity = velocities[i].Value;
                    if (rigidBody.Type == BodyType.Dynamic)
                    {
                        body.InverseMass = rigidBody.InverseMass;
                        body.GravityScale = rigidBody.GravityScale;
                        body.LinearDamping = rigidBody.LinearDamping;
                    }
                }

                var reach = body.Velocity.Length() * deltaSeconds;
                if (body.Type == BodyType.Dynamic)
                    reach += (Gravity * body.GravityScale).Length() * deltaSeconds * deltaSeconds;

                _proxies.Add(new BodyProxy
                {
                    Center = positions[i].Value + collider.Offset,
                    Extents = collider.Shape == ShapeType.Circle
                        ? new Vector2(collider.Radius, collider.Radius)
                        : collider.HalfExtents,
                    Category = collider.Category,
                    Mask = collider.Mask,
                    Margin = reach,
                    Shape = collider.Shape,
                    Type = body.Type,
                    IsSensor = collider.IsSensor
                });
                _bodies.Add(body);
                _surfaces.Add(new Surface(collider.Friction, collider.Restitution));
                _entities.Add(entities[i]);
            }
        }
    }

    /// <summary>
    /// Measures each candidate pair, keeps it if it touches or can touch this step, and turns non-sensor pairs into
    /// solver contacts warm-started from the previous step.
    /// </summary>
    private void CreateContacts(float h)
    {
        var proxies = _proxies.AsSpan();
        var bodies = _bodies.AsSpan();
        var surfaces = _surfaces.AsSpan();
        var candidates = _candidates.AsSpan();
        var hertz = MathF.Min(ContactHertz, .25f / h);
        var softness = Softness.Create(hertz, ContactDampingRatio, h);
        var staticSoftness = Softness.Create(2f * hertz, ContactDampingRatio, h);

        for (var i = 0; i < candidates.Length; i++)
        {
            var pair = candidates[i];
            ref readonly var a = ref proxies[pair.A];
            ref readonly var b = ref proxies[pair.B];
            if (!ShouldTest(a, b))
                continue;

            var separation = Collision.Collide(a, b, out var normal);
            var touching = separation < TouchDistance;
            var isSensorPair = a.IsSensor || b.IsSensor;

            // Sensors report overlaps only. Contacts are kept while the bodies can close the gap this step.
            if (isSensorPair ? !touching : separation > a.Margin + b.Margin + TouchDistance)
                continue;

            var kind = a.IsSensor ? PairKind.SensorA : b.IsSensor ? PairKind.SensorB : PairKind.Contact;
            var slot = AddToCache(pair, kind, touching, out var normalImpulse, out var tangentImpulse);
            if (isSensorPair)
                continue;

            ref readonly var bodyA = ref bodies[pair.A];
            ref readonly var bodyB = ref bodies[pair.B];
            var inverseMassSum = bodyA.InverseMass + bodyB.InverseMass;
            _contacts.Add(new ContactConstraint
            {
                A = pair.A,
                B = pair.B,
                CacheSlot = slot,
                Normal = normal,
                BaseSeparation = separation,
                NormalMass = inverseMassSum > 0f ? 1f / inverseMassSum : 0f,
                Friction = MathF.Sqrt(surfaces[pair.A].Friction * surfaces[pair.B].Friction),
                Restitution = MathF.Max(surfaces[pair.A].Restitution, surfaces[pair.B].Restitution),
                RelativeVelocity = Vector2.Dot(bodyB.Velocity - bodyA.Velocity, normal),
                NormalImpulse = normalImpulse,
                TangentImpulse = tangentImpulse,
                Softness = bodyA.InverseMass == 0f || bodyB.InverseMass == 0f ? staticSoftness : softness
            });
        }
    }

    /// <summary>
    /// Adds the pair to the cache in an order that doesn't depend on the chunk layout (lower entity id first), and
    /// raises its begin or end event when its touching state changed since the previous step.
    /// </summary>
    private int AddToCache(BodyPair pair, PairKind kind, bool touching, out float normalImpulse,
        out float tangentImpulse)
    {
        var entityA = _entities[pair.A];
        var entityB = _entities[pair.B];
        if (entityA.Id > entityB.Id)
        {
            (entityA, entityB) = (entityB, entityA);
            kind = kind switch
            {
                PairKind.SensorA => PairKind.SensorB,
                PairKind.SensorB => PairKind.SensorA,
                _ => kind
            };
        }

        var slot = _pairCache.Add(entityA, entityB, kind, touching, out var wasTouching, out normalImpulse,
            out tangentImpulse);
        if (touching == wasTouching)
            return slot;

        switch (kind)
        {
            case PairKind.Contact:
                (touching ? _contactBegins : _contactEnds).Add(new ContactEvent(entityA, entityB));
                break;
            case PairKind.SensorA:
                (touching ? _sensorBegins : _sensorEnds).Add(new SensorEvent(entityA, entityB));
                break;
            default:
                (touching ? _sensorBegins : _sensorEnds).Add(new SensorEvent(entityB, entityA));
                break;
        }

        return slot;
    }

    private void Solve(float h)
    {
        var bodies = _bodies.AsSpan();
        var contacts = _contacts.AsSpan();
        var inverseH = 1f / h;

        for (var substep = 0; substep < _substepCount; substep++)
        {
            ContactSolver.IntegrateVelocities(bodies, Gravity, h);
            ContactSolver.WarmStart(bodies, contacts);
            ContactSolver.Solve(bodies, contacts, inverseH, ContactPushMaxSpeed, LinearSlop, useBias: true);
            ContactSolver.IntegratePositions(bodies, h);
            ContactSolver.Solve(bodies, contacts, inverseH, ContactPushMaxSpeed, LinearSlop, useBias: false);
        }

        ContactSolver.ApplyRestitution(bodies, contacts, RestitutionThreshold);

        for (var i = 0; i < contacts.Length; i++)
            _pairCache.StoreImpulses(contacts[i].CacheSlot, contacts[i].NormalImpulse, contacts[i].TangentImpulse);
    }

    /// <summary>
    /// Writes moved bodies back, chunk by chunk in the same order as <see cref="Gather"/>. Nothing can change the
    /// chunk layout in between: structural changes are blocked or deferred until the group's playback.
    /// </summary>
    private void Scatter()
    {
        var bodies = _bodies.AsSpan();
        var index = 0;
        foreach (var chunk in _colliders.GetChunks())
        {
            if (!chunk.Has<RigidBody>() || !chunk.Has<Velocity>())
            {
                index += chunk.Count;
                continue;
            }

            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++, index++)
            {
                ref readonly var body = ref bodies[index];
                if (body.Type == BodyType.Static)
                    continue;

                positions[i].Value += body.DeltaPosition;
                velocities[i].Value = body.Velocity;
            }
        }
    }

    private static bool ShouldTest(in BodyProxy a, in BodyProxy b)
    {
        if ((a.Category & b.Mask) == 0 || (b.Category & a.Mask) == 0)
            return false;

        if (a.Type == BodyType.Static && b.Type == BodyType.Static)
            return false;

        if (a.IsSensor || b.IsSensor)
            return !(a.IsSensor && b.IsSensor);

        return a.Type == BodyType.Dynamic || b.Type == BodyType.Dynamic;
    }

    /// <summary>Friction and restitution of a body's collider. Only read when contacts are created.</summary>
    private readonly struct Surface(float friction, float restitution)
    {
        public readonly float Friction = friction;
        public readonly float Restitution = restitution;
    }

    /// <summary>
    /// Rigid bodies without a collider: the same substeps as the solver (velocity first, then position), without
    /// contacts.
    /// </summary>
    private struct IntegrateJob : IForEach<Position, Velocity, RigidBody>
    {
        public Vector2 Gravity;
        public float H;
        public int SubstepCount;

        public void Execute(ref Position position, ref Velocity velocity, ref RigidBody body)
        {
            if (body.Type == BodyType.Static)
                return;

            for (var i = 0; i < SubstepCount; i++)
            {
                if (body.Type == BodyType.Dynamic)
                {
                    var damping = 1f / (1f + H * body.LinearDamping);
                    velocity.Value = velocity.Value * damping + Gravity * (body.GravityScale * H);
                }

                position.Value += velocity.Value * H;
            }
        }
    }
}
