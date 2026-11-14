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

/// <summary>
/// Something lying on the road (or thrown and still in the air): a banana, a dropped can, a fake pad, a smoke bomb,
/// or a roadblock in the air (it becomes a <see cref="PowerUpObstacle"/> where it lands).
/// </summary>
public struct PowerUpHazard
{
    /// <summary>The power-up that left it.</summary>
    public PowerUpKind Source;
    public Vector2 Position;
    public float Height;

    /// <summary>The way it was thrown, kart heading: a thrown roadblock lands across it.</summary>
    public float Heading;

    /// <summary>In the air (thrown): planar velocity, height over the road and vertical speed.</summary>
    public bool InAir;
    public Vector2 Velocity;
    public float AirHeight;
    public float VerticalSpeed;

    public float ArmingSeconds;

    /// <summary>Seconds left on the road (a smoke bomb's 10.8 s; the rest stay until hit).</summary>
    public float RemainingSeconds;

    /// <summary>A smoke bomb has blasted; now it is only a cloud.</summary>
    public bool HasGoneOff;
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
/// A roadblock on the road, a box turned across <see cref="Heading"/>. The <see cref="PowerUpSystem"/> gives each a
/// physics body (plastic: 45 kg; concrete: fixed) and copies its motion back; the field only crashes karts into it.
/// </summary>
public struct PowerUpObstacle
{
    public PowerUpKind Kind;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Heading;
    public float Height;
    public float RemainingSeconds;

    /// <summary>Changes whenever the slot gets a new block, so a body can tell it is no longer the same one.</summary>
    public long Id;
    public bool IsActive;
    public readonly Vector2 Half => PowerUpCatalog.ObstacleHalf(Kind);
}

/// <summary>A fake finish line painted across the road: only drawn.</summary>
public struct PowerUpDecoy
{
    public Vector2 Position;
    public Vector2 Forward;
    public float Height;
    public float LeftWidth;
    public float RightWidth;
    public long Id;
}

/// <summary>A locomotive and its cart running along the route.</summary>
public struct PowerUpTrain
{
    public int Owner;

    /// <summary>+1 ahead, -1 back down the track.</summary>
    public int Direction;

    /// <summary>Route metres of the engine's nose.</summary>
    public float Along;

    /// <summary>The owner's lane at the start, metres right; eased to 0 over the first 60 m.</summary>
    public float StartLateral;
    public float RunMeters;
    public float Seconds;
    public float Height;
    public bool HasPassed;
    public float LastGap;
    public float FadeSeconds;
    public int HitMask;
    public readonly bool IsFading => FadeSeconds > 0f;

    public readonly float Lateral => StartLateral * MathF.Max(0f, 1f - RunMeters / PowerUpCatalog.TrainJoinLineMeters);
}

/// <summary>
/// The power-ups of one race, as pure logic (Kartster's <c>PowerUpSystem</c> with its hazard, projectile, throw,
/// obstacle, magnet and locomotive simulations): pads that hand out a draw from the normal set weighted by race
/// position, one slot per kart (a reel spinning, then the held power-up and its charges), timed effects
/// (<see cref="KartEffects"/>), what lies on the road, cans in flight, trains. Karts are numbered 0..n-1
/// (<c>Kart.Index</c>). Each fixed step: <see cref="Step"/> with every kart. Every draw comes from one seeded
/// <see cref="Random"/>, so a race repeats from its seed. Allocates nothing after construction.
/// </summary>
public sealed class PowerUpField
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int HazardCapacity = 64;
    public const int ProjectileCapacity = 32;
    public const int ObstacleCapacity = 16;
    public const int DecoyCapacity = 8;
    public const int TrainCapacity = 4;

    private readonly PowerUpPad[] _pads;
    private readonly Slot[] _slots;
    private readonly PowerUpKind[][] _reels;
    private readonly KartEffects[] _effects;
    private readonly PowerUpAim?[] _requests;
    private readonly int[] _routeSamples;
    private readonly PowerUpKart[] _karts;
    private readonly PowerUpHazard[] _hazards = new PowerUpHazard[HazardCapacity];
    private readonly PowerUpProjectile[] _projectiles = new PowerUpProjectile[ProjectileCapacity];
    private readonly PowerUpObstacle[] _obstacles = new PowerUpObstacle[ObstacleCapacity];
    private readonly float[] _crashCooldowns;
    private readonly PowerUpDecoy[] _decoys = new PowerUpDecoy[DecoyCapacity];
    private readonly PowerUpTrain[] _trains = new PowerUpTrain[TrainCapacity];
    private readonly Random _random;
    private readonly Random _steeringRandom;
    private readonly BotRoutePlan _route;
    private int _hazardCount;
    private int _projectileCount;
    private int _decoyCount;
    private int _trainCount;
    private long _placed;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="pads">Pad centres, world axes (<see cref="PowerUpPads.Load"/>).</param>
    /// <param name="route">
    /// The centerline, for the Joika's road ahead, route gaps for the bots, the skid's side, the fake finish's road
    /// and the locomotive's line; null: a Joika flies straight at its target, gaps are straight distances, and a fake
    /// finish or a locomotive is spent with nothing (Kartster: "needs the track under the kart").
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
        _karts = new PowerUpKart[kartCount];
        _crashCooldowns = new float[ObstacleCapacity * kartCount];
        for (var i = 0; i < kartCount; i++)
        {
            _reels[i] = new PowerUpKind[PowerUpCatalog.ReelLength];
            _effects[i] = new KartEffects();
            _routeSamples[i] = -1;
        }

        _random = new Random(seed);
        _steeringRandom = new Random(seed ^ 0x5EED);
        _route = route;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int KartCount => _slots.Length;
    public ReadOnlySpan<PowerUpPad> Pads => _pads;
    public ReadOnlySpan<PowerUpHazard> Hazards => _hazards.AsSpan(0, _hazardCount);
    public ReadOnlySpan<PowerUpProjectile> Projectiles => _projectiles.AsSpan(0, _projectileCount);

    /// <summary>Every obstacle slot; only those with <see cref="PowerUpObstacle.IsActive"/> are on the road.</summary>
    public Span<PowerUpObstacle> Obstacles => _obstacles;
    public ReadOnlySpan<PowerUpDecoy> Decoys => _decoys.AsSpan(0, _decoyCount);
    public ReadOnlySpan<PowerUpTrain> Trains => _trains.AsSpan(0, _trainCount);

    /// <summary>The karts as the last <see cref="Step"/> left them (for drawing).</summary>
    public ReadOnlySpan<PowerUpKart> Karts => _karts;

    /// <summary>False (the countdown): presses do nothing, as Kartster's use button during the countdown.</summary>
    public bool CanUse { get; set; } = true;

    /// <summary>Square the karts drive in (world X, Z): a can that leaves it is gone. Default: no edge.</summary>
    public Vector2 MapCenter { get; set; }
    public float MapHalfSize { get; set; } = float.MaxValue;

    public BotRoad Road => _route?.Road;

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
    /// The steering kart <paramref name="kart"/>'s model gets for <paramref name="steer"/> (-1..1): times the glitch's
    /// -1, plus the earthquake's noise, a fresh draw each call (Kartster's <c>ApplyInputModifiers</c>).
    /// </summary>
    public float Steer(int kart, float steer)
    {
        var effects = _effects[kart];
        var scale = effects.SteeringScale;
        var noise = effects.SteeringNoise;
        if (scale == 1f && noise <= 0f)
            return steer;

        var steering = steer * scale;
        if (noise > 0f)
            steering += ((float)_steeringRandom.NextDouble() - 0.5f) * 2f * noise;
        return Math.Clamp(steering, -1f, 1f);
    }

    /// <summary>
    /// How many times its own mass kart <paramref name="kart"/> is in a contact: invulnerable,
    /// <see cref="PowerUpCatalog.ImmovableMass"/>; with an anvil, 4; else 1 (Kartster's <c>KartContactMass</c>).
    /// </summary>
    public float ContactMass(int kart)
    {
        var effects = _effects[kart];
        if (effects.IsInvulnerable)
            return PowerUpCatalog.ImmovableMass;

        var mass = effects.Mass;
        return mass > 0f ? mass : 1f;
    }

    /// <summary>The smoke cloud's radius <paramref name="hazard"/> (a gone-off smoke bomb), Kartster's
    /// <c>SmokeClouds.GetRadius</c>: a third of 9 m billowing out over 1.5 s, then spreading slowly.</summary>
    public static float SmokeCloudRadius(in PowerUpHazard hazard)
    {
        var age = SmokeAge(hazard);
        if (age < 0f)
            return 0f;

        var t = MathF.Min(age / 1.5f, 1f);
        var billow = t * t * (3f - 2f * t);
        return PowerUpCatalog.SmokeRadius * (0.35f + 0.55f * billow + 0.2f * age / PowerUpCatalog.SmokeSeconds);
    }

    /// <summary>The cloud's density, thinning to nothing over its last 3 s.</summary>
    public static float SmokeDensity(in PowerUpHazard hazard) =>
        SmokeAge(hazard) < 0f ? 0f : Math.Clamp(hazard.RemainingSeconds / 3f, 0f, 1f);

    /// <summary>
    /// How much of the view at <paramref name="eye"/> (world X, Z) the smoke takes, 0..0.85, Kartster's
    /// <c>SmokeClouds.GetCover</c> (a dark wash over the picture).
    /// </summary>
    public float SmokeCover(Vector2 eye)
    {
        var cover = 0f;
        foreach (ref readonly var hazard in Hazards)
        {
            if (hazard.Source != PowerUpKind.SmokeBomb)
                continue;

            var radius = SmokeCloudRadius(hazard);
            if (radius <= 0f)
                continue;

            var inside = Math.Clamp((1f - Vector2.Distance(eye, hazard.Position) / radius) * 2.5f, 0f, 1f);
            cover = MathF.Max(cover, PowerUpCatalog.SmokeDarkness * SmokeDensity(hazard) * inside);
        }

        return cover;
    }

    /// <summary>
    /// One fixed step: the presses since the last one (on what the slot showed then), then in Kartster's order effects
    /// count down, reels land, the magnet pulls, hazards, obstacles, cans and trains move and hit, pads recharge and
    /// karts with an empty slot take the ones they touch.
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
        Pull(seconds, karts);
        UpdateHazards(seconds, karts);
        UpdateObstacles(seconds, karts);
        UpdateProjectiles(seconds, karts);
        UpdateTrains(seconds, karts);
        UpdatePads(seconds, karts);
        karts.CopyTo(_karts);
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
            case PowerUpKind.Invulnerable:
                effects.Add(kind, EffectChannel.Invulnerable, 1f, PowerUpCatalog.InvulnerableSeconds);
                break;
            case PowerUpKind.Magnet:
                effects.Add(kind, EffectChannel.MagnetPull, PowerUpCatalog.MagnetPull, PowerUpCatalog.MagnetSeconds);
                break;
            case PowerUpKind.Anvil:
                effects.Add(kind, EffectChannel.Mass, PowerUpCatalog.AnvilMass, PowerUpCatalog.AnvilSeconds);
                break;
            case PowerUpKind.Vampire:
                Vampire(kart, karts);
                break;
            case PowerUpKind.Lightning or PowerUpKind.Earthquake or PowerUpKind.Glitch:
                StrikeOthers(kart, kind);
                break;
            case PowerUpKind.FakeFinish:
                PaintFinish(self);
                break;
            case PowerUpKind.Locomotive:
                SendTrain(kart, aim == PowerUpAim.Backward ? -1 : 1, karts);
                break;
            case not PowerUpKind.None when PowerUpCatalog.IsDropped(kind):
                if (aim == PowerUpAim.Forward)
                    Throw(self, kind);
                else if (PowerUpCatalog.IsObstacle(kind))
                    PlaceObstacle(kind, self.Position - self.Forward * PowerUpCatalog.ObstacleDropDistance,
                        self.Heading, self.Height);
                else
                    Place(kind, self.Position - self.Forward * PowerUpCatalog.DropDistance, self.Height);
                break;
            default: // the cans: fired ahead, or dropped behind to lie on the road
                if (aim == PowerUpAim.Backward)
                    Place(kind, self.Position - self.Forward * PowerUpCatalog.DropDistance, self.Height);
                else
                    Launch(kart, kind, karts);
                break;
        }
    }

    //**********************************************************
    //** internals: effects on others
    //**********************************************************

    /// <summary>Kartster's <c>ApplyToTargets</c> with <c>AllOpponents</c>. Not blocked by invulnerability.</summary>
    private void StrikeOthers(int kart, PowerUpKind kind)
    {
        for (var k = 0; k < _effects.Length; k++)
        {
            if (k == kart)
                continue;

            var effects = _effects[k];
            switch (kind)
            {
                case PowerUpKind.Lightning:
                    effects.Add(kind, EffectChannel.Shrink, PowerUpCatalog.LightningShrink,
                        PowerUpCatalog.LightningSeconds);
                    effects.Add(kind, EffectChannel.TopSpeed, PowerUpCatalog.LightningTopSpeed,
                        PowerUpCatalog.LightningSeconds);
                    break;
                case PowerUpKind.Earthquake:
                    effects.Add(kind, EffectChannel.SteeringNoise, PowerUpCatalog.EarthquakeSteeringNoise,
                        PowerUpCatalog.DisruptionSeconds);
                    break;
                default:
                    effects.Add(kind, EffectChannel.SteeringScale, PowerUpCatalog.GlitchSteeringScale,
                        PowerUpCatalog.DisruptionSeconds);
                    break;
            }
        }
    }

    /// <summary>The kart one place ahead loses a quarter of its power and the user gains it; the leader gets nothing.
    /// </summary>
    private void Vampire(int kart, ReadOnlySpan<PowerUpKart> karts)
    {
        var target = AtPosition(karts, karts[kart].RacePosition - 1);
        if (target < 0)
            return; // spent with nothing: there is nobody ahead to take from

        _effects[target].Add(PowerUpKind.Vampire, EffectChannel.EnginePower, PowerUpCatalog.VampireDrain,
            PowerUpCatalog.VampireSeconds);
        _effects[kart].Add(PowerUpKind.Vampire, EffectChannel.EnginePower, PowerUpCatalog.VampireGain,
            PowerUpCatalog.VampireSeconds, selfHalf: true);
    }

    /// <summary>The kart at race position <paramref name="position"/>; -1 when there is none.</summary>
    private static int AtPosition(ReadOnlySpan<PowerUpKart> karts, int position)
    {
        if (position < 1)
            return -1;

        for (var k = 0; k < karts.Length; k++)
        {
            if (karts[k].RacePosition == position)
                return k;
        }

        return -1;
    }

    /// <summary>Every magnet pulls every kart within 26 m but the invulnerable (Kartster's <c>MagnetSimulation</c>).
    /// </summary>
    private void Pull(float seconds, Span<PowerUpKart> karts)
    {
        for (var m = 0; m < karts.Length; m++)
        {
            var pull = _effects[m].MagnetPull;
            if (pull <= 0f)
                continue;

            for (var k = 0; k < karts.Length; k++)
            {
                var toMagnet = karts[m].Position - karts[k].Position;
                var distanceSquared = toMagnet.LengthSquared();
                if (k == m || _effects[k].IsInvulnerable || distanceSquared <= 1e-6f
                    || distanceSquared > PowerUpCatalog.MagnetRadius * PowerUpCatalog.MagnetRadius)
                    continue;

                karts[k].Velocity += toMagnet / MathF.Sqrt(distanceSquared) * pull * seconds;
            }
        }
    }

    //**********************************************************
    //** internals: hazards
    //**********************************************************

    private ref PowerUpHazard AddHazard(PowerUpKind source)
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

        _hazards[index] = new PowerUpHazard
        {
            Source = source,
            Age = _placed++,
            ArmingSeconds = ArmingSeconds(source),
            RemainingSeconds = source == PowerUpKind.SmokeBomb ? PowerUpCatalog.SmokeSeconds : float.MaxValue,
        };
        return ref _hazards[index];
    }

    private static float ArmingSeconds(PowerUpKind source) => source switch
    {
        PowerUpKind.Banana => PowerUpCatalog.BananaArmingSeconds,
        PowerUpKind.SmokeBomb => PowerUpCatalog.SmokeFuseSeconds,
        PowerUpKind.FakePowerUp => PowerUpCatalog.FakePadArmingSeconds,
        _ => PowerUpCatalog.DroppedCanArmingSeconds,
    };

    private static float Radius(PowerUpKind source) => source switch
    {
        PowerUpKind.Banana => PowerUpCatalog.BananaRadius,
        PowerUpKind.FakePowerUp => PowerUpCatalog.FakePadRadius,
        _ => PowerUpCatalog.DroppedCanRadius,
    };

    private void Place(PowerUpKind source, Vector2 position, float height)
    {
        ref var hazard = ref AddHazard(source);
        hazard.Position = position;
        hazard.Height = height;
    }

    /// <summary>
    /// Something thrown ahead (Kartster's <c>ThrowSimulation</c>, flat road): it hits nobody in the air and becomes
    /// the real thing where it lands, armed from the landing.
    /// </summary>
    private void Throw(in PowerUpKart self, PowerUpKind kind)
    {
        ref var hazard = ref AddHazard(kind);
        hazard.Position = self.Position + self.Forward * PowerUpCatalog.ThrowAhead;
        hazard.Height = self.Height;
        hazard.Heading = self.Heading;
        hazard.InAir = true;
        hazard.Velocity = self.Velocity + self.Forward * PowerUpCatalog.ThrowSpeed;
        hazard.AirHeight = PowerUpCatalog.ThrowHeight;
        hazard.VerticalSpeed = PowerUpCatalog.ThrowUpSpeed;
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
                    Land(h);
                continue;
            }

            hazard.RemainingSeconds -= seconds;
            if (hazard.RemainingSeconds <= 0f)
            {
                RemoveHazard(h);
                continue;
            }

            hazard.ArmingSeconds = MathF.Max(0f, hazard.ArmingSeconds - seconds);
            if (!hazard.IsArmed)
                continue;

            if (hazard.Source == PowerUpKind.SmokeBomb)
            {
                if (!hazard.HasGoneOff)
                    Blast(hazard.Position, karts);
                hazard.HasGoneOff = true;
                continue; // from now on only a cloud
            }

            // Nobody is exempt from their own: arming is the only protection. The first kart inside uses it up. An
            // invulnerable kart is skipped, so what it drives over stays armed for whoever is behind.
            var radius = Radius(hazard.Source);
            for (var k = 0; k < karts.Length; k++)
            {
                if (_effects[k].IsInvulnerable
                    || Vector2.DistanceSquared(karts[k].Position, hazard.Position) > radius * radius)
                    continue;

                if (hazard.Source == PowerUpKind.Banana)
                {
                    Spin(ref karts[k], PowerUpCatalog.BananaSpin);
                    _effects[k].Add(PowerUpKind.Banana, EffectChannel.TyreGrip, PowerUpCatalog.BananaGrip,
                        PowerUpCatalog.BananaGripSeconds, diminishes: true);
                }
                else
                {
                    // A can lying on the road, or a fake pad: Kartster's CanStopImpact and the can's grip loss.
                    karts[k].Velocity = Vector2.Zero;
                    Spin(ref karts[k], PowerUpCatalog.CanSpin);
                    _effects[k].Add(hazard.Source, EffectChannel.TyreGrip, PowerUpCatalog.CanGrip,
                        PowerUpCatalog.CanGripSeconds, diminishes: true);
                }

                RemoveHazard(h);
                break;
            }
        }
    }

    private void RemoveHazard(int index) => _hazards[index] = _hazards[--_hazardCount];

    /// <summary>A thrown thing comes down: a roadblock becomes a block across the throw; the rest lie there.</summary>
    private void Land(int index)
    {
        ref var hazard = ref _hazards[index];
        hazard.InAir = false;
        hazard.Velocity = Vector2.Zero;
        if (!PowerUpCatalog.IsObstacle(hazard.Source))
            return;

        PlaceObstacle(hazard.Source, hazard.Position, hazard.Heading, hazard.Height);
        RemoveHazard(index);
    }

    /// <summary>
    /// A smoke bomb going off (Kartster's <c>HazardSimulation.GoOff</c>, without the hop): every kart within 8 m but
    /// the invulnerable, harder the nearer the middle.
    /// </summary>
    private void Blast(Vector2 center, Span<PowerUpKart> karts)
    {
        for (var k = 0; k < karts.Length; k++)
        {
            var away = karts[k].Position - center;
            var distance = away.Length();
            if (_effects[k].IsInvulnerable || distance > PowerUpCatalog.SmokeBlastRadius)
                continue;

            var strength = 1f - distance / PowerUpCatalog.SmokeBlastRadius;
            ref var kart = ref karts[k];
            kart.Velocity *= float.Lerp(1f, PowerUpCatalog.SmokeBlastSpeedKept, strength);
            Spin(ref kart, PowerUpCatalog.SmokeBlastSpin * strength);
            if (distance > 0.1f)
                kart.Velocity += away / distance * PowerUpCatalog.SmokeBlastShove * strength;
            _effects[k].Add(PowerUpKind.SmokeBomb, EffectChannel.TyreGrip, PowerUpCatalog.SmokeBlastGrip,
                PowerUpCatalog.SmokeBlastGripSeconds, diminishes: true);
        }
    }

    private static float SmokeAge(in PowerUpHazard hazard) =>
        PowerUpCatalog.SmokeSeconds - hazard.RemainingSeconds - PowerUpCatalog.SmokeFuseSeconds;

    /// <summary>Adds yaw rate the way the kart is turning, left when it goes straight (Kartster's spin-out).</summary>
    private static void Spin(ref PowerUpKart kart, float radiansPerSecond) =>
        kart.YawRate += (kart.YawRate >= 0f ? 1f : -1f) * radiansPerSecond;

    //**********************************************************
    //** internals: roadblocks and the fake finish
    //**********************************************************

    private void PlaceObstacle(PowerUpKind kind, Vector2 position, float heading, float height)
    {
        // shortcut: 16 slots; when they are full the oldest block goes (Kartster's list has no limit).
        var index = 0;
        for (var i = 0; i < _obstacles.Length; i++)
        {
            if (!_obstacles[i].IsActive)
            {
                index = i;
                break;
            }

            if (_obstacles[i].Id < _obstacles[index].Id)
                index = i;
        }

        _obstacles[index] = new PowerUpObstacle
        {
            Kind = kind,
            Position = position,
            Heading = heading,
            Height = height,
            RemainingSeconds = kind == PowerUpKind.Roadblock ? PowerUpCatalog.RoadblockSeconds : float.MaxValue,
            Id = _placed++,
            IsActive = true,
        };
        _crashCooldowns.AsSpan(index * KartCount, KartCount).Clear();
    }

    /// <summary>
    /// Counts the blocks down and crashes a kart about to hit one faster than 4 m/s, once a second (Kartster's
    /// <c>CrashImpact</c>): the field runs after the physics, so "about to" is the gap closing within the next step.
    /// The body the <see cref="PowerUpSystem"/> gives it does the rest.
    /// </summary>
    private void UpdateObstacles(float seconds, Span<PowerUpKart> karts)
    {
        for (var o = 0; o < _obstacles.Length; o++)
        {
            ref var block = ref _obstacles[o];
            if (!block.IsActive)
                continue;

            block.RemainingSeconds -= seconds;
            if (block.RemainingSeconds <= 0f)
            {
                block.IsActive = false;
                continue;
            }

            var kept = block.Kind == PowerUpKind.ConcreteRoadblock ? 0f : PowerUpCatalog.RoadblockCrashSpeedKept;
            for (var k = 0; k < karts.Length; k++)
            {
                ref var cooldown = ref _crashCooldowns[o * KartCount + k];
                cooldown = MathF.Max(0f, cooldown - seconds);
                ref var kart = ref karts[k];
                if (cooldown > 0f || _effects[k].IsInvulnerable)
                    continue;

                var gap = Separation(kart, block.Position, block.Heading, block.Half, out var normal);
                var closing = Vector2.Dot(kart.Velocity - block.Velocity, normal);
                if (closing <= PowerUpCatalog.CrashSpeed || gap > closing * seconds * 1.5f + 0.05f)
                    continue;

                kart.Velocity *= kept;
                cooldown = PowerUpCatalog.CrashCooldownSeconds;
            }
        }
    }

    /// <summary>
    /// The gap between a kart's box and a box at <paramref name="position"/> turned to <paramref name="heading"/>
    /// (half size <paramref name="half"/>: across, along; negative: overlapping), by the separating axes of both
    /// boxes, and the axis that gives it, pointing from the kart to the box.
    /// </summary>
    private static float Separation(in PowerUpKart kart, Vector2 position, float heading, Vector2 half,
        out Vector2 normal)
    {
        var forward = new Vector2(-MathF.Sin(heading), -MathF.Cos(heading));
        var right = new Vector2(MathF.Cos(heading), -MathF.Sin(heading));
        var offset = position - kart.Position;
        var best = float.MinValue;
        normal = Vector2.Zero;
        Span<Vector2> axes = [kart.Right, kart.Forward, right, forward];
        foreach (var axis in axes)
        {
            var reach = MathF.Abs(Vector2.Dot(kart.Right, axis)) * kart.HalfWidth
                        + MathF.Abs(Vector2.Dot(kart.Forward, axis)) * kart.HalfLength
                        + MathF.Abs(Vector2.Dot(right, axis)) * half.X + MathF.Abs(Vector2.Dot(forward, axis)) * half.Y;
            var along = Vector2.Dot(offset, axis);
            var gap = MathF.Abs(along) - reach;
            if (gap <= best)
                continue;

            best = gap;
            normal = along >= 0f ? axis : -axis;
        }

        return best;
    }

    /// <summary>A finish line painted across the road under the kart; off the road (or no route) it is spent.</summary>
    private void PaintFinish(in PowerUpKart self)
    {
        var road = Road;
        if (road == null)
            return;

        var sample = road.Locate(self.Position, -1);
        var (_, lateral) = road.ToRoute(self.Position, sample);
        if (lateral < -road.LeftWidth(sample) || lateral > road.RightWidth(sample))
            return;

        var index = _decoyCount < DecoyCapacity ? _decoyCount++ : (int)(_placed % DecoyCapacity);
        var forward = road.ForwardAt(sample);
        _decoys[index] = new PowerUpDecoy
        {
            Position = self.Position - new Vector2(-forward.Y, forward.X) * lateral,
            Forward = forward,
            Height = self.Height,
            LeftWidth = road.LeftWidth(sample),
            RightWidth = road.RightWidth(sample),
            Id = _placed++,
        };
    }

    //**********************************************************
    //** internals: cans in flight
    //**********************************************************

    private void Launch(int owner, PowerUpKind kind, ReadOnlySpan<PowerUpKart> karts)
    {
        if (_projectileCount >= ProjectileCapacity)
            return; // shortcut: a fixed pool; a can fired with 32 already in the air is lost

        var self = karts[owner];
        var homing = PowerUpCatalog.IsHoming(kind);
        var target = homing ? AtPosition(karts, self.RacePosition - 1) : -1;
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
    /// passes through a kart between two steps. The owner too, once armed; never an invulnerable kart, which it flies
    /// through.
    /// </summary>
    private int FirstHit(Vector2 from, Vector2 to, ReadOnlySpan<PowerUpKart> karts)
    {
        var segment = to - from;
        var lengthSquared = MathF.Max(segment.LengthSquared(), 1e-12f);
        var nearest = float.MaxValue;
        var hit = -1;
        for (var k = 0; k < karts.Length; k++)
        {
            var along = Math.Clamp(Vector2.Dot(karts[k].Position - from, segment) / lengthSquared, 0f, 1f);
            var closest = from + segment * along;
            if (_effects[k].IsInvulnerable
                || Vector2.DistanceSquared(karts[k].Position, closest) > PowerUpCatalog.CanRadius * PowerUpCatalog.CanRadius
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
    //** internals: the locomotive
    //**********************************************************

    /// <summary>
    /// A train ahead (<paramref name="direction"/> 1) or back: its near end 4 m from the kart, in the kart's lane.
    /// Without a route, or with all slots running, the charge is spent with nothing.
    /// </summary>
    private void SendTrain(int owner, int direction, ReadOnlySpan<PowerUpKart> karts)
    {
        var road = Road;
        if (road == null || _trainCount >= TrainCapacity)
            return;

        var self = karts[owner];
        var sample = _routeSamples[owner] >= 0 ? _routeSamples[owner] : road.Locate(self.Position, -1);
        var (along, lateral) = road.ToRoute(self.Position, sample);
        _trains[_trainCount++] = new PowerUpTrain
        {
            Owner = owner,
            Direction = direction,
            Along = along + direction * (PowerUpCatalog.TrainStartClearance + PowerUpCatalog.TrainLength),
            StartLateral = lateral,
            Height = self.Height,
            FadeSeconds = Target(owner, direction, karts) < 0 ? PowerUpCatalog.TrainFadeSeconds : 0f,
            LastGap = float.MinValue,
        };
    }

    /// <summary>
    /// What a train runs past: ahead, the leader; back, the last kart; the owner left out, so -1 when it is that kart.
    /// </summary>
    private static int Target(int owner, int direction, ReadOnlySpan<PowerUpKart> karts)
    {
        var mine = karts[owner].RacePosition;
        if (direction > 0 ? mine <= 1 : mine >= karts.Length || mine <= 0)
            return -1;

        var best = -1;
        for (var k = 0; k < karts.Length; k++)
        {
            if (k == owner || karts[k].RacePosition <= 0)
                continue;

            if (best < 0 || (direction > 0
                    ? karts[k].RacePosition < karts[best].RacePosition
                    : karts[k].RacePosition > karts[best].RacePosition))
                best = k;
        }

        return best;
    }

    private void UpdateTrains(float seconds, Span<PowerUpKart> karts)
    {
        var road = Road;
        for (var t = _trainCount - 1; t >= 0; t--)
        {
            ref var train = ref _trains[t];
            train.Seconds += seconds;
            train.Along += train.Direction * PowerUpCatalog.TrainSpeed * seconds;
            train.RunMeters += PowerUpCatalog.TrainSpeed * seconds;
            if (train.IsFading)
            {
                train.FadeSeconds -= seconds;
                if (train.FadeSeconds <= 0f)
                    _trains[t] = _trains[--_trainCount];
                continue;
            }

            if (train.Seconds >= PowerUpCatalog.TrainSeconds || HasPassed(ref train, road, karts)
                || (!road.IsClosed && (train.Along < 0f || train.Along > road.Length)))
            {
                train.FadeSeconds = PowerUpCatalog.TrainFadeSeconds;
                continue;
            }

            for (var k = 0; k < karts.Length; k++)
            {
                if ((train.HitMask & (1 << k)) != 0 || _effects[k].IsInvulnerable
                    || !TrainHits(train, road, karts[k], out var forward, out var center))
                    continue;

                // Kartster's Impact (0, 4, 0.3, 6) without the hop, then the shove along and away from its side.
                train.HitMask |= 1 << k;
                ref var kart = ref karts[k];
                var right = new Vector2(-forward.Y, forward.X);
                kart.Velocity = kart.Velocity * PowerUpCatalog.TrainSpeedKept + forward * PowerUpCatalog.TrainShoveAlong
                                + right * (Vector2.Dot(kart.Position - center, right) >= 0f ? 1f : -1f)
                                * PowerUpCatalog.TrainShoveAside;
                Spin(ref kart, PowerUpCatalog.TrainSpin);
            }
        }
    }

    /// <summary>
    /// Whether the train's tail is 40 m past its target: the gap, the nearer way round, has to go from ahead of the
    /// tail to behind it by a small step, so the jump at half a lap is not a pass.
    /// </summary>
    private bool HasPassed(ref PowerUpTrain train, BotRoad road, ReadOnlySpan<PowerUpKart> karts)
    {
        var target = Target(train.Owner, train.Direction, karts);
        if (target < 0)
            return true;

        var kartAlong = road.ToRoute(karts[target].Position, _routeSamples[target] >= 0
            ? _routeSamples[target]
            : road.Locate(karts[target].Position, -1)).Along;
        var tail = train.Along - train.Direction * PowerUpCatalog.TrainLength;
        var gap = train.Direction * road.Gap(kartAlong, tail);
        if (!train.HasPassed && train.LastGap <= 0f && gap > 0f && gap - train.LastGap < 10f)
            train.HasPassed = true;
        train.LastGap = gap;
        return train.HasPassed && gap >= PowerUpCatalog.TrainPassMargin;
    }

    /// <summary>The engine or the cart (route-aligned boxes) touches the kart's box.</summary>
    private static bool TrainHits(in PowerUpTrain train, BotRoad road, in PowerUpKart kart, out Vector2 forward,
        out Vector2 center)
    {
        var engine = train.Along - train.Direction * PowerUpCatalog.EngineLength / 2f;
        var cart = train.Along - train.Direction
            * (PowerUpCatalog.EngineLength + PowerUpCatalog.CouplingGap + PowerUpCatalog.CartLength / 2f);
        return CarHits(train, road, kart, engine, PowerUpCatalog.EngineLength / 2f, out forward, out center)
               || CarHits(train, road, kart, cart, PowerUpCatalog.CartLength / 2f, out forward, out center);
    }

    private static bool CarHits(in PowerUpTrain train, BotRoad road, in PowerUpKart kart, float along, float halfLength,
        out Vector2 forward, out Vector2 center)
    {
        CarPose(train, road, along, out center, out forward);
        return Separation(kart, center, MathF.Atan2(-forward.X, -forward.Y),
            new Vector2(PowerUpCatalog.TrainHalfWidth, halfLength), out _) <= 0f;
    }

    /// <summary>Where a car whose middle is <paramref name="along"/> route metres stands, and the way it runs.</summary>
    public static void CarPose(in PowerUpTrain train, BotRoad road, float along, out Vector2 center,
        out Vector2 forward)
    {
        var lateral = train.Lateral;
        center = road.PointAhead(0, along, lateral);
        var ahead = road.PointAhead(0, along + train.Direction * 2f, lateral);
        var direction = ahead - center;
        forward = direction.LengthSquared() > 1e-8f ? Vector2.Normalize(direction) : road.ForwardAt(0);
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
