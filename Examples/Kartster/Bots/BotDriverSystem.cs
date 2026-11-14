using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Systems;

namespace Kartster.Bots;

/// <summary>
/// Drives every <see cref="Bot"/> kart by writing its <see cref="KartControls"/>, a simple form of Kartster's built-in
/// bots (Earl to Merlin, <c>Docs/built-in-bots.md</c>):
/// <list type="bullet">
/// <item>Steering: pure pursuit at a point on its lane (<see cref="Bot.LaneOffset"/> right of the centerline)
/// <see cref="LookaheadSeconds"/> ahead at its speed (at least <see cref="MinimumLookahead"/>), the arc's wheel angle
/// over the lock at that speed, ramped like the keyboard (<see cref="KartInputSystem.RampSteer"/>).</item>
/// <item>Speed: the lower of its <see cref="BotRoutePlan"/> speed here and <see cref="SpeedLeadSeconds"/> ahead; full
/// throttle (times <see cref="ThrottleCap"/>) 2 m/s under it, full brake 3 m/s over it, linear between.</item>
/// <item>Off the road for longer than its reaction time: aims <see cref="RejoinAimMeters"/> ahead at the centerline, no
/// faster than <see cref="RejoinSpeed"/>. Aim point behind (spun): full lock towards it, at most 6 m/s.</item>
/// <item>Stuck (throttle and under 1 m/s for 1.5 s): reverses 1.2 s with the steering the other way.</item>
/// </list>
/// <see cref="Bot.Skill"/> (0..1) moves every number from Earl's towards Merlin's. Runs in the fixed step, before the
/// <see cref="KartPhysicsSystem"/>: it reads only what the physics changes, so a bot drives the same at any frame rate
/// and a headless test drives exactly as the game does; a decision costs a few hundred nanoseconds.
/// </summary>
[UpdateBefore(typeof(KartPhysicsSystem))]
public sealed class BotDriverSystem(Track track, KartPhysicsSystem physics) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MinimumLookahead = 5f;

    /// <summary>How far ahead it reads the speed it should be at, seconds (Kartster's 0.25).</summary>
    public const float SpeedLeadSeconds = 0.25f;

    /// <summary>How far ahead it aims to rejoin the road, metres.</summary>
    public const float RejoinAimMeters = 12f;

    private const float FullThrottleUnderSpeed = 2f;
    private const float FullBrakeOverSpeed = 3f;
    private const float AimBehindSpeed = 6f;
    private const float StuckSeconds = 1.5f;
    private const float ReverseSeconds = 1.2f;
    private const float StuckSpeed = 1f;
    private const float StuckThrottle = 0.3f;

    private BotRoutePlan[] _plans;
    private Query _bots;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Most throttle given, by skill: Earl-like 0.85 to 1.</summary>
    public static float ThrottleCap(float skill) => float.Lerp(0.85f, 1f, skill);

    /// <summary>Share of the grip-limited corner speed taken (Earl 0.7, Merlin 0.82).</summary>
    public static float CornerSpeedFactor(float skill) => float.Lerp(0.7f, 0.85f, skill);

    /// <summary>Share of full braking planned with (Earl 0.6, Merlin 0.7). Lower brakes earlier.</summary>
    public static float BrakingShare(float skill) => float.Lerp(0.6f, 0.75f, skill);

    /// <summary>How far ahead it aims, seconds at its speed (Earl 0.6, Merlin 0.5).</summary>
    public static float LookaheadSeconds(float skill) => float.Lerp(0.6f, 0.45f, skill);

    /// <summary>Time off the road before the bot notices (Earl 0.8, Merlin 0.4).</summary>
    public static float ReactionSeconds(float skill) => float.Lerp(0.8f, 0.4f, skill);

    /// <summary>Fastest it goes back onto the road, m/s (Earl 8, Merlin 14).</summary>
    public static float RejoinSpeed(float skill) => float.Lerp(8f, 14f, skill);

    /// <summary>The plan of the bot in kart slot <paramref name="index"/>, made on its first step.</summary>
    public BotRoutePlan GetPlan(int index) => _plans[index];

    /// <summary>
    /// What a bot does this step: <paramref name="controls"/> from its kart's <paramref name="body"/>, the
    /// <paramref name="surface"/> under it and its <paramref name="plan"/>; updates <paramref name="bot"/>.
    /// </summary>
    public static void Drive(ref Bot bot, ref KartControls controls, in KartBody body, Surface surface,
        KartTuning tuning, BotRoutePlan plan, float seconds)
    {
        var forward = body.Forward;
        var right = body.Right;
        var speed = Vector2.Dot(body.Velocity, forward);

        var sample = plan.Locate(body.Position, bot.Sample);
        if (bot.Sample >= 0)
            bot.Progress += plan.StepsBetween(bot.Sample, sample) * plan.Spacing;
        bot.Sample = sample;

        bot.OffRoadSeconds = surface == Surface.Asphalt ? 0f : bot.OffRoadSeconds + seconds;
        var rejoining = bot.OffRoadSeconds > ReactionSeconds(bot.Skill);

        var absoluteSpeed = MathF.Abs(speed);
        var lookahead = rejoining
            ? RejoinAimMeters
            : MathF.Max(MinimumLookahead, LookaheadSeconds(bot.Skill) * absoluteSpeed);
        var toTarget = plan.PointAhead(sample, lookahead, rejoining ? 0f : bot.LaneOffset) - body.Position;
        var targetSpeed = MathF.Min(plan.SpeedAhead(sample, 0f),
            plan.SpeedAhead(sample, SpeedLeadSeconds * absoluteSpeed));
        if (rejoining)
            targetSpeed = MathF.Min(targetSpeed, RejoinSpeed(bot.Skill));

        var across = Vector2.Dot(toTarget, right);
        var along = Vector2.Dot(toTarget, forward);
        float steer;
        if (along <= 0f)
        {
            // Behind: pure pursuit has no useful arc. Full lock towards it, slowly.
            steer = across >= 0f ? 1f : -1f;
            targetSpeed = MathF.Min(targetSpeed, AimBehindSpeed);
        }
        else
        {
            var alpha = MathF.Atan2(across, along); // positive = target to the right
            var wheelAngle = MathF.Atan(2f * tuning.WheelbaseMeters * MathF.Sin(alpha) / toTarget.Length());
            var lockAngle = float.DegreesToRadians(KartDynamics.GetSteerAngleDegrees(tuning, speed));
            steer = Math.Clamp(wheelAngle / lockAngle, -1f, 1f);
        }

        var throttle = Math.Clamp((targetSpeed - speed) / FullThrottleUnderSpeed, 0f, 1f) * ThrottleCap(bot.Skill);
        var brake = Math.Clamp((speed - targetSpeed) / FullBrakeOverSpeed, 0f, 1f);

        if (bot.ReverseSeconds > 0f)
        {
            bot.ReverseSeconds -= seconds;
            throttle = 0f;
            brake = 1f; // brake at a standstill is reverse
            steer = -steer; // swings the nose back towards the aim
        }
        else
        {
            var tryingAndFailing = throttle >= StuckThrottle && absoluteSpeed < StuckSpeed;
            bot.StuckSeconds = tryingAndFailing ? bot.StuckSeconds + seconds : 0f;
            if (bot.StuckSeconds >= StuckSeconds)
            {
                bot.StuckSeconds = 0f;
                bot.ReverseSeconds = ReverseSeconds;
            }
        }

        controls.Throttle = throttle;
        controls.Brake = brake;
        controls.Handbrake = false;
        controls.Steer = KartInputSystem.RampSteer(controls.Steer, steer, seconds);
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _plans = new BotRoutePlan[physics.KartCount];
        _bots = World.Query(Query.With<Bot, Kart, KartControls>().With<Position, Rotation, Velocity>()
            .With<AngularVelocity>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bots.GetChunks())
        {
            var bots = chunk.GetSpan<Bot>();
            var karts = chunk.GetSpan<Kart>();
            var controls = chunk.GetSpan<KartControls>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.GetSpan<Rotation>();
            var velocities = chunk.GetSpan<Velocity>();
            var angularVelocities = chunk.GetSpan<AngularVelocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var index = karts[i].Index;
                var tuning = physics.GetDynamics(index).Tuning;
                var plan = _plans[index] ??= new BotRoutePlan(track, tuning, CornerSpeedFactor(bots[i].Skill),
                    BrakingShare(bots[i].Skill));
                var body = KartFrame.ToBody(positions[i].Value, rotations[i].Radians, velocities[i].Value,
                    angularVelocities[i].Value);
                Drive(ref bots[i], ref controls[i], body, karts[i].Surface, tuning, plan, deltaSeconds);
            }
        }
    }
}
