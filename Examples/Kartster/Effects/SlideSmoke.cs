using System;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.Effects;

/// <summary>
/// Grey smoke off a sliding axle, as Kartster's slide smoke (<c>KartParticleEmitters.EmitSlideSmoke</c>): each wheel
/// of an axle sliding past <see cref="SlidingThreshold"/> puffs up to <see cref="RatePerWheel"/> a second, thrown out
/// of the corner and drifting up, growing from 0.25 m to 1.1 m and fading out. A fixed pool of puffs, drawn as
/// camera-facing quads with screen-door transparency; nothing is allocated while driving. One pool for all karts:
/// each frame <see cref="Emit"/> per kart, then <see cref="Advance"/> once.
/// </summary>
public sealed class SlideSmoke
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int MaximumPuffs = 300;
    public const float RatePerWheel = 55f;         // puffs per second at full slide
    public const float SlidingThreshold = 0.1f;    // m/s, KartAxleSlip.SlidingThresholdMetersPerSecond
    private const float FullSlideSlip = 6f;        // m/s above the threshold for the full rate
    private const float StartSize = 0.25f;
    private const float EndSize = 1.1f;
    private const float StartOpacity = 90f / 255f;
    private const float RiseAcceleration = 0.4f;   // m/s², up: the cloud drifts up rather than settling
    private const float Drag = 2.5f;               // per second

    private static readonly Vector3 Color = new Vector3(210, 205, 200) / 255f;

    private readonly Puff[] _puffs = new Puff[MaximumPuffs];
    private readonly float[] _accumulators;
    private readonly Random _random;
    private int _next;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="kartCount">How many karts puff (each wheel keeps its own rate).</param>
    public SlideSmoke(int seed = 1, int kartCount = 1)
    {
        _random = new Random(seed);
        _accumulators = new float[kartCount * KartWheels.Count];
        var indices = new int[MaximumPuffs * 6];
        for (var i = 0; i < MaximumPuffs; i++)
        {
            var v = i * 4;
            indices[i * 6] = v;
            indices[i * 6 + 1] = v + 1;
            indices[i * 6 + 2] = v + 2;
            indices[i * 6 + 3] = v;
            indices[i * 6 + 4] = v + 2;
            indices[i * 6 + 5] = v + 3;
        }

        var colors = new Vector3[MaximumPuffs * 2];
        Array.Fill(colors, Color);
        Mesh = new Mesh(new Vector3[MaximumPuffs * 4], indices, colors)
        {
            TriangleOpacity = new float[MaximumPuffs * 2],
            IsUnlit = true
        };
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Draw it last. Rebuilt by <see cref="BuildQuads"/> each frame.</summary>
    public Mesh Mesh { get; }

    /// <summary>Puffs alive now.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _puffs.Length; i++)
                if (_puffs[i].Age < _puffs[i].Lifetime)
                    count++;
            return count;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>One kart: <see cref="Emit"/>, then <see cref="Advance"/>.</summary>
    public void Update(in KartWheels wheels, Vector3 velocity, Vector3 right, float frontSlip, float rearSlip,
        float seconds)
    {
        Emit(wheels, velocity, right, frontSlip, rearSlip, seconds);
        Advance(seconds);
    }

    /// <summary>Puffs this frame's smoke off one kart's sliding axles.</summary>
    /// <param name="velocity">The kart's velocity, m/s.</param>
    /// <param name="right">The kart's right on the ground, unit length.</param>
    /// <param name="kart">Which kart, 0..kartCount-1.</param>
    public void Emit(in KartWheels wheels, Vector3 velocity, Vector3 right, float frontSlip, float rearSlip,
        float seconds, int kart = 0)
    {
        // Thrown along the kart's right, signed by which way it is sliding, so the cloud leaves the outside.
        var slideDirection = Vector3.Dot(velocity, right) >= 0f ? right : -right;
        for (var wheel = 0; wheel < KartWheels.Count; wheel++)
        {
            var slip = KartWheels.IsFront(wheel) ? frontSlip : rearSlip;
            var rate = Math.Clamp((slip - SlidingThreshold) / FullSlideSlip, 0f, 1f) * RatePerWheel;
            ref var accumulator = ref _accumulators[kart * KartWheels.Count + wheel];
            if (rate <= 0f)
            {
                accumulator = 0f;
                continue;
            }

            // Rates need accumulators: at a high frame rate most frames owe a fraction of a puff.
            accumulator += rate * seconds;
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                Spawn(wheels[wheel], velocity * 0.1f + slideDirection * (slip * 0.3f));
            }
        }
    }

    /// <summary>Moves and ages every puff by <paramref name="seconds"/>.</summary>
    public void Advance(float seconds)
    {
        var drag = 1f / (1f + Drag * seconds);
        for (var i = 0; i < _puffs.Length; i++)
        {
            ref var puff = ref _puffs[i];
            if (puff.Age >= puff.Lifetime)
                continue;

            puff.Age += seconds;
            puff.Velocity = puff.Velocity * drag + new Vector3(0f, RiseAcceleration * seconds, 0f);
            puff.Position += puff.Velocity * seconds;
        }
    }

    /// <summary>Turns every puff into a quad facing the camera. Dead puffs become invisible.</summary>
    /// <param name="cameraRight">The camera's right, unit length.</param>
    /// <param name="cameraUp">The camera's up, unit length.</param>
    public void BuildQuads(Vector3 cameraRight, Vector3 cameraUp)
    {
        var positions = Mesh.Positions;
        var opacity = Mesh.TriangleOpacity;
        for (var i = 0; i < _puffs.Length; i++)
        {
            ref readonly var puff = ref _puffs[i];
            var v = i * 4;
            var age = puff.Lifetime > 0f ? puff.Age / puff.Lifetime : 1f;
            if (age >= 1f)
            {
                opacity[i * 2] = opacity[i * 2 + 1] = 0f;
                continue;
            }

            var half = float.Lerp(StartSize, EndSize, age) / 2f;
            var right = cameraRight * half;
            var up = cameraUp * half;

            // Counter-clockwise as the camera sees it, so it is never culled.
            positions[v] = puff.Position - right - up;
            positions[v + 1] = puff.Position + right - up;
            positions[v + 2] = puff.Position + right + up;
            positions[v + 3] = puff.Position - right + up;
            opacity[i * 2] = opacity[i * 2 + 1] = StartOpacity * (1f - age);
        }

        Mesh.MarkChanged();
    }

    /// <summary>Removes every puff.</summary>
    public void Clear()
    {
        Array.Clear(_puffs);
        Array.Clear(_accumulators);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Spawn(Vector3 wheel, Vector3 launch)
    {
        var scatter = new Vector3(Next(-1f, 1f), Next(-1f, 1f), Next(-1f, 1f));
        if (scatter != Vector3.Zero)
            scatter = Vector3.Normalize(scatter) * Next(0f, 0.4f);

        _puffs[_next] = new Puff
        {
            Position = wheel + new Vector3(0f, 0.1f, 0f),
            Velocity = launch + new Vector3(0f, Next(0.8f, 1.6f), 0f) + scatter,
            Lifetime = Next(0.7f, 1.1f)
        };
        _next = (_next + 1) % MaximumPuffs;
    }

    private float Next(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private struct Puff
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Lifetime;
    }
}
