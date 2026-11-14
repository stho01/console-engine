using System;
using System.Numerics;
using Kartster.Bots;

namespace Kartster.PowerUps;

/// <summary>
/// One kart as the power-ups see it, in the kart frame (<see cref="Driving.KartBody"/>: x = world X, y = world Z,
/// heading and yaw rate positive turning left). <see cref="PowerUpField.Step"/> reads it and writes the shoves back
/// into <see cref="Velocity"/> and <see cref="YawRate"/>.
/// </summary>
public struct PowerUpKart
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Heading;
    public float YawRate;

    /// <summary>World Y of the road under the kart, for drawing what it leaves.</summary>
    public float Height;

    /// <summary>1-based race position; 0: not ranked (then it counts as the leader).</summary>
    public int RacePosition;

    /// <summary>Half the chassis box, metres: the pad pickup test.</summary>
    public float HalfWidth;
    public float HalfLength;

    public readonly Vector2 Forward => new(-MathF.Sin(Heading), -MathF.Cos(Heading));
    public readonly Vector2 Right => new(MathF.Cos(Heading), -MathF.Sin(Heading));
}

/// <summary>A pad: where it is (world axes) and how long until it is back.</summary>
public struct PowerUpPad
{
    public Vector3 Position;
    public float RespawnSeconds;
    public readonly bool IsAvailable => RespawnSeconds <= 0f;
}

/// <summary>Something lying on the road (or thrown and still in the air): a banana or a dropped can.</summary>
public struct PowerUpHazard
{
    /// <summary>The power-up that left it: <see cref="PowerUpKind.Banana"/> or one of the cans.</summary>
    public PowerUpKind Source;
    public Vector2 Position;
    public float Height;

    /// <summary>In the air (thrown): planar velocity, height over the road and vertical speed.</summary>
    public bool InAir;
    public Vector2 Velocity;
    public float AirHeight;
    public float VerticalSpeed;

    public float ArmingSeconds;
    public long Age;
    public readonly bool IsArmed => !InAir && ArmingSeconds <= 0f;
}

/// <summary>A can in flight.</summary>
public struct PowerUpProjectile
{
    public PowerUpKind Source;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Height;
    public float RemainingSeconds;
    public float ArmingSeconds;
    public int Owner;

    /// <summary>The kart a Joika chases (one place ahead of the owner at launch); -1: flies straight.</summary>
    public int Target;
    public int RouteSample;
    public readonly bool IsArmed => ArmingSeconds <= 0f;
}

/// <summary>
/// The power-ups of one race, as pure logic (Kartster's <c>PowerUpSystem</c> with its hazard and projectile
/// simulations): pads that hand out a draw from the normal set weighted by race position, one slot per kart (a reel
/// spinning, then the held power-up and its charges), timed effects (<see cref="KartEffects"/>), bananas and dropped
/// cans on the road, cans in flight. Karts are numbered 0..n-1 (<c>Kart.Index</c>). Each fixed step:
/// <see cref="Step"/> with every kart. Every draw comes from one seeded <see cref="Random"/>, so a race repeats from
/// its seed. Allocates nothing after construction.
/// </summary>
public sealed class PowerUpField
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int HazardCapacity = 64;
    public const int ProjectileCapacity = 32;

    private readonly PowerUpPad[] _pads;
    private readonly Slot[] _slots;
    private readonly PowerUpKind[][] _reels;
    private readonly KartEffects[] _effects;
    private readonly PowerUpAim?[] _requests;
    private readonly int[] _routeSamples;
    private readonly PowerUpHazard[] _hazards = new PowerUpHazard[HazardCapacity];
    private readonly PowerUpProjectile[] _projectiles = new PowerUpProjectile[ProjectileCapacity];
    private readonly Random _random;
    private readonly BotRoutePlan _route;
    private int _hazardCount;
    private int _projectileCount;
    private long _hazardsPlaced;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="pads">Pad centres, world axes (<see cref="PowerUpPads.Load"/>).</param>
    /// <param name="route">
    /// The centerline, for the Joika's road ahead, route gaps for the bots and the skid's side; null: a Joika flies
    /// straight at its target, gaps are straight distances.
    /// </param>
    public PowerUpField(ReadOnlySpan<Vector3> pads, int kartCount, int seed, BotRoutePlan route = null)
    {
        _pads = new PowerUpPad[pads.Length];
        for (var i = 0; i < pads.Length; i++)
            _pads[i].Position = pads[i];
        _slots = new Slot[kartCount];
        _reels = new PowerUpKind[kartCount][];
        _effects = new KartEffects[kartCount];
        _requests = new PowerUpAim?[kartCount];
        _routeSamples = new int[kartCount];
        for (var i = 0; i < kartCount; i++)
        {
            _reels[i] = new PowerUpKind[PowerUpCatalog.ReelLength];
            _effects[i] = new KartEffects();
            _routeSamples[i] = -1;
        }

        _random = new Random(seed);
        _route = route;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int KartCount => _slots.Length;
    public ReadOnlySpan<PowerUpPad> Pads => _pads;
    public ReadOnlySpan<PowerUpHazard> Hazards => _hazards.AsSpan(0, _hazardCount);
    public ReadOnlySpan<PowerUpProjectile> Projectiles => _projectiles.AsSpan(0, _projectileCount);

    /// <summary>False (the countdown): presses do nothing, as Kartster's use button during the countdown.</summary>
    public bool CanUse { get; set; } = true;

    /// <summary>Square the karts drive in (world X, Z): a can that leaves it is gone. Default: no edge.</summary>
    public Vector2 MapCenter { get; set; }
    public float MapHalfSize { get; set; } = float.MaxValue;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>What kart <paramref name="kart"/> holds once its reel has landed; None while it spins or is empty.</summary>
    public PowerUpKind Held(int kart) => _slots[kart].IsSpinning ? PowerUpKind.None : _slots[kart].Held;

    public int Charges(int kart) => _slots[kart].Charges;

    public bool IsSpinning(int kart) => _slots[kart].IsSpinning;

    /// <summary>A reel spinning or something held: the kart takes no pad.</summary>
    public bool IsHolding(int kart) => _slots[kart].IsSpinning || _slots[kart].Held != PowerUpKind.None;

    /// <summary>What the slot shows: the reel's face while it spins, else what is held.</summary>
    public PowerUpKind Showing(int kart)
    {
        ref readonly var slot = ref _slots[kart];
        return slot.IsSpinning ? _reels[kart][slot.FaceIndex % PowerUpCatalog.ReelLength] : slot.Held;
    }

    public KartEffects Effects(int kart) => _effects[kart];

    /// <summary>Puts <paramref name="kind"/> into the slot with its charges (what a reel lands on; tests).</summary>
    public void Give(int kart, PowerUpKind kind)
    {
        ref var slot = ref _slots[kart];
        slot.IsSpinning = false;
        slot.Held = kind;
        slot.Charges = PowerUpCatalog.Charges(kind);
    }

    /// <summary>
    /// A press of use, carried out in the next <see cref="Step"/>: stops a spinning reel on the face it shows, or
    /// fires one charge of what is held (<paramref name="aim"/>: Kartster's T throws ahead, G drops behind).
    /// </summary>
    public void RequestUse(int kart, PowerUpAim aim = PowerUpAim.Default) => _requests[kart] = aim;

    /// <summary>
    /// Metres along the route from kart <paramref name="from"/> to kart <paramref name="to"/>, the nearer way round a
    /// closed track (negative: behind). Without a route, the straight distance along <paramref name="from"/>'s heading.
    /// </summary>
    public float RouteGap(ReadOnlySpan<PowerUpKart> karts, int from, int to)
    {
        if (_route == null || _routeSamples[from] < 0 || _routeSamples[to] < 0)
            return Vector2.Dot(karts[to].Position - karts[from].Position, karts[from].Forward);

        var length = _route.Count * _route.Spacing;
        var gap = (_routeSamples[to] - _routeSamples[from]) * _route.Spacing;
        if (gap > length / 2f) gap -= length;
        if (gap < -length / 2f) gap += length;
        return gap;
    }

    /// <summary>The route sample nearest kart <paramref name="kart"/> after the last step; -1 without a route.</summary>
    public int RouteSample(int kart) => _routeSamples[kart];

    /// <summary>
    /// One fixed step: the presses since the last one (on what the slot showed then), then in Kartster's order effects
    /// count down, reels land, hazards and cans move and hit, pads recharge and karts with an empty slot take the ones
    /// they touch.
    /// </summary>
    public void Step(float seconds, Span<PowerUpKart> karts)
    {
        CarryOutPresses(karts);
        for (var k = 0; k < karts.Length; k++)
        {
            _effects[k].Update(seconds);
            if (_route != null)
                _routeSamples[k] = _route.Locate(karts[k].Position, _routeSamples[k]);
        }

        AdvanceReels(seconds);
        UpdateHazards(seconds, karts);
        UpdateProjectiles(seconds, karts);
        UpdatePads(seconds, karts);
    }

    //**********************************************************
    //** internals: slots
    //**********************************************************

    private void AdvanceReels(float seconds)
    {
        for (var k = 0; k < _slots.Length; k++)
        {
            ref var slot = ref _slots[k];
            if (!slot.IsSpinning)
                continue;

            slot.SpinSeconds += seconds;
            if (slot.SpinSeconds >= PowerUpCatalog.SpinSeconds)
            {
                Give(k, Showing(k));
                continue;
            }

            // Holds its speed and gives it up late, on the square of the progress (Kartster's AdvanceSpins).
            slot.UntilNextFace -= seconds;
            if (slot.UntilNextFace <= 0f)
            {
                slot.FaceIndex++;
                var progress = slot.SpinSeconds / PowerUpCatalog.SpinSeconds;
                slot.UntilNextFace = float.Lerp(PowerUpCatalog.FastFaceSeconds, PowerUpCatalog.SlowFaceSeconds,
                    progress * progress);
            }
        }
    }

    private void Draw(int kart, ReadOnlySpan<PowerUpKart> karts)
    {
        var count = karts.Length;
        var position = karts[kart].RacePosition;
        // A lone kart, or one not ranked yet, is the leader (Kartster's RacePositions).
        var normalized = count > 1 && position > 0 ? (position - 1) / (float)(count - 1) : 0f;
        var reel = _reels[kart];
        for (var i = 0; i < reel.Length; i++)
            reel[i] = PowerUpCatalog.Roll(normalized, _random);

        ref var slot = ref _slots[kart];
        slot = new Slot { IsSpinning = true, UntilNextFace = PowerUpCatalog.FastFaceSeconds };
    }

    private void CarryOutPresses(Span<PowerUpKart> karts)
    {
        for (var k = 0; k < _requests.Length; k++)
        {
            if (_requests[k] is not { } aim)
                continue;

            _requests[k] = null;
            if (!CanUse)
                continue;

            ref var slot = ref _slots[k];
            if (slot.IsSpinning)
                Give(k, Showing(k)); // the press stops the reel and is spent choosing
            else if (slot.Held != PowerUpKind.None)
                Fire(k, slot.Held, aim, karts);
        }
    }

    private void Fire(int kart, PowerUpKind kind, PowerUpAim aim, Span<PowerUpKart> karts)
    {
        ref var slot = ref _slots[kart];
        if (--slot.Charges <= 0)
            slot.Held = PowerUpKind.None;

        ref var self = ref karts[kart];
        var effects = _effects[kart];
        switch (kind)
        {
            case PowerUpKind.SpeedBoost:
                self.Velocity += self.Forward * PowerUpCatalog.BoostImpulse;
                effects.Add(kind, EffectChannel.EnginePower, PowerUpCatalog.BoostPower, PowerUpCatalog.BoostSeconds);
                effects.Add(kind, EffectChannel.TyreGrip, PowerUpCatalog.BoostGrip, PowerUpCatalog.BoostSeconds);
                break;
            case PowerUpKind.EngineTune:
                effects.Add(kind, EffectChannel.EnginePower, PowerUpCatalog.EngineTunePower, PowerUpCatalog.TuneSeconds);
                break;
            case PowerUpKind.GripTune:
                effects.Add(kind, EffectChannel.TyreGrip, PowerUpCatalog.GripTuneGrip, PowerUpCatalog.TuneSeconds);
                break;
            case PowerUpKind.Banana when aim == PowerUpAim.Forward:
                Throw(self);
                break;
            case PowerUpKind.Banana:
                Place(kind, self.Position - self.Forward * PowerUpCatalog.DropDistance, self.Height,
                    PowerUpCatalog.BananaArmingSeconds);
                break;
            default: // the cans: fired ahead, or dropped behind to lie on the road
                if (aim == PowerUpAim.Backward)
                    Place(kind, self.Position - self.Forward * PowerUpCatalog.DropDistance, self.Height,
                        PowerUpCatalog.DroppedCanArmingSeconds);
                else
                    Launch(kart, kind, karts);
                break;
        }
    }

    //**********************************************************
    //** internals: hazards
    //**********************************************************

    private ref PowerUpHazard AddHazard()
    {
        // shortcut: a fixed pool; when it is full the oldest hazard goes (Kartster's list has no limit).
        var index = _hazardCount;
        if (_hazardCount < HazardCapacity)
        {
            _hazardCount++;
        }
        else
        {
            index = 0;
            for (var i = 1; i < _hazardCount; i++)
            {
                if (_hazards[i].Age < _hazards[index].Age)
                    index = i;
            }
        }

        _hazards[index] = new PowerUpHazard { Age = _hazardsPlaced++ };
        return ref _hazards[index];
    }

    private void Place(PowerUpKind source, Vector2 position, float height, float arming)
    {
        ref var hazard = ref AddHazard();
        hazard.Source = source;
        hazard.Position = position;
        hazard.Height = height;
        hazard.ArmingSeconds = arming;
    }

    /// <summary>A banana thrown ahead (Kartster's <c>ThrowSimulation</c>, flat road): armed from the landing.</summary>
    private void Throw(in PowerUpKart self)
    {
        ref var hazard = ref AddHazard();
        hazard.Source = PowerUpKind.Banana;
        hazard.Position = self.Position + self.Forward * PowerUpCatalog.ThrowAhead;
        hazard.Height = self.Height;
        hazard.InAir = true;
        hazard.Velocity = self.Velocity + self.Forward * PowerUpCatalog.ThrowSpeed;
        hazard.AirHeight = PowerUpCatalog.ThrowHeight;
        hazard.VerticalSpeed = PowerUpCatalog.ThrowUpSpeed;
        hazard.ArmingSeconds = PowerUpCatalog.BananaArmingSeconds;
    }

    private void UpdateHazards(float seconds, Span<PowerUpKart> karts)
    {
        for (var h = _hazardCount - 1; h >= 0; h--)
        {
            ref var hazard = ref _hazards[h];
            if (hazard.InAir)
            {
                hazard.Position += hazard.Velocity * seconds;
                hazard.VerticalSpeed -= PowerUpCatalog.Gravity * seconds;
                hazard.AirHeight += hazard.VerticalSpeed * seconds;
                if (hazard.AirHeight <= 0f)
                    hazard.InAir = false; // it hits nobody in the air
                continue;
            }

            hazard.ArmingSeconds = MathF.Max(0f, hazard.ArmingSeconds - seconds);
            if (!hazard.IsArmed)
                continue;

            // Nobody is exempt from their own: arming is the only protection. The first kart inside uses it up.
            var radius = hazard.Source == PowerUpKind.Banana ? PowerUpCatalog.BananaRadius : PowerUpCatalog.DroppedCanRadius;
            for (var k = 0; k < karts.Length; k++)
            {
                if (Vector2.DistanceSquared(karts[k].Position, hazard.Position) > radius * radius)
                    continue;

                if (hazard.Source == PowerUpKind.Banana)
                {
                    Spin(ref karts[k], PowerUpCatalog.BananaSpin);
                    _effects[k].Add(PowerUpKind.Banana, EffectChannel.TyreGrip, PowerUpCatalog.BananaGrip,
                        PowerUpCatalog.BananaGripSeconds, diminishes: true);
                }
                else
                {
                    karts[k].Velocity = Vector2.Zero;
                    Spin(ref karts[k], PowerUpCatalog.CanSpin);
                    _effects[k].Add(hazard.Source, EffectChannel.TyreGrip, PowerUpCatalog.CanGrip,
                        PowerUpCatalog.CanGripSeconds, diminishes: true);
                }

                _hazards[h] = _hazards[--_hazardCount];
                break;
            }
        }
    }

    /// <summary>Adds yaw rate the way the kart is turning, left when it goes straight (Kartster's spin-out).</summary>
    private static void Spin(ref PowerUpKart kart, float radiansPerSecond) =>
        kart.YawRate += (kart.YawRate >= 0f ? 1f : -1f) * radiansPerSecond;

    //**********************************************************
    //** internals: cans in flight
    //**********************************************************

    private void Launch(int owner, PowerUpKind kind, ReadOnlySpan<PowerUpKart> karts)
    {
        if (_projectileCount >= ProjectileCapacity)
            return; // shortcut: a fixed pool; a can fired with 32 already in the air is lost

        var self = karts[owner];
        var homing = PowerUpCatalog.IsHoming(kind);
        var target = -1;
        if (homing && self.RacePosition > 1)
        {
            for (var k = 0; k < karts.Length; k++)
            {
                if (karts[k].RacePosition == self.RacePosition - 1)
                    target = k;
            }
        }

        _projectiles[_projectileCount++] = new PowerUpProjectile
        {
            Source = kind,
            Position = self.Position + self.Forward * PowerUpCatalog.CanLaunchOffset,
            Velocity = self.Forward * (homing ? PowerUpCatalog.JoikaSpeed : PowerUpCatalog.CanSpeed),
            Height = self.Height,
            RemainingSeconds = homing ? PowerUpCatalog.JoikaSeconds : PowerUpCatalog.CanSeconds,
            ArmingSeconds = PowerUpCatalog.CanArmingSeconds,
            Owner = owner,
            Target = target,
            RouteSample = _routeSamples[owner],
        };
    }

    private void UpdateProjectiles(float seconds, Span<PowerUpKart> karts)
    {
        for (var p = _projectileCount - 1; p >= 0; p--)
        {
            ref var can = ref _projectiles[p];
            can.RemainingSeconds -= seconds;
            if (can.RemainingSeconds <= 0f)
            {
                _projectiles[p] = _projectiles[--_projectileCount];
                continue;
            }

            can.ArmingSeconds = MathF.Max(0f, can.ArmingSeconds - seconds);
            if (can.Target >= 0)
                Steer(ref can, karts[can.Target].Position, seconds);

            var from = can.Position;
            can.Position += can.Velocity * seconds;
            var offMap = MathF.Abs(can.Position.X - MapCenter.X) > MapHalfSize
                         || MathF.Abs(can.Position.Y - MapCenter.Y) > MapHalfSize;
            var hit = can.IsArmed ? FirstHit(from, can.Position, karts) : -1;
            if (hit >= 0)
                Hit(can, ref karts[hit], hit, karts);
            if (hit >= 0 || offMap)
                _projectiles[p] = _projectiles[--_projectileCount];
        }
    }

    /// <summary>
    /// Turns a Joika towards its target, keeping its speed: along the road (12 m ahead of itself) while the target is
    /// more than 25 m away, then straight at it (Kartster's <c>ProjectileSimulation.Steer</c>).
    /// </summary>
    private void Steer(ref PowerUpProjectile can, Vector2 target, float seconds)
    {
        var aim = target;
        if (_route != null && Vector2.DistanceSquared(target, can.Position)
            > PowerUpCatalog.DirectChaseMeters * PowerUpCatalog.DirectChaseMeters)
        {
            can.RouteSample = _route.Locate(can.Position, can.RouteSample);
            aim = _route.PointAhead(can.RouteSample, PowerUpCatalog.RoadLookAheadMeters, 0f);
        }

        can.Velocity = TurnToward(can.Velocity, aim - can.Position, PowerUpCatalog.JoikaTurn * seconds);
    }

    /// <summary><paramref name="velocity"/> turned towards <paramref name="direction"/>, at most by the angle.</summary>
    public static Vector2 TurnToward(Vector2 velocity, Vector2 direction, float maximumRadians)
    {
        var speed = velocity.Length();
        if (speed <= 1e-6f || direction.LengthSquared() <= 1e-8f)
            return velocity;

        var from = MathF.Atan2(velocity.Y, velocity.X);
        var to = MathF.Atan2(direction.Y, direction.X);
        var turn = MathF.IEEERemainder(to - from, MathF.Tau);
        var angle = from + Math.Clamp(turn, -maximumRadians, maximumRadians);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
    }

    /// <summary>
    /// The kart nearest along the segment the can covered this step, within its radius: swept, so a fast can never
    /// passes through a kart between two steps. The owner too, once armed.
    /// </summary>
    private static int FirstHit(Vector2 from, Vector2 to, ReadOnlySpan<PowerUpKart> karts)
    {
        var segment = to - from;
        var lengthSquared = MathF.Max(segment.LengthSquared(), 1e-12f);
        var nearest = float.MaxValue;
        var hit = -1;
        for (var k = 0; k < karts.Length; k++)
        {
            var along = Math.Clamp(Vector2.Dot(karts[k].Position - from, segment) / lengthSquared, 0f, 1f);
            var closest = from + segment * along;
            if (Vector2.DistanceSquared(karts[k].Position, closest) > PowerUpCatalog.CanRadius * PowerUpCatalog.CanRadius
                || along >= nearest)
                continue;

            nearest = along;
            hit = k;
        }

        return hit;
    }

    /// <summary>
    /// Keeps 40% of the speed, spins it, shoves it 8 m/s across the can's path away from the shooter (straight
    /// behind: the side of the road it is on) and takes most of its grip for 1.5 s.
    /// </summary>
    private void Hit(in PowerUpProjectile can, ref PowerUpKart kart, int index, ReadOnlySpan<PowerUpKart> karts)
    {
        var path = Vector2.Normalize(can.Velocity);
        var across = new Vector2(-path.Y, path.X);
        var fromShooter = Vector2.Dot(kart.Position - karts[can.Owner].Position, across);
        if (MathF.Abs(fromShooter) < PowerUpCatalog.SkidSideMeters || index == can.Owner)
        {
            var center = _route != null && _routeSamples[index] >= 0
                ? _route.PointAhead(_routeSamples[index], 0f, 0f)
                : kart.Position - across;
            fromShooter = Vector2.Dot(kart.Position - center, across);
        }

        kart.Velocity = kart.Velocity * PowerUpCatalog.CanHitSpeedKept
                        + across * (fromShooter >= 0f ? PowerUpCatalog.CanSkid : -PowerUpCatalog.CanSkid);
        Spin(ref kart, PowerUpCatalog.CanSpin);
        _effects[index].Add(can.Source, EffectChannel.TyreGrip, PowerUpCatalog.CanGrip, PowerUpCatalog.CanGripSeconds,
            diminishes: true);
    }

    //**********************************************************
    //** internals: pads
    //**********************************************************

    private void UpdatePads(float seconds, ReadOnlySpan<PowerUpKart> karts)
    {
        for (var p = 0; p < _pads.Length; p++)
            _pads[p].RespawnSeconds = MathF.Max(0f, _pads[p].RespawnSeconds - seconds);

        for (var k = 0; k < karts.Length; k++)
        {
            for (var p = 0; p < _pads.Length; p++)
            {
                // Per pad: in a row the first fills the slot and the rest stay for the next kart.
                if (!_pads[p].IsAvailable || IsHolding(k) || !Touches(karts[k], _pads[p].Position))
                    continue;

                _pads[p].RespawnSeconds = PowerUpCatalog.RespawnSeconds;
                Draw(k, karts);
            }
        }
    }

    /// <summary>The pad's pickup circle overlaps the kart's chassis box, seen from above.</summary>
    private static bool Touches(in PowerUpKart kart, Vector3 pad)
    {
        var offset = new Vector2(pad.X, pad.Z) - kart.Position;
        var across = Math.Clamp(Vector2.Dot(offset, kart.Right), -kart.HalfWidth, kart.HalfWidth);
        var along = Math.Clamp(Vector2.Dot(offset, kart.Forward), -kart.HalfLength, kart.HalfLength);
        var closest = kart.Right * across + kart.Forward * along;
        return Vector2.DistanceSquared(offset, closest) <= PowerUpCatalog.PickUpRadius * PowerUpCatalog.PickUpRadius;
    }

    private struct Slot
    {
        public PowerUpKind Held;
        public int Charges;
        public bool IsSpinning;
        public float SpinSeconds;
        public float UntilNextFace;
        public int FaceIndex;
    }
}
