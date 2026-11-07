using System;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// One physics step. Add it first to <c>EcsScene.FixedSimulation</c>, and the systems that react to its events after
/// it in the same group. Per step:
/// <list type="number">
/// <item>Moves every non-static <see cref="RigidBody"/> with a <c>Velocity</c> (gravity and damping for dynamic
/// bodies first).</item>
/// <item>Copies every <see cref="Collider"/> into a dense list (<see cref="BodyProxy"/>).</item>
/// <item>Finds overlapping bounding boxes with a <see cref="SpatialHashGrid"/>, drops pairs the rules below exclude, and
/// tests the shapes (<see cref="Collision"/>).</item>
/// <item>Compares the touching pairs with the previous step's (<see cref="PairCache"/>) and fills the event lists.</item>
/// </list>
/// Pair rules: the filters must accept each other (<see cref="Collider.Category"/>/<see cref="Collider.Mask"/>), two
/// static bodies are never tested, two sensors never detect each other, and a non-sensor pair needs a dynamic body.
/// <para>
/// Phase 2 of <c>docs/design/physics-plan.md</c>: contacts are detected and reported, but don't push bodies apart yet.
/// </para>
/// </summary>
public sealed class PhysicsSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>A bit bigger than a typical console sprite. Pass the size of a typical body instead.</summary>
    public const float DefaultCellSize = 8f;

    private readonly SpatialHashGrid _grid;
    private readonly PairCache _pairCache = new();
    private readonly ArrayBuffer<BodyProxy> _bodies = new();
    private readonly ArrayBuffer<Entity> _entities = new();
    private readonly ArrayBuffer<BodyPair> _candidates = new();
    private readonly ArrayBuffer<ContactEvent> _contactBegins = new();
    private readonly ArrayBuffer<ContactEvent> _contactEnds = new();
    private readonly ArrayBuffer<SensorEvent> _sensorBegins = new();
    private readonly ArrayBuffer<SensorEvent> _sensorEnds = new();

    private Query _movers;
    private Query _colliders;

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

    /// <summary>Colliders in the last step.</summary>
    public int BodyCount => _bodies.Count;

    /// <summary>Pairs touching in the last step, contacts and sensor overlaps.</summary>
    public int TouchingPairCount => _pairCache.Count;

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
        _movers = World.Query(Query.With<Position, Velocity, RigidBody>());
        _colliders = World.Query(Query.With<Position, Collider>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var integrate = new IntegrateJob { DeltaSeconds = deltaSeconds, Gravity = Gravity };
        _movers.ForEach<IntegrateJob, Position, Velocity, RigidBody>(ref integrate);

        GatherBodies();

        var bodies = _bodies.AsSpan();
        _candidates.Clear();
        _grid.Build(bodies);
        _grid.FindPairs(bodies, _candidates);

        _contactBegins.Clear();
        _contactEnds.Clear();
        _sensorBegins.Clear();
        _sensorEnds.Clear();
        _pairCache.BeginStep();
        UpdatePairs(bodies);
        _pairCache.CollectEnded(_contactEnds, _sensorEnds);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Reads the collider chunks front to back into <see cref="_bodies"/> and <see cref="_entities"/>.</summary>
    private void GatherBodies()
    {
        _bodies.Clear();
        _entities.Clear();
        var count = _colliders.Count;
        _bodies.EnsureCapacity(count);
        _entities.EnsureCapacity(count);

        foreach (var chunk in _colliders.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var colliders = chunk.GetSpan<Collider>();
            var entities = chunk.Entities;
            var rigidBodies = chunk.Has<RigidBody>() ? chunk.GetSpan<RigidBody>() : default;

            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var collider = ref colliders[i];
                _bodies.Add(new BodyProxy
                {
                    Center = positions[i].Value + collider.Offset,
                    Extents = collider.Shape == ShapeType.Circle
                        ? new Vector2(collider.Radius, collider.Radius)
                        : collider.HalfExtents,
                    Category = collider.Category,
                    Mask = collider.Mask,
                    Shape = collider.Shape,
                    Type = rigidBodies.IsEmpty ? BodyType.Static : rigidBodies[i].Type,
                    IsSensor = collider.IsSensor
                });
                _entities.Add(entities[i]);
            }
        }
    }

    private void UpdatePairs(ReadOnlySpan<BodyProxy> bodies)
    {
        var candidates = _candidates.AsSpan();
        for (var i = 0; i < candidates.Length; i++)
        {
            var pair = candidates[i];
            ref readonly var a = ref bodies[pair.A];
            ref readonly var b = ref bodies[pair.B];
            if (!ShouldTest(a, b) || !Collision.Collide(a, b, out _, out _))
                continue;

            var kind = a.IsSensor ? PairKind.SensorA : b.IsSensor ? PairKind.SensorB : PairKind.Contact;
            var entityA = _entities[pair.A];
            var entityB = _entities[pair.B];

            // The cache needs one order per pair that doesn't depend on chunk layout.
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

            if (!_pairCache.Touch(entityA, entityB, kind))
                continue;

            switch (kind)
            {
                case PairKind.Contact:
                    _contactBegins.Add(new ContactEvent(entityA, entityB));
                    break;
                case PairKind.SensorA:
                    _sensorBegins.Add(new SensorEvent(entityA, entityB));
                    break;
                default:
                    _sensorBegins.Add(new SensorEvent(entityB, entityA));
                    break;
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

    /// <summary>Semi-implicit Euler: velocity first, then position with the new velocity.</summary>
    private struct IntegrateJob : IForEach<Position, Velocity, RigidBody>
    {
        public float DeltaSeconds;
        public Vector2 Gravity;

        public void Execute(ref Position position, ref Velocity velocity, ref RigidBody body)
        {
            if (body.Type == BodyType.Static)
                return;

            if (body.Type == BodyType.Dynamic)
            {
                velocity.Value += Gravity * (body.GravityScale * DeltaSeconds);
                velocity.Value *= 1f / (1f + DeltaSeconds * body.LinearDamping);
            }

            position.Value += velocity.Value * DeltaSeconds;
        }
    }
}
