using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Race;
using Kartster.Systems;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.PowerUps;

/// <summary>
/// Runs the <see cref="PowerUpField"/> in the fixed step, after the karts have moved and the race has ranked them:
/// puts every kart's real mass back (<see cref="PowerUpContactSystem"/>), copies every kart (<c>Components.Kart</c>,
/// its body, its <see cref="RaceProgress"/> position) and every roadblock's body into the field, lets each
/// <see cref="Bot"/> decide (<see cref="BotPowerUps"/>), steps the field, writes the shoves back into
/// <c>Velocity</c> and <c>AngularVelocity</c> and gives each new roadblock a body (plastic: 45 kg, sliding to a stop
/// at 6 m/s²; concrete: static), removing the ones that are gone. Presses are held during the race countdown.
/// Allocates nothing per step (a roadblock's body is created when it is put down).
/// </summary>
[UpdateAfter(typeof(KartGroundSystem))]
public sealed class PowerUpSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly RaceSession _race;
    private readonly BotRoutePlan _route;
    private readonly BotPowerUps _bots;
    private readonly PowerUpKart[] _karts;
    private readonly float[] _inverseMasses;
    private readonly Entity[] _obstacleBodies = new Entity[PowerUpField.ObstacleCapacity];
    private readonly long[] _obstacleIds = new long[PowerUpField.ObstacleCapacity];
    private Query _query;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="route">The centerline (see <see cref="PowerUpField"/>); null: none.</param>
    /// <param name="race">Null: presses always count.</param>
    public PowerUpSystem(PowerUpField field, KartPhysicsSystem physics, BotRoutePlan route = null,
        RaceSession race = null)
    {
        Field = field;
        _race = race;
        _route = route;
        _bots = new BotPowerUps(field.KartCount);
        _karts = new PowerUpKart[field.KartCount];
        _inverseMasses = new float[field.KartCount];
        for (var i = 0; i < _karts.Length; i++)
        {
            var tuning = physics.GetDynamics(i).Tuning;
            _karts[i].HalfWidth = tuning.ChassisWidthMeters / 2f;
            _karts[i].HalfLength = tuning.ChassisLengthMeters / 2f;
            _inverseMasses[i] = 1f / tuning.Mass;
        }

        Array.Fill(_obstacleIds, -1L);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public PowerUpField Field { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The race's power-ups, in one call: a <see cref="PowerUpField"/> on <paramref name="track"/>'s pads (seeded by
    /// <paramref name="seed"/>, the clock when null), its effects on the <paramref name="physics"/>
    /// (<see cref="KartPhysicsSystem.Effects"/>), the steering and contact systems round the kart model, this system
    /// in <paramref name="fixedStep"/> (add it after the driving and race systems) and the player's keys
    /// (<see cref="PowerUpInputSystem"/>) in <paramref name="frame"/>.
    /// </summary>
    public static PowerUpSystem Add(SystemGroup fixedStep, SystemGroup frame, Track track, KartPhysicsSystem physics,
        RaceSession race = null, int? seed = null, Func<Key, bool> isPressed = null)
    {
        var route = new BotRoutePlan(track, physics.GetDynamics(0).Tuning, BotDriverSystem.CornerSpeedFactor(0.5f),
            BotDriverSystem.BrakingShare(0.5f));
        var field = new PowerUpField(track.PowerUpPads, physics.KartCount, seed ?? Environment.TickCount, route)
        {
            MapCenter = new System.Numerics.Vector2(track.GroundCenter.X, track.GroundCenter.Z),
            MapHalfSize = track.GroundHalfSize,
        };
        physics.Effects = new KartEffectTuning(field);
        var steering = fixedStep.Add(new PowerUpSteeringSystem(field));
        fixedStep.Add(new PowerUpContactSystem(field, steering));
        var system = new PowerUpSystem(field, physics, route, race);
        fixedStep.Add(system);
        frame?.Add(new PowerUpInputSystem(field, isPressed));
        return system;
    }

    /// <summary>The slot as the HUD shows it: the reel's face or what is held, null when empty.</summary>
    public string HudName(int kart) => PowerUpCatalog.Name(Field.Showing(kart));

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _query = World.Query(Query.With<Kart, Position, Rotation>()
        .With<Velocity, AngularVelocity>().With<RigidBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        Field.CanUse = _race is not { CanDrive: false };
        foreach (var chunk in _query.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.GetSpan<Rotation>();
            var velocities = chunk.GetSpan<Velocity>();
            var angularVelocities = chunk.GetSpan<AngularVelocity>();
            var bodies = chunk.GetSpan<RigidBody>();
            var progress = chunk.Has<RaceProgress>() ? chunk.GetSpan<RaceProgress>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                var index = karts[i].Index;
                bodies[i].InverseMass = _inverseMasses[index];
                ref var kart = ref _karts[index];
                kart.Position = new(positions[i].Value.X, positions[i].Value.Y);
                kart.Velocity = new(velocities[i].Value.X, velocities[i].Value.Y);
                kart.Heading = KartFrame.ToHeading(rotations[i].Radians);
                kart.YawRate = -angularVelocities[i].Value;
                kart.Height = karts[i].Height;
                kart.RacePosition = progress.IsEmpty ? 0 : progress[i].Position;
            }
        }

        ReadObstacles(deltaSeconds);
        foreach (var chunk in _query.GetChunks())
        {
            if (!chunk.Has<Bot>())
                continue;

            var karts = chunk.GetSpan<Kart>();
            for (var i = 0; i < chunk.Count; i++)
                _bots.Update(Field, _karts, karts[i].Index, _route, deltaSeconds);
        }

        Field.Step(deltaSeconds, _karts);

        foreach (var chunk in _query.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var velocities = chunk.GetSpan<Velocity>();
            var angularVelocities = chunk.GetSpan<AngularVelocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var kart = ref _karts[karts[i].Index];
                velocities[i].Value = new XnaVector2(kart.Velocity.X, kart.Velocity.Y);
                angularVelocities[i].Value = -kart.YawRate;
            }
        }

        WriteObstacles();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Copies each roadblock body's motion into the field, slowing a plastic one at 6 m/s² and damping its turn
    /// (Kartster's <c>ObstacleSimulation.Step</c>).
    /// </summary>
    private void ReadObstacles(float seconds)
    {
        var obstacles = Field.Obstacles;
        for (var o = 0; o < obstacles.Length; o++)
        {
            ref var block = ref obstacles[o];
            var body = _obstacleBodies[o];
            if (!block.IsActive || _obstacleIds[o] != block.Id || !World.IsAlive(body))
                continue;

            block.Position = new(World.Get<Position>(body).Value.X, World.Get<Position>(body).Value.Y);
            block.Heading = KartFrame.ToHeading(World.Get<Rotation>(body).Radians);
            if (block.Kind != PowerUpKind.Roadblock)
                continue;

            ref var velocity = ref World.Get<Velocity>(body).Value;
            var speed = velocity.Length();
            velocity = speed > 1e-4f
                ? velocity * (MathF.Max(0f, speed - PowerUpCatalog.RoadblockSlideDeceleration * seconds) / speed)
                : XnaVector2.Zero;
            World.Get<AngularVelocity>(body).Value *= MathF.Max(0f, 1f - PowerUpCatalog.RoadblockSpinDamping * seconds);
            block.Velocity = new(velocity.X, velocity.Y);
        }
    }

    /// <summary>A body for each block put down this step; the bodies of blocks that are gone are removed.</summary>
    private void WriteObstacles()
    {
        var obstacles = Field.Obstacles;
        for (var o = 0; o < obstacles.Length; o++)
        {
            ref readonly var block = ref obstacles[o];
            var current = block.IsActive && _obstacleIds[o] == block.Id;
            if (current)
                continue;

            if (World.IsAlive(_obstacleBodies[o]))
                World.Destroy(_obstacleBodies[o]);
            _obstacleBodies[o] = Entity.Null;
            _obstacleIds[o] = -1;
            if (block.IsActive)
            {
                _obstacleBodies[o] = CreateBody(block);
                _obstacleIds[o] = block.Id;
            }
        }
    }

    private Entity CreateBody(in PowerUpObstacle block)
    {
        var position = new Position(new XnaVector2(block.Position.X, block.Position.Y));
        var rotation = new Rotation(KartFrame.ToRadians(block.Heading));
        var half = new XnaVector2(block.Half.X, block.Half.Y);
        if (block.Kind == PowerUpKind.ConcreteRoadblock)
        {
            return World.Create(position, rotation,
                Collider.Box(half) with { Restitution = PowerUpCatalog.ConcreteRestitution, Friction = 0.3f });
        }

        return World.Create(position, rotation, new Velocity(XnaVector2.Zero), new AngularVelocity(0f),
            RigidBody.Dynamic(PowerUpCatalog.RoadblockMass, gravityScale: 0f),
            Collider.Box(half) with { Restitution = PowerUpCatalog.RoadblockRestitution, Friction = 0.3f });
    }
}
