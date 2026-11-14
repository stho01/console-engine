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
/// copies every kart (<c>Components.Kart</c>, its body, its <see cref="RaceProgress"/> position) into the field, lets
/// each <see cref="Bot"/> decide (<see cref="BotPowerUps"/>), steps the field and writes the shoves back into
/// <c>Velocity</c> and <c>AngularVelocity</c>. Presses are held during the race countdown. Allocates nothing per step.
/// </summary>
[UpdateAfter(typeof(KartGroundSystem))]
public sealed class PowerUpSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly KartPhysicsSystem _physics;
    private readonly RaceSession _race;
    private readonly BotRoutePlan _route;
    private readonly BotPowerUps _bots;
    private readonly PowerUpKart[] _karts;
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
        _physics = physics;
        _race = race;
        _route = route;
        _bots = new BotPowerUps(field.KartCount);
        _karts = new PowerUpKart[field.KartCount];
        for (var i = 0; i < _karts.Length; i++)
        {
            var tuning = physics.GetDynamics(i).Tuning;
            _karts[i].HalfWidth = tuning.ChassisWidthMeters / 2f;
            _karts[i].HalfLength = tuning.ChassisLengthMeters / 2f;
        }
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
    /// (<see cref="KartPhysicsSystem.Effects"/>), this system in <paramref name="fixedStep"/> (add it after the driving
    /// and race systems) and the player's keys (<see cref="PowerUpInputSystem"/>) in <paramref name="frame"/>.
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

    protected override void OnCreate() =>
        _query = World.Query(Query.With<Kart, Position, Rotation>().With<Velocity, AngularVelocity>());

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
            var progress = chunk.Has<RaceProgress>() ? chunk.GetSpan<RaceProgress>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var kart = ref _karts[karts[i].Index];
                kart.Position = new(positions[i].Value.X, positions[i].Value.Y);
                kart.Velocity = new(velocities[i].Value.X, velocities[i].Value.Y);
                kart.Heading = KartFrame.ToHeading(rotations[i].Radians);
                kart.YawRate = -angularVelocities[i].Value;
                kart.Height = karts[i].Height;
                kart.RacePosition = progress.IsEmpty ? 0 : progress[i].Position;
            }
        }

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
    }
}
