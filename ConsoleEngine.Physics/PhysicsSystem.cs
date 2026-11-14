using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// One physics step. Add it first to <c>EcsScene.FixedSimulation</c>, and the systems that react to its events after
/// it in the same group. Per step, following Box2D v3:
/// <list type="number">
/// <item>Copies every <see cref="Collider"/> into dense lists (<see cref="BodyProxy"/>, <see cref="BodyShape"/>,
/// <see cref="BodyState"/>, <see cref="BodySim"/>), with a margin of how far the body can move this step.</item>
/// <item>Finds bounding boxes (grown by the margin) that overlap with a <see cref="SpatialHashGrid"/>, drops pairs the
/// rules below exclude, and builds contact manifolds (<see cref="Collision"/>). Dynamic bodies also get contacts with
/// the solid tiles of <see cref="TileGrid"/>.</item>
/// <item>Compares the pairs with the previous step's (<see cref="PairCache"/>): begin/end events, warm-start impulses.
/// </item>
/// <item>Groups touching dynamic bodies into islands; islands that have been still for <see cref="TimeToSleep"/> go to
/// sleep and are skipped until something wakes them.</item>
/// <item>Solves the contacts in <see cref="SubstepCount"/> substeps (<see cref="ContactSolver"/>), then restitution.
/// </item>
/// <item>Writes <c>Position</c>, <c>Velocity</c>, <c>Rotation</c>, <see cref="AngularVelocity"/> and the sleep state
/// back.</item>
/// </list>
/// Rigid bodies without a collider are only integrated. Pair rules: the filters must accept each other
/// (<see cref="Collider.Category"/>/<see cref="Collider.Mask"/>), two static bodies are never tested, two sensors never
/// detect each other, and a non-sensor pair needs a dynamic body.
/// </summary>
public sealed partial class PhysicsSystem : EntitySystem, IPhysicsEvents, IPhysicsQueries
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

    /// <summary>A body slower than this, cells/s, counts as still for sleeping (Box2D's default).</summary>
    public const float LinearSleepTolerance = .05f;

    /// <summary>A body turning slower than this, radians/s (2°/s), counts as still for sleeping (Box2D's default).</summary>
    public const float AngularSleepTolerance = 2f * MathF.PI / 180f;

    /// <summary>The moving (dynamic and kinematic) bodies, rebuilt every step.</summary>
    private readonly SpatialHashGrid _grid;

    /// <summary>The static bodies, rebuilt only when one is added, removed, moved, turned or reshaped.</summary>
    private readonly SpatialHashGrid _staticGrid;

    /// <summary>This step's body indices of the moving bodies, in body order.</summary>
    private readonly ArrayBuffer<int> _movingBodies = new();

    /// <summary>Body indices and bounding boxes of the static bodies <see cref="_staticGrid"/> was built from.</summary>
    private readonly ArrayBuffer<int> _staticBodies = new();

    private readonly ArrayBuffer<StaticBounds> _staticBounds = new();
    private int _staticCount;
    private bool _staticsChanged;
    private readonly PairCache _pairCache = new();
    private readonly ArrayBuffer<BodyProxy> _proxies = new();
    private readonly ArrayBuffer<BodyShape> _shapes = new();
    private readonly ArrayBuffer<BodyState> _states = new();
    private readonly ArrayBuffer<BodySim> _sims = new();
    private readonly ArrayBuffer<Entity> _entities = new();
    private readonly ArrayBuffer<BodyPair> _candidates = new();
    private readonly ArrayBuffer<ContactConstraint> _contacts = new();
    private readonly ArrayBuffer<ContactEvent> _contactBegins = new();
    private readonly ArrayBuffer<ContactEvent> _contactEnds = new();
    private readonly ArrayBuffer<SensorEvent> _sensorBegins = new();
    private readonly ArrayBuffer<SensorEvent> _sensorEnds = new();
    private readonly ArrayBuffer<int> _islandParents = new();
    private readonly ArrayBuffer<float> _islandSleepTimes = new();

    /// <summary>Pairs of two resting bodies skipped this step, with their slot in the previous cache table.</summary>
    private readonly ArrayBuffer<RestingPair> _restingPairs = new();

    /// <summary>Body index by entity id, valid where <see cref="_indexStamps"/> holds this step's stamp.</summary>
    private int[] _indexById = new int[64];

    private int[] _indexStamps = new int[64];
    private int _indexStamp;
    private int _indexMapStamp = -1;

    /// <summary>Index of the static solver body that stands for every tile, or -1.</summary>
    private int _tileBody = -1;

    private Query _bodiesWithoutCollider;
    private Query _colliders;
    private Query _forces;
    private int _substepCount = 4;

    /// <summary>Some body was asleep at the start of the step, so islands must be checked for waking.</summary>
    private bool _anySleeping;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="cellSize">
    /// Broadphase cell size in cells. About the size of a typical body: much smaller puts big bodies in many cells,
    /// much bigger tests many far-apart pairs.
    /// </param>
    public PhysicsSystem(float cellSize = DefaultCellSize)
    {
        _grid = new SpatialHashGrid(cellSize);
        _staticGrid = new SpatialHashGrid(cellSize);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Acceleration of dynamic bodies in cells/s², times <see cref="RigidBody.GravityScale"/>. Default 0.</summary>
    public Vector2 Gravity { get; set; }

    public float CellSize => _grid.CellSize;

    /// <summary>The polygon shapes <see cref="Collider.Polygon"/> colliders refer to.</summary>
    public PolygonLibrary Polygons { get; } = new();

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

    /// <summary>Whether still islands of bodies go to sleep. Default on.</summary>
    public bool EnableSleep { get; set; } = true;

    /// <summary>Seconds a whole island must be still before it sleeps (Box2D's default: 0.5).</summary>
    public float TimeToSleep { get; set; } = .5f;

    /// <summary>
    /// Solid tiles. Dynamic bodies collide with them, and so do the spatial queries and <see cref="CharacterMover"/>.
    /// Optional.
    /// </summary>
    public TileGrid TileGrid { get; set; }

    /// <summary>Colliders in the last step.</summary>
    public int BodyCount => _proxies.Count;

    /// <summary>Dynamic bodies asleep after the last step.</summary>
    public int SleepingBodyCount { get; private set; }

    /// <summary>Contacts the solver worked on in the last step (touching or speculative, bodies and tiles).</summary>
    public int ContactCount => _contacts.Count;

    /// <summary>Pairs of colliders touching in the last step, contacts and sensor overlaps. Tiles not counted.</summary>
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
        _forces = World.Query(Query.With<Force>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var h = deltaSeconds / _substepCount;
        IntegrateBodiesWithoutCollider(h);
        Gather(deltaSeconds);

        _candidates.Clear();
        _grid.Build(_proxies.AsSpan(), _movingBodies.AsSpan());
        if (_staticsChanged)
            _staticGrid.Build(_proxies.AsSpan(), _staticBodies.AsSpan());
        _grid.FindPairs(_candidates);
        if (_staticGrid.EntryCount > 0)
            _grid.FindPairs(_staticGrid, _candidates);

        _contacts.Clear();
        _contactBegins.Clear();
        _contactEnds.Clear();
        _sensorBegins.Clear();
        _sensorEnds.Clear();
        _pairCache.BeginStep(_candidates.Count + CountTileCandidates());
        var hertz = MathF.Min(ContactHertz, .25f / h);
        var softness = Softness.Create(hertz, ContactDampingRatio, h);
        var staticSoftness = Softness.Create(2f * hertz, ContactDampingRatio, h);
        CreateContacts(softness, staticSoftness);
        _pairCache.CollectEnded(_contactEnds, _sensorEnds);

        if (EnableSleep && _anySleeping)
        {
            WakeBodiesThatLostContacts();
            WakeTouchedIslands();
            ResolveRestingPairs(softness, staticSoftness);
        }

        CreateTileContacts(staticSoftness);
        if (EnableSleep && _anySleeping)
            DropSleepingContacts();

        Solve(h);

        if (EnableSleep)
            UpdateSleep(deltaSeconds);
        else
            SleepingBodyCount = 0;

        Scatter();
        ClearForces();
        _queryStateIsCurrent = false;
    }

    //**********************************************************
    //** internals: gather
    //**********************************************************

    /// <summary>
    /// Reads the collider chunks front to back. A dynamic or kinematic body without <c>Velocity</c> is handled as
    /// static. The margin is how far the body can get this step: its speed, what gravity adds, and how far its farthest
    /// point travels while it turns.
    /// </summary>
    private void Gather(float deltaSeconds)
    {
        // One slot more in the body arrays for the tile body. Every slot is written below, so nothing is cleared.
        var count = _colliders.Count;
        var bodyCount = TileGrid != null ? count + 1 : count;
        var proxies = _proxies.Resize(count);
        var shapes = _shapes.Resize(count);
        var states = _states.Resize(bodyCount);
        var sims = _sims.Resize(bodyCount);
        var bodyEntities = _entities.Resize(count);
        _indexStamp++;
        _anySleeping = false;
        _movingBodies.Clear();
        _staticCount = 0;
        _staticsChanged = false;
        var index = 0;

        foreach (var chunk in _colliders.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var colliders = chunk.GetSpan<Collider>();
            var entities = chunk.Entities;
            var rigidBodies = chunk.Has<RigidBody>() ? chunk.GetSpan<RigidBody>() : default;
            var velocities = chunk.Has<Velocity>() ? chunk.GetSpan<Velocity>() : default;
            var rotations = chunk.Has<Rotation>() ? chunk.GetSpan<Rotation>() : default;
            var spins = chunk.Has<AngularVelocity>() ? chunk.GetSpan<AngularVelocity>() : default;
            var canMove = !rigidBodies.IsEmpty && !velocities.IsEmpty;
            var canTurn = canMove && !rotations.IsEmpty && !spins.IsEmpty;
            var interpolations = chunk.Has<Interpolation>() ? chunk.GetSpan<Interpolation>() : default;
            var forces = chunk.Has<Force>() ? chunk.GetSpan<Force>() : default;

            for (var i = 0; i < chunk.Count; i++, index++)
            {
                ref readonly var collider = ref colliders[i];
                var angle = rotations.IsEmpty ? 0f : rotations[i].Radians;
                if (!interpolations.IsEmpty)
                    RememberTransform(ref interpolations[i], positions[i].Value, angle);
                var (sin, cos) = angle == 0f ? (0f, 1f) : MathF.SinCos(angle);
                ref var shape = ref shapes[index];
                shape.Position = positions[i].Value;
                shape.Cos = cos;
                shape.Sin = sin;
                shape.Shape = collider.Shape;
                shape.Radius = collider.Radius;
                shape.Offset = collider.Offset;
                shape.HalfExtents = collider.HalfExtents;
                shape.PolygonId = collider.PolygonId;

                ref var proxy = ref proxies[index];
                ComputeBounds(shape, out proxy.Center, out proxy.Extents, out var reachRadius);

                ref var state = ref states[index];
                ref var sim = ref sims[index];
                state = BodyState.AtRest;
                sim = default;
                if (canMove && rigidBodies[i].Type != BodyType.Static)
                {
                    ReadBody(rigidBodies[i], velocities[i].Value, canTurn ? spins[i].Value : 0f, canTurn, shape,
                        forces.IsEmpty ? default : forces[i], ref state, ref sim);
                }

                var reach = (state.Velocity.Length() + MathF.Abs(state.AngularVelocity) * reachRadius) * deltaSeconds;
                if (sim.Type == BodyType.Dynamic)
                    reach += sim.Acceleration.Length() * deltaSeconds * deltaSeconds;
                _anySleeping |= sim.IsSleeping;

                proxy.Category = collider.Category;
                proxy.Mask = collider.Mask;
                proxy.Margin = sim.IsSleeping ? 0f : reach;
                proxy.Type = sim.Type;
                proxy.IsSensor = collider.IsSensor;
                proxy.IsSleeping = sim.IsSleeping;
                sim.Friction = collider.Friction;
                sim.Restitution = collider.Restitution;
                bodyEntities[index] = entities[i];
                if (sim.Type == BodyType.Static)
                    NoteStatic(index, proxy);
                else
                    _movingBodies.Add(index);
            }
        }

        if (_staticCount != _staticBodies.Count)
        {
            _staticBodies.Truncate(_staticCount);
            _staticBounds.Truncate(_staticCount);
            _staticsChanged = true;
        }

        _tileBody = -1;
        if (TileGrid != null)
        {
            _tileBody = count;
            states[count] = BodyState.AtRest;
            sims[count] = default;
        }
    }

    /// <summary>
    /// Counts a static body and compares it with the same static of the last step: a different body index or bounding
    /// box means <see cref="_staticGrid"/> must be rebuilt. Bounds cover a changed position, rotation or collider.
    /// </summary>
    private void NoteStatic(int body, in BodyProxy proxy)
    {
        var k = _staticCount++;
        var bounds = new StaticBounds(proxy.Center, proxy.Extents);
        if (k < _staticBodies.Count)
        {
            if (_staticBodies[k] == body && _staticBounds[k].Equals(bounds))
                return;

            _staticBodies[k] = body;
            _staticBounds[k] = bounds;
        }
        else
        {
            _staticBodies.Add(body);
            _staticBounds.Add(bounds);
        }

        _staticsChanged = true;
    }

    private void ReadBody(in RigidBody rigidBody, Vector2 velocity, float spin, bool canTurn, in BodyShape shape,
        in Force force, ref BodyState state, ref BodySim sim)
    {
        sim.Type = rigidBody.Type;
        state.Velocity = velocity;
        state.AngularVelocity = spin;
        if (rigidBody.Type != BodyType.Dynamic)
            return;

        sim.InverseMass = rigidBody.InverseMass;
        sim.LinearDamping = rigidBody.LinearDamping;
        sim.AngularDamping = rigidBody.AngularDamping;
        if (canTurn)
        {
            var inertiaPerMass = InertiaPerMass(shape);
            sim.InverseInertia = inertiaPerMass > 0f ? rigidBody.InverseMass / inertiaPerMass : 0f;
        }

        sim.Acceleration = Gravity * rigidBody.GravityScale + force.Value * rigidBody.InverseMass;
        sim.AngularAcceleration = force.Torque * sim.InverseInertia;

        // Game code giving a sleeping body a velocity or a force wakes it, and so does turning sleep off.
        var woken = rigidBody.IsSleeping && (!EnableSleep || velocity != Vector2.Zero || spin != 0f ||
                                             force.Value != Vector2.Zero || force.Torque != 0f);
        sim.IsSleeping = rigidBody.IsSleeping && !woken;
        sim.SleepTime = woken ? 0f : rigidBody.SleepTime;
    }

    /// <summary>Rotational inertia about the body origin per unit of mass, for a uniform shape.</summary>
    private float InertiaPerMass(in BodyShape shape) => shape.Shape switch
    {
        ShapeType.Circle => .5f * shape.Radius * shape.Radius + shape.Offset.LengthSquared(),
        ShapeType.Box => (shape.HalfExtents.X * shape.HalfExtents.X + shape.HalfExtents.Y * shape.HalfExtents.Y) / 3f +
                         shape.Offset.LengthSquared(),
        _ => Polygons.InertiaPerMass(shape.PolygonId)
    };

    /// <summary>The world bounding box of a shape, and the distance of its farthest point from the body origin.</summary>
    private void ComputeBounds(in BodyShape shape, out Vector2 center, out Vector2 extents, out float reachRadius)
    {
        switch (shape.Shape)
        {
            case ShapeType.Circle:
                center = shape.ToWorld(shape.Offset);
                extents = new Vector2(shape.Radius, shape.Radius);
                reachRadius = shape.Offset.Length() + shape.Radius;
                return;
            case ShapeType.Box:
                center = shape.ToWorld(shape.Offset);
                var cos = MathF.Abs(shape.Cos);
                var sin = MathF.Abs(shape.Sin);
                var half = shape.HalfExtents;
                extents = new Vector2(cos * half.X + sin * half.Y, sin * half.X + cos * half.Y);
                reachRadius = shape.Offset.Length() + half.Length();
                return;
            default:
                ref readonly var polygon = ref Polygons.Get(shape.PolygonId);
                var min = new Vector2(float.MaxValue);
                var max = new Vector2(float.MinValue);
                for (var i = 0; i < polygon.Count; i++)
                {
                    var point = shape.ToWorld(polygon.Points[i]);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }

                center = (min + max) * .5f;
                extents = (max - min) * .5f;
                reachRadius = Polygons.Radius(shape.PolygonId);
                return;
        }
    }

    /// <summary>
    /// Fills the entity-id-to-index map for this step. Only needed to wake bodies by entity, so it is built on first use
    /// instead of in <see cref="Gather"/>.
    /// </summary>
    private void BuildIndexMap()
    {
        if (_indexMapStamp == _indexStamp)
            return;

        var entities = _entities.AsSpan();
        for (var index = 0; index < entities.Length; index++)
        {
            var id = entities[index].Id;
            if (id >= _indexById.Length)
            {
                var size = Math.Max(id + 1, _indexById.Length * 2);
                Array.Resize(ref _indexById, size);
                Array.Resize(ref _indexStamps, size);
            }

            _indexById[id] = index;
            _indexStamps[id] = _indexStamp;
        }

        _indexMapStamp = _indexStamp;
    }

    /// <summary>This step's body index of <paramref name="entity"/>, or -1 if it has no collider (or is dead).</summary>
    private int IndexOf(Entity entity)
    {
        BuildIndexMap();
        var id = entity.Id;
        if ((uint)id >= (uint)_indexById.Length || _indexStamps[id] != _indexStamp)
            return -1;

        var index = _indexById[id];
        return _entities[index] == entity ? index : -1;
    }

    //**********************************************************
    //** internals: contacts
    //**********************************************************

    /// <summary>Contact manifolds for every candidate pair the rules allow, and their begin/end events.</summary>
    private void CreateContacts(Softness softness, Softness staticSoftness)
    {
        var proxies = _proxies.AsSpan();
        var candidates = _candidates.AsSpan();
        _restingPairs.Clear();

        for (var i = 0; i < candidates.Length; i++)
        {
            var pair = candidates[i];
            ref readonly var a = ref proxies[pair.A];
            ref readonly var b = ref proxies[pair.B];
            if (!ShouldTest(a, b))
                continue;

            // Neither body can have moved since the previous step: keep that step's result instead of measuring again,
            // unless one of them wakes up this step (ResolveRestingPairs).
            if (_anySleeping && !a.IsSensor && !b.IsSensor && IsResting(a) && IsResting(b))
            {
                OrderedEntities(pair, out var first, out var second);
                var previous = _pairCache.FindPrevious(first, PairCache.KeyOf(second));
                if (previous >= 0)
                {
                    _restingPairs.Add(new RestingPair(pair, previous));
                    continue;
                }
            }

            CreateContact(pair, softness, staticSoftness);
        }
    }

    /// <summary>Measures one candidate pair and, if it touches or can touch, caches it and adds its contact.</summary>
    private void CreateContact(BodyPair pair, Softness softness, Softness staticSoftness)
    {
        ref readonly var a = ref _proxies[pair.A];
        ref readonly var b = ref _proxies[pair.B];
        var isSensorPair = a.IsSensor || b.IsSensor;
        var speculative = isSensorPair ? TouchDistance : a.Margin + b.Margin + TouchDistance;
        Collision.Collide(_shapes[pair.A], _shapes[pair.B], Polygons, speculative, out var manifold);
        if (manifold.PointCount == 0)
            return;

        var touching = manifold.MinSeparation < TouchDistance;
        if (isSensorPair && !touching)
            return;

        var kind = a.IsSensor ? PairKind.SensorA : b.IsSensor ? PairKind.SensorB : PairKind.Contact;
        var slot = AddToCache(pair, kind, touching, out var impulses);
        if (isSensorPair)
            return;

        if (_anySleeping)
            WakeIfPushed(pair.A, pair.B);
        AddContact(pair.A, pair.B, _shapes[pair.B].Position, slot, manifold, impulses, softness, staticSoftness);
    }

    private static bool IsResting(in BodyProxy proxy) => proxy.IsSleeping || proxy.Type == BodyType.Static;

    /// <summary>
    /// The resting pairs skipped in <see cref="CreateContacts"/>, after waking. A pair where a body woke is measured after
    /// all, and wakes its other body too, so a whole sleeping pile wakes in one step (repeated until nothing changes).
    /// The rest keep the previous step's cache entry: touching, impulses and all.
    /// </summary>
    private void ResolveRestingPairs(Softness softness, Softness staticSoftness)
    {
        var resting = _restingPairs.AsSpan();
        var remaining = resting.Length;
        bool changed;
        do
        {
            changed = false;
            for (var i = 0; i < remaining; i++)
            {
                var pair = resting[i].Pair;
                if (!IsAwakeDynamic(pair.A) && !IsAwakeDynamic(pair.B))
                    continue;

                if (_sims[pair.A].IsSleeping) Wake(pair.A);
                if (_sims[pair.B].IsSleeping) Wake(pair.B);
                CreateContact(pair, softness, staticSoftness);

                // Done with this one: move the last unresolved pair into its place.
                resting[i--] = resting[--remaining];
                changed = true;
            }
        } while (changed);

        for (var i = 0; i < remaining; i++)
            _pairCache.CopyPrevious(resting[i].PreviousSlot);
    }

    private bool IsAwakeDynamic(int body) => _sims[body].Type == BodyType.Dynamic && !_sims[body].IsSleeping;

    private void OrderedEntities(BodyPair pair, out Entity first, out Entity second)
    {
        first = _entities[pair.A];
        second = _entities[pair.B];
        if (first.Id > second.Id)
            (first, second) = (second, first);
    }

    /// <summary>A sleeping body touched by a moving kinematic body wakes (dynamic ones wake it through islands).</summary>
    private void WakeIfPushed(int a, int b)
    {
        if (_sims[a].IsSleeping && _sims[b].Type == BodyType.Kinematic && IsMoving(_states[b]))
            Wake(a);
        if (_sims[b].IsSleeping && _sims[a].Type == BodyType.Kinematic && IsMoving(_states[a]))
            Wake(b);
    }

    private static bool IsMoving(in BodyState state) => state.Velocity != Vector2.Zero || state.AngularVelocity != 0f;

    /// <summary>Upper bound of the tile contacts <see cref="CreateTileContacts"/> can add, to size the pair cache.</summary>
    private int CountTileCandidates()
    {
        if (_tileBody < 0)
            return 0;

        var count = 0;
        var proxies = _proxies.AsSpan();
        for (var i = 0; i < proxies.Length; i++)
        {
            if (CollidesWithTiles(proxies[i]))
            {
                TileRange(proxies[i], out var firstColumn, out var firstRow, out var lastColumn, out var lastRow);
                count += Math.Max(0, lastColumn - firstColumn + 1) * Math.Max(0, lastRow - firstRow + 1);
            }
        }

        return count;
    }

    /// <summary>
    /// Contacts between awake dynamic bodies and the solid tiles under their grown bounds. A tile face that touches
    /// another solid tile is inside the wall and never pushes, so bodies slide over tile seams instead of catching on
    /// them: boxes and polygons only use the tile's open faces (<see cref="Collision.PolygonTile"/>), and a circle's
    /// contact is skipped when it pushes out of a closed face. Tile contacts have no events.
    /// </summary>
    private void CreateTileContacts(Softness staticSoftness)
    {
        if (_tileBody < 0)
            return;

        var tiles = TileGrid;
        var tileHalf = tiles.TileSize * .5f;
        var tileShape = BodyShape.Unrotated(Vector2.Zero, ShapeType.Box);
        tileShape.HalfExtents = tileHalf;
        var proxies = _proxies.AsSpan();
        var shapes = _shapes.AsSpan();

        for (var i = 0; i < proxies.Length; i++)
        {
            ref readonly var proxy = ref proxies[i];
            if (!CollidesWithTiles(proxy) || _sims[i].IsSleeping)
                continue;

            var speculative = proxy.Margin + TouchDistance;
            var isCircle = shapes[i].Shape == ShapeType.Circle;
            var polygon = default(ConvexPolygon);
            if (!isCircle)
                shapes[i].ToWorldPolygon(Polygons, out polygon);

            TileRange(proxy, out var firstColumn, out var firstRow, out var lastColumn, out var lastRow);
            for (var row = firstRow; row <= lastRow; row++)
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (!tiles.IsSolid(column, row))
                    continue;

                tileShape.Position = tiles.TileMin(column, row) + tileHalf;
                Manifold manifold;
                if (isCircle)
                {
                    Collision.Collide(shapes[i], tileShape, Polygons, speculative, out manifold);
                    if (manifold.PointCount == 0 || IsInternalFace(tiles, column, row, manifold.Normal))
                        continue;
                }
                else
                {
                    var tile = ConvexPolygon.Box(tileShape.Position, tileHalf);
                    Collision.PolygonTile(polygon, tile, OpenFaces(tiles, column, row), speculative, out manifold);
                    if (manifold.PointCount == 0)
                        continue;
                }

                var touching = manifold.MinSeparation < TouchDistance;
                var slot = _pairCache.Add(_entities[i], Entity.Null, PairCache.KeyOfTile(row * tiles.Columns + column),
                    PairKind.Tile, touching, out _, out var impulses);
                AddContact(i, _tileBody, tileShape.Position, slot, manifold, impulses, staticSoftness, staticSoftness);
            }
        }
    }

    /// <summary>
    /// Dynamic, non-sensor bodies whose mask includes the tiles. <see cref="CreateTileContacts"/> runs after waking and
    /// skips bodies that still sleep: they can't have moved, and their tile contacts would be dropped anyway.
    /// </summary>
    private bool CollidesWithTiles(in BodyProxy proxy) =>
        proxy.Type == BodyType.Dynamic && !proxy.IsSensor && (proxy.Mask & TileGrid.Category) != 0;

    private void TileRange(in BodyProxy proxy, out int firstColumn, out int firstRow, out int lastColumn,
        out int lastRow)
    {
        var tiles = TileGrid;
        var reach = proxy.BoundsExtents + new Vector2(TouchDistance, TouchDistance);
        firstColumn = Math.Max(tiles.ColumnOf(proxy.Center.X - reach.X), 0);
        firstRow = Math.Max(tiles.RowOf(proxy.Center.Y - reach.Y), 0);
        lastColumn = Math.Min(tiles.ColumnOf(proxy.Center.X + reach.X), tiles.Columns - 1);
        lastRow = Math.Min(tiles.RowOf(proxy.Center.Y + reach.Y), tiles.Rows - 1);
    }

    /// <summary>
    /// The faces of a tile that don't touch another solid tile, as bits in <see cref="ConvexPolygon.Box"/>'s face order:
    /// up, right, down, left.
    /// </summary>
    private static int OpenFaces(TileGrid tiles, int column, int row) =>
        (tiles.IsSolid(column, row - 1) ? 0 : 1) |
        (tiles.IsSolid(column + 1, row) ? 0 : 2) |
        (tiles.IsSolid(column, row + 1) ? 0 : 4) |
        (tiles.IsSolid(column - 1, row) ? 0 : 8);

    /// <summary>
    /// Whether the tile face the contact pushes out of touches another solid tile. <paramref name="normal"/> points from
    /// the body into the tile, so the face looks the other way; corners use their dominant axis.
    /// </summary>
    private static bool IsInternalFace(TileGrid tiles, int column, int row, Vector2 normal) =>
        MathF.Abs(normal.X) >= MathF.Abs(normal.Y)
            ? tiles.IsSolid(column - MathF.Sign(normal.X), row)
            : tiles.IsSolid(column, row - MathF.Sign(normal.Y));

    /// <summary>Turns a manifold into a solver contact, warm-started with the cached impulses of matching points.</summary>
    private void AddContact(int a, int b, Vector2 originB, int slot, in Manifold manifold, in CachedImpulses impulses,
        Softness softness, Softness staticSoftness)
    {
        ref readonly var simA = ref _sims[a];
        ref readonly var simB = ref _sims[b];

        var friction = b == _tileBody ? simA.Friction : MathF.Sqrt(simA.Friction * simB.Friction);
        var restitution = b == _tileBody
            ? simA.Restitution
            : MathF.Max(simA.Restitution, simB.Restitution);
        ref var contact = ref _contacts.Push();
        contact.A = a;
        contact.B = b;
        contact.CacheSlot = slot;
        contact.PointCount = manifold.PointCount;
        contact.Normal = manifold.Normal;
        contact.InverseMassA = simA.InverseMass;
        contact.InverseInertiaA = simA.InverseInertia;
        contact.InverseMassB = simB.InverseMass;
        contact.InverseInertiaB = simB.InverseInertia;
        contact.Friction = friction;
        contact.Restitution = restitution;
        contact.Softness = simA.InverseMass == 0f || simB.InverseMass == 0f ? staticSoftness : softness;
        ref readonly var stateA = ref _states[a];
        ref readonly var stateB = ref _states[b];
        contact.IsLinear = simA.InverseInertia == 0f && simB.InverseInertia == 0f &&
                           stateA.AngularVelocity == 0f && stateB.AngularVelocity == 0f;

        var originA = _shapes[a].Position;
        PreparePoint(contact, ref contact.Point0, stateA, stateB, originA, originB, manifold.Point0, impulses);
        if (manifold.PointCount == 2)
            PreparePoint(contact, ref contact.Point1, stateA, stateB, originA, originB, manifold.Point1, impulses);
    }

    private static void PreparePoint(in ContactConstraint contact, ref ContactPoint target, in BodyState a,
        in BodyState b, Vector2 originA, Vector2 originB, in ManifoldPoint point, in CachedImpulses impulses)
    {
        var normal = contact.Normal;
        impulses.Find(point.Id, out var normalImpulse, out var tangentImpulse);
        if (contact.IsLinear)
        {
            // No rotation terms: the anchors don't matter and the mass is the same in every direction.
            var massSum = contact.InverseMassA + contact.InverseMassB;
            var mass = massSum > 0f ? 1f / massSum : 0f;
            target.AdjustedSeparation = point.Separation;
            target.NormalMass = mass;
            target.TangentMass = mass;
            target.NormalImpulse = normalImpulse;
            target.TangentImpulse = tangentImpulse;
            target.MaxNormalImpulse = 0f;
            target.RelativeVelocity = Vector2.Dot(b.Velocity - a.Velocity, normal);
            target.Id = point.Id;
            return;
        }

        var anchorA = point.Point - originA;
        var anchorB = point.Point - originB;
        target.AnchorA = anchorA;
        target.AnchorB = anchorB;
        target.AdjustedSeparation = point.Separation - Vector2.Dot(anchorB - anchorA, normal);
        target.NormalMass = ContactSolver.EffectiveMass(contact, anchorA, anchorB, normal);
        target.TangentMass = ContactSolver.EffectiveMass(contact, anchorA, anchorB, ContactSolver.Tangent(normal));
        target.NormalImpulse = normalImpulse;
        target.TangentImpulse = tangentImpulse;
        target.MaxNormalImpulse = 0f;
        target.RelativeVelocity = Vector2.Dot(ContactSolver.RelativeVelocity(a, b, anchorA, anchorB), normal);
        target.Id = point.Id;
    }

    /// <summary>
    /// Adds the pair to the cache in an order that doesn't depend on the chunk layout (lower entity id first), and
    /// raises its begin or end event when its touching state changed since the previous step.
    /// </summary>
    private int AddToCache(BodyPair pair, PairKind kind, bool touching, out CachedImpulses impulses)
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

        var slot = _pairCache.Add(entityA, entityB, PairCache.KeyOf(entityB), kind, touching, out var wasTouching,
            out impulses);
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

    //**********************************************************
    //** internals: sleep
    //**********************************************************

    private void Wake(int body)
    {
        ref var sim = ref _sims[body];
        sim.IsSleeping = false;
        sim.SleepTime = 0f;
    }

    /// <summary>A body whose contact ended (the other body moved away or was destroyed) wakes, so it can fall.</summary>
    private void WakeBodiesThatLostContacts()
    {
        var ended = _contactEnds.AsSpan();
        for (var i = 0; i < ended.Length; i++)
        {
            var a = IndexOf(ended[i].A);
            var b = IndexOf(ended[i].B);
            if (a >= 0) Wake(a);
            if (b >= 0) Wake(b);
        }
    }

    /// <summary>A sleeping body in the same island as an awake one wakes: the island is awake as a whole.</summary>
    private void WakeTouchedIslands()
    {
        BuildIslands();
        var sims = _sims.AsSpan()[.._proxies.Count];
        var parents = _islandParents.AsSpan();
        var awake = _islandSleepTimes.AsSpan();
        awake.Clear();
        for (var i = 0; i < sims.Length; i++)
        {
            if (sims[i].Type == BodyType.Dynamic && !sims[i].IsSleeping)
                awake[Find(parents, i)] = 1f;
        }

        for (var i = 0; i < sims.Length; i++)
        {
            ref var sim = ref sims[i];
            if (sim.IsSleeping && awake[Find(parents, i)] != 0f)
            {
                sim.IsSleeping = false;
                sim.SleepTime = 0f;
            }
        }
    }

    /// <summary>
    /// Union-find over the contacts between dynamic bodies: each island is the set of bodies that touch each other,
    /// directly or through others. Leaves <see cref="_islandSleepTimes"/> sized for one value per body.
    /// </summary>
    private void BuildIslands()
    {
        var count = _proxies.Count;
        _islandParents.Clear();
        _islandParents.EnsureCapacity(count);
        _islandSleepTimes.Clear();
        _islandSleepTimes.EnsureCapacity(count);
        for (var i = 0; i < count; i++)
        {
            _islandParents.Add(i);
            _islandSleepTimes.Add(0f);
        }

        var parents = _islandParents.AsSpan();
        var sims = _sims.AsSpan();
        var contacts = _contacts.AsSpan();
        for (var i = 0; i < contacts.Length; i++)
        {
            ref readonly var contact = ref contacts[i];
            if (contact.B < count && sims[contact.A].Type == BodyType.Dynamic &&
                sims[contact.B].Type == BodyType.Dynamic)
                Union(parents, contact.A, contact.B);
        }
    }

    /// <summary>Contacts where neither side is awake and moving don't need solving.</summary>
    private void DropSleepingContacts()
    {
        var contacts = _contacts.AsSpan();
        var sims = _sims.AsSpan();
        var kept = 0;
        for (var i = 0; i < contacts.Length; i++)
        {
            ref readonly var contact = ref contacts[i];
            if (IsActive(sims[contact.A]) || IsActive(sims[contact.B]))
            {
                contacts[kept++] = contact;
                continue;
            }

            // Keep the impulses for when the island wakes.
            _pairCache.StoreImpulses(contact.CacheSlot, contact);
        }

        _contacts.Truncate(kept);
    }

    private static bool IsActive(in BodySim sim) => sim.Type != BodyType.Static && !sim.IsSleeping;

    /// <summary>
    /// Counts how long each awake dynamic body has been still. Only when one has been still for
    /// <see cref="TimeToSleep"/> are islands built: an island whose least-still body has been still that long goes to
    /// sleep with zero velocity.
    /// </summary>
    private void UpdateSleep(float deltaSeconds)
    {
        var count = _proxies.Count;
        var sims = _sims.AsSpan()[..count];
        var states = _states.AsSpan();
        var linear = LinearSleepTolerance * LinearSleepTolerance;
        var anyReady = false;
        var sleeping = 0;
        for (var i = 0; i < sims.Length; i++)
        {
            ref var sim = ref sims[i];
            if (sim.Type != BodyType.Dynamic)
                continue;

            if (sim.IsSleeping)
            {
                sleeping++;
                continue;
            }

            ref readonly var state = ref states[i];
            var still = state.Velocity.LengthSquared() <= linear &&
                        MathF.Abs(state.AngularVelocity) <= AngularSleepTolerance;
            sim.SleepTime = still ? sim.SleepTime + deltaSeconds : 0f;
            anyReady |= sim.SleepTime >= TimeToSleep;
        }

        if (anyReady)
            sleeping += SleepReadyIslands();

        SleepingBodyCount = sleeping;
    }

    /// <summary>Puts every island whose bodies have all been still for <see cref="TimeToSleep"/> to sleep.</summary>
    private int SleepReadyIslands()
    {
        BuildIslands();
        var sims = _sims.AsSpan()[.._proxies.Count];
        var states = _states.AsSpan();
        var parents = _islandParents.AsSpan();
        var minimum = _islandSleepTimes.AsSpan();
        minimum.Fill(float.MaxValue);
        for (var i = 0; i < sims.Length; i++)
        {
            if (sims[i].Type == BodyType.Dynamic && !sims[i].IsSleeping)
            {
                ref var island = ref minimum[Find(parents, i)];
                island = MathF.Min(island, sims[i].SleepTime);
            }
        }

        var slept = 0;
        for (var i = 0; i < sims.Length; i++)
        {
            ref var sim = ref sims[i];
            if (sim.Type != BodyType.Dynamic || sim.IsSleeping || minimum[Find(parents, i)] < TimeToSleep)
                continue;

            sim.IsSleeping = true;
            states[i].Velocity = Vector2.Zero;
            states[i].AngularVelocity = 0f;
            slept++;
        }

        return slept;
    }

    private static int Find(Span<int> parents, int body)
    {
        var root = body;
        while (parents[root] != root)
            root = parents[root];

        // Path compression.
        while (parents[body] != root)
        {
            var next = parents[body];
            parents[body] = root;
            body = next;
        }

        return root;
    }

    private static void Union(Span<int> parents, int a, int b)
    {
        var rootA = Find(parents, a);
        var rootB = Find(parents, b);
        if (rootA != rootB)
            parents[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
    }

    //**********************************************************
    //** internals: solve and write back
    //**********************************************************

    private void Solve(float h)
    {
        var states = _states.AsSpan();
        var sims = _sims.AsSpan();
        var contacts = _contacts.AsSpan();
        var inverseH = 1f / h;

        for (var substep = 0; substep < _substepCount; substep++)
        {
            ContactSolver.IntegrateVelocities(states, sims, h);
            ContactSolver.WarmStart(states, contacts);
            ContactSolver.Solve(states, contacts, inverseH, ContactPushMaxSpeed, LinearSlop, useBias: true);
            ContactSolver.IntegratePositions(states, h);
            ContactSolver.Solve(states, contacts, inverseH, ContactPushMaxSpeed, LinearSlop, useBias: false);
        }

        ContactSolver.ApplyRestitution(states, contacts, RestitutionThreshold);

        for (var i = 0; i < contacts.Length; i++)
            _pairCache.StoreImpulses(contacts[i].CacheSlot, contacts[i]);
    }

    /// <summary>
    /// Writes moved bodies back, chunk by chunk in the same order as <see cref="Gather"/>. Nothing can change the
    /// chunk layout in between: structural changes are blocked or deferred until the group's playback.
    /// </summary>
    private void Scatter()
    {
        var states = _states.AsSpan();
        var sims = _sims.AsSpan();
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
            var rigidBodies = chunk.GetSpan<RigidBody>();
            var canTurn = chunk.Has<Rotation>() && chunk.Has<AngularVelocity>();
            var rotations = canTurn ? chunk.GetSpan<Rotation>() : default;
            var spins = canTurn ? chunk.GetSpan<AngularVelocity>() : default;
            for (var i = 0; i < chunk.Count; i++, index++)
            {
                ref readonly var sim = ref sims[index];
                if (sim.Type == BodyType.Static)
                    continue;

                ref readonly var state = ref states[index];
                positions[i].Value += state.DeltaPosition;
                velocities[i].Value = state.Velocity;
                rigidBodies[i].IsSleeping = sim.IsSleeping;
                rigidBodies[i].SleepTime = sim.SleepTime;
                if (!canTurn)
                    continue;

                // Less than half a turn per step, so the angle of (cos, sin) is the whole turn.
                if (state.DeltaRotation.Y != 0f)
                    rotations[i].Radians += MathF.Atan2(state.DeltaRotation.Y, state.DeltaRotation.X);
                spins[i].Value = state.AngularVelocity;
            }
        }
    }

    /// <summary>
    /// Rigid bodies without a collider: the same substeps as the solver (velocity first, then position), without
    /// contacts. They turn if they have a <c>Rotation</c> and an <see cref="AngularVelocity"/>. With no shape they have no
    /// inertia, so a <see cref="Force"/> only pushes them: its torque is ignored.
    /// </summary>
    private void IntegrateBodiesWithoutCollider(float h)
    {
        var gravity = Gravity;
        foreach (var chunk in _bodiesWithoutCollider.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var rigidBodies = chunk.GetSpan<RigidBody>();
            var canTurn = chunk.Has<Rotation>() && chunk.Has<AngularVelocity>();
            var rotations = canTurn ? chunk.GetSpan<Rotation>() : default;
            var spins = canTurn ? chunk.GetSpan<AngularVelocity>() : default;
            var interpolations = chunk.Has<Interpolation>() ? chunk.GetSpan<Interpolation>() : default;
            var forces = chunk.Has<Force>() ? chunk.GetSpan<Force>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var body = ref rigidBodies[i];
                var push = gravity * body.GravityScale;
                if (!forces.IsEmpty)
                    push += forces[i].Value * body.InverseMass;
                if (!interpolations.IsEmpty)
                    RememberTransform(ref interpolations[i], positions[i].Value, canTurn ? rotations[i].Radians : 0f);
                if (body.Type == BodyType.Static)
                    continue;

                for (var s = 0; s < _substepCount; s++)
                {
                    if (body.Type == BodyType.Dynamic)
                    {
                        velocities[i].Value = velocities[i].Value * (1f / (1f + h * body.LinearDamping)) + push * h;
                        if (canTurn && body.AngularDamping != 0f)
                            spins[i].Value *= 1f / (1f + h * body.AngularDamping);
                    }

                    positions[i].Value += velocities[i].Value * h;
                    if (canTurn)
                        rotations[i].Radians += spins[i].Value * h;
                }
            }
        }
    }

    /// <summary>Forces last one step (<see cref="Force"/>), on every entity, whatever kind of body it is.</summary>
    private void ClearForces()
    {
        foreach (var chunk in _forces.GetChunks())
            chunk.GetSpan<Force>().Clear();
    }

    private readonly struct RestingPair(BodyPair pair, int previousSlot)
    {
        public readonly BodyPair Pair = pair;
        public readonly int PreviousSlot = previousSlot;
    }

    /// <summary>Where an interpolated body is before this step: what <see cref="InterpolationSystem"/> draws from.</summary>
    private static void RememberTransform(ref Interpolation interpolation, Vector2 position, float radians)
    {
        interpolation.PreviousPosition = position;
        interpolation.PreviousRadians = radians;
        interpolation.HasPrevious = true;
    }

    /// <summary>A static body's bounding box as the static grid holds it.</summary>
    private readonly record struct StaticBounds(Vector2 Center, Vector2 Extents);
}
