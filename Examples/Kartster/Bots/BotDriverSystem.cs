using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
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
/// <item>Traffic (<see cref="BotTraffic"/>): every step it first places every kart on the <see cref="BotRoad"/>
/// (<see cref="TrafficKart"/>, one preallocated slot each); then each bot's lane is its <see cref="Bot.LaneOffset"/>
/// plus a slow wander (<see cref="Wanders"/>), moved round karts alongside and ahead, onto a kart it leans on
/// (<see cref="Aggression"/>), kept <see cref="BotTraffic.EdgeMargin"/> inside the asphalt, and eased there at
/// <see cref="BotTraffic.LaneChangeSpeed"/>; a kart ahead in its path caps its speed. With
/// <see cref="RubberBanding"/> its pace follows the leading human.</item>
/// </list>
/// <see cref="Bot.Skill"/> (0..1) moves every number from Earl's towards Merlin's. Runs in the fixed step, before the
/// <see cref="KartPhysicsSystem"/>: it reads only what the physics changes, so a bot drives the same at any frame rate
/// and a headless test drives exactly as the game does; a decision costs a few hundred nanoseconds. While the
/// <paramref name="race"/> can't drive (the countdown) every bot's controls are held at zero and it decides nothing.
/// </summary>
/// <param name="race">Null: always drives.</param>
[UpdateBefore(typeof(KartPhysicsSystem))]
public sealed class BotDriverSystem(Track track, KartPhysicsSystem physics, RaceSession race = null) : EntitySystem
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
    private const float WanderRate = 0.15f; // turns per second, and 2.3 times that (Kartster's)
    private const int NarrowestAheadSamples = 5; // the lane must fit the road this many samples (10 m) on

    private BotRoad _road;
    private BotRoutePlan[] _plans;
    private TrafficKart[] _traffic;
    private BotTrafficState[] _states;
    private Query _bots;
    private Query _karts;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Kartster's optional catch-up (<see cref="BotTraffic.RubberBand"/>). Off, as in Kartster.</summary>
    public bool RubberBanding { get; set; }

    /// <summary>The slow wobble off the line of Kartster's Earl and Merlin (<see cref="WanderMeters"/>). On.</summary>
    public bool Wanders { get; set; } = true;

    /// <summary>The road every bot plans on: samples, directions, widths. Made in <c>OnCreate</c>.</summary>
    public BotRoad Road => _road;

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

    /// <summary>Most the line wanders either way, metres (Earl 1, Merlin 0.4).</summary>
    public static float WanderMeters(float skill) => float.Lerp(1f, 0.4f, skill);

    /// <summary>Chance it leans on a kart beside it, per <see cref="BotTraffic.LeanCooldownSeconds"/> (0 to 0.4).</summary>
    public static float Aggression(float skill) => float.Lerp(0f, 0.4f, skill);

    /// <summary>The plan of the bot in kart slot <paramref name="index"/>, made on its first step.</summary>
    public BotRoutePlan GetPlan(int index) => _plans[index];

    /// <summary>Where kart slot <paramref name="index"/> was on the road at the last step.</summary>
    public TrafficKart GetTraffic(int index) => _traffic[index];

    /// <summary>The lane the bot in slot <paramref name="index"/> aimed for at the last step, metres right.</summary>
    public float GetLaneOffset(int index) => _states[index].Offset;

    /// <summary>
    /// What a bot does this step: <paramref name="controls"/> from its kart's <paramref name="body"/>, the
    /// <paramref name="surface"/> under it and its <paramref name="plan"/>; updates <paramref name="bot"/>.
    /// </summary>
    public static void Drive(ref Bot bot, ref KartControls controls, in KartBody body, Surface surface,
        KartTuning tuning, BotRoutePlan plan, float seconds) =>
        Drive(ref bot, ref controls, body, surface, tuning, plan, seconds, bot.LaneOffset, float.MaxValue, 1f);

    /// <summary>
    /// <see cref="Drive(ref Bot, ref KartControls, in KartBody, Surface, KartTuning, BotRoutePlan, float)"/> on
    /// <paramref name="laneOffset"/> instead of the bot's own lane, no faster than <paramref name="speedLimit"/>, at
    /// <paramref name="pace"/> times its planned speed and throttle (rubber-banding; corners at most 97% of the grip
    /// limit, as Kartster).
    /// </summary>
    public static void Drive(ref Bot bot, ref KartControls controls, in KartBody body, Surface surface,
        KartTuning tuning, BotRoutePlan plan, float seconds, float laneOffset, float speedLimit, float pace)
    {
        var forward = body.Forward;
        var right = body.Right;
        var speed = Vector2.Dot(body.Velocity, forward);

        var sample = plan.Locate(body.Position, bot.Sample);
        bot.Sample = sample;

        bot.OffRoadSeconds = surface == Surface.Asphalt ? 0f : bot.OffRoadSeconds + seconds;
        var rejoining = bot.OffRoadSeconds > ReactionSeconds(bot.Skill);

        var absoluteSpeed = MathF.Abs(speed);
        var lookahead = rejoining
            ? RejoinAimMeters
            : MathF.Max(MinimumLookahead, LookaheadSeconds(bot.Skill) * absoluteSpeed);
        var toTarget = plan.PointAhead(sample, lookahead, rejoining ? 0f : laneOffset) - body.Position;
        var targetSpeed = PlanSpeed(plan, sample, absoluteSpeed);
        var throttleCap = ThrottleCap(bot.Skill);
        if (pace != 1f)
        {
            targetSpeed *= MathF.Min(pace, MathF.Max(1f, 0.97f / CornerSpeedFactor(bot.Skill)));
            throttleCap = MathF.Min(1f, throttleCap * pace);
        }

        if (rejoining)
            targetSpeed = MathF.Min(targetSpeed, RejoinSpeed(bot.Skill));
        targetSpeed = MathF.Min(targetSpeed, speedLimit);

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

        var throttle = Math.Clamp((targetSpeed - speed) / FullThrottleUnderSpeed, 0f, 1f) * throttleCap;
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
        _road = new BotRoad(track);
        _plans = new BotRoutePlan[physics.KartCount];
        _traffic = new TrafficKart[physics.KartCount];
        for (var i = 0; i < _traffic.Length; i++)
            _traffic[i].Sample = -1;
        _states = new BotTrafficState[physics.KartCount];
        _bots = World.Query(Query.With<Bot, Kart, KartControls>().With<Position, Rotation, Velocity>()
            .With<AngularVelocity>());
        _karts = World.Query(Query.With<Kart, Position, Velocity>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (race is { CanDrive: false })
        {
            foreach (var chunk in _bots.GetChunks())
                chunk.GetSpan<KartControls>()[..chunk.Count].Clear();
            return;
        }

        LocateKarts();
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
                var plan = _plans[index] ??= new BotRoutePlan(_road, tuning, CornerSpeedFactor(bots[i].Skill),
                    BrakingShare(bots[i].Skill));
                var body = KartFrame.ToBody(positions[i].Value, rotations[i].Radians, velocities[i].Value,
                    angularVelocities[i].Value);
                bots[i].Sample = _traffic[index].Sample;
                var speedLimit = float.MaxValue;
                var laneOffset = ChooseLane(index, bots[i], plan, Vector2.Dot(body.Velocity, body.Forward),
                    ref speedLimit, deltaSeconds);
                var pace = RubberBanding ? BotTraffic.RubberBand(_traffic, index) : 1f;
                Drive(ref bots[i], ref controls[i], body, karts[i].Surface, tuning, plan, deltaSeconds, laneOffset,
                    speedLimit, pace);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float PlanSpeed(BotRoutePlan plan, int sample, float absoluteSpeed) =>
        MathF.Min(plan.SpeedAhead(sample, 0f), plan.SpeedAhead(sample, SpeedLeadSeconds * absoluteSpeed));

    /// <summary>Puts every kart on the road for this step, before any bot decides.</summary>
    private void LocateKarts()
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var isHuman = chunk.Has<PlayerKart>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var traffic = ref _traffic[karts[i].Index];
                var position = new Vector2(positions[i].Value.X, positions[i].Value.Y);
                var wasSeen = traffic.Sample >= 0;
                traffic.Sample = _road.Locate(position, traffic.Sample);
                var (along, lateral) = _road.ToRoute(position, traffic.Sample);
                traffic.Driven = wasSeen ? traffic.Driven + _road.Gap(traffic.Along, along) : _road.Gap(0f, along);
                traffic.Along = along;
                traffic.Lateral = lateral;
                var velocity = new Vector2(velocities[i].Value.X, velocities[i].Value.Y);
                traffic.Speed = Vector2.Dot(velocity, _road.ForwardAt(traffic.Sample));
                traffic.IsHuman = isHuman;
            }
        }
    }

    /// <summary>
    /// The lane the bot in slot <paramref name="index"/> aims for this step (<see cref="BotTraffic"/>), and the speed
    /// it must keep under for the kart ahead.
    /// </summary>
    private float ChooseLane(int index, in Bot bot, BotRoutePlan plan, float speed, ref float speedLimit,
        float seconds)
    {
        ref var state = ref _states[index];
        if (!state.IsStarted)
            state = BotTrafficState.Start(index, bot.LaneOffset);

        var sample = _traffic[index].Sample;
        var (left, right) = _road.NarrowestAhead(sample, NarrowestAheadSamples);
        var least = -left + BotTraffic.EdgeMargin;
        var most = right - BotTraffic.EdgeMargin;
        if (least > most)
            least = most = (least + most) / 2f; // narrower than two margins: the middle of the asphalt

        var line = bot.LaneOffset;
        if (Wanders)
        {
            state.WanderPhase = (state.WanderPhase + seconds * WanderRate) % 1f;
            state.WanderPhase2 = (state.WanderPhase2 + seconds * WanderRate * 2.3f) % 1f;
            line += WanderMeters(bot.Skill) * (0.7f * MathF.Sin(state.WanderPhase * MathF.Tau) +
                                               0.3f * MathF.Sin(state.WanderPhase2 * MathF.Tau));
        }

        var planSpeed = PlanSpeed(plan, sample, MathF.Abs(speed));
        var wanted = BotTraffic.Avoid(_road, _traffic, index, state.LeanTarget, Math.Clamp(line, least, most),
            planSpeed, least, most, ref speedLimit);
        wanted = BotTraffic.Lean(_road, _traffic, index, ref state, Aggression(bot.Skill),
            planSpeed < speed - BotTraffic.BrakingMargin, wanted, least, most, seconds);

        var step = BotTraffic.LaneChangeSpeed * seconds;
        state.Offset = Math.Clamp(Math.Clamp(wanted, state.Offset - step, state.Offset + step), least, most);
        return state.Offset;
    }
}
