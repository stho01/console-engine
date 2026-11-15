using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Rendering3D;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using Kartster.Ui;

namespace Kartster.Modes;

/// <summary>
/// Time trial on one track with one kart: the player alone, every lap recorded pose by pose (<see cref="Step"/>, once
/// per fixed step, allocation free), the fastest lap kept as <see cref="Best"/> and saved (<see cref="GhostFile"/>),
/// and on later laps and attempts that lap driven again as a see-through ghost (<see cref="TryGetGhost"/>), which
/// nothing collides with. Kartster has no time trial; the ghost is drawn as its see-through ghost karts are, at
/// <see cref="GhostOpacity"/>. Lives across attempts (restarts): <see cref="AddTo"/> starts a new one.
/// </summary>
/// <remarks>
/// A lap's poses start on the step it began: for lap 1 the step the countdown ran out (on the grid), for the others
/// the step the kart crossed the line. The ghost shows step n of the best lap on step n of the lap being driven.
/// </remarks>
public sealed class TimeTrial
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's <c>KartGhost.GhostOpacity</c>.</summary>
    public const float GhostOpacity = 0.35f;

    /// <summary>The longest lap recorded: 10 minutes at 120 steps per second (1.1 MB per lap buffer).</summary>
    public const int MaxLapSteps = 120 * 600;

    /// <summary>Lap times shown; the menu offers up to 5 laps.</summary>
    public const int MaxLaps = 9;

    public const string BestLabel = "BEST ";
    public const string NoTime = "-";

    private readonly string _path;
    private readonly float _stepSeconds;
    private GhostLap _best;
    private GhostLap _lap;
    private readonly float[] _lapTimes = new float[MaxLaps];
    private readonly char[] _text = new char[32];
    private int _lapTimeCount;
    private int _currentLap = 1;
    private bool _done;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="path">The best lap's file (<see cref="GhostFile.PathFor"/>), read now; null: nothing on disk.</param>
    public TimeTrial(string path, float stepSeconds = TrackScene.StepSeconds, int maxLapSteps = MaxLapSteps)
    {
        _path = path;
        _stepSeconds = stepSeconds;
        _best = new GhostLap(maxLapSteps);
        _lap = new GhostLap(maxLapSteps);
        if (path != null)
            GhostFile.Load(path, _best, stepSeconds);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The fastest lap so far (from the file or this session); <c>Count</c> 0: none yet.</summary>
    public GhostLap Best => _best;

    /// <summary>The times of the laps done in this attempt, in order.</summary>
    public ReadOnlySpan<float> LapTimes => _lapTimes.AsSpan(0, _lapTimeCount);

    /// <summary>Whether the last new best lap couldn't be written (the ghost still shows this session).</summary>
    public bool SaveFailed { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Adds the recorder (<see cref="TimeTrialSystem"/>) to the fixed step and the ghost
    /// (<see cref="GhostRenderSystem"/>) after <paramref name="camera"/> to the presentation, and starts a new attempt.
    /// </summary>
    public void AddTo(SystemGroup fixedStep, SystemGroup presentation, RaceSession race, KartRenderSystem camera,
        KartsterGame game, FixedTimestep timestep)
    {
        Reset();
        fixedStep.Add(new TimeTrialSystem(this, race));
        presentation.Add(new GhostRenderSystem(this, camera, game, CreateGhostMesh(game.Kart), timestep));
    }

    /// <summary>A new attempt: no laps done, nothing recorded; <see cref="Best"/> stays.</summary>
    public void Reset()
    {
        _lap.Clear();
        _lapTimeCount = 0;
        _currentLap = 1;
        _done = false;
    }

    /// <summary>
    /// One fixed step, after the race has counted it: ends the lap when the race counted one (a faster one becomes
    /// <see cref="Best"/> and is saved), then records <paramref name="pose"/>. Nothing during the countdown or once
    /// the player has finished.
    /// </summary>
    public void Step(in Racer racer, RacePhase phase, GhostPose pose)
    {
        if (_done || phase == RacePhase.Countdown)
            return;

        if (racer.CurrentLap != _currentLap || racer.HasFinished)
        {
            EndLap(racer.LastLapSeconds);
            _currentLap = racer.CurrentLap;
        }

        if (racer.HasFinished || phase != RacePhase.Racing)
        {
            _done = true;
            return;
        }

        _lap.Add(pose);
    }

    /// <summary>
    /// Where the ghost is drawn now: the best lap at the step the current lap is on, between the step before and it
    /// by <paramref name="alpha"/> (<c>FixedTimestep.Alpha</c>), as the karts are interpolated.
    /// </summary>
    /// <returns>False when there is no ghost to show: no best lap, past its end, or the attempt is over.</returns>
    public bool TryGetGhost(float alpha, out GhostPose pose)
    {
        var step = _lap.Count - 1;
        if (_done || step < 0 || step >= _best.Count)
        {
            pose = default;
            return false;
        }

        var current = _best[step];
        var previous = _best[Math.Max(0, step - 1)];
        var turn = MathF.IEEERemainder(current.Radians - previous.Radians, MathF.Tau);
        pose = new GhostPose(
            previous.X + (current.X - previous.X) * alpha,
            previous.Z + (current.Z - previous.Z) * alpha,
            previous.Height + (current.Height - previous.Height) * alpha,
            previous.Radians + turn * alpha);
        return true;
    }

    /// <summary>
    /// The time-trial part of the HUD: hides the standings (a race of one) and draws in their place, bottom-left over
    /// the lap line, this attempt's lap times (the best lap's in yellow) and "BEST" with the best lap's time.
    /// Allocates nothing.
    /// </summary>
    public void DrawUi(IPixelTarget target, ref HudState hud)
    {
        hud.StandingCount = 0;
        var lapTop = target.Height - Hud.Margin - PixelFont.GlyphHeight * Hud.LapScale;
        var y = lapTop - Hud.Margin - PixelFont.GlyphHeight;
        var timeX = Hud.Margin + PixelFont.Advance * BestLabel.Length;

        PixelFont.DrawTextOutlined(target, Hud.Margin, y, BestLabel, UiColors.Sky, UiColors.TextShadow);
        var length = _best.Count > 0 ? TextFormat.Time(_text, _best.LapSeconds) : TextFormat.Copy(_text, NoTime);
        PixelFont.DrawTextOutlined(target, timeX, y, _text.AsSpan(0, length), UiColors.Sky, UiColors.TextShadow);

        for (var i = _lapTimeCount - 1; i >= 0; i--)
        {
            y -= Hud.StandingRowHeight;
            var color = _lapTimes[i] == _best.LapSeconds ? UiColors.Selected : UiColors.Text;
            length = TextFormat.Int(_text, i + 1);
            PixelFont.DrawTextOutlined(target, Hud.Margin, y, _text.AsSpan(0, length), color, UiColors.TextShadow);
            length = TextFormat.Time(_text, _lapTimes[i]);
            PixelFont.DrawTextOutlined(target, timeX, y, _text.AsSpan(0, length), color, UiColors.TextShadow);
        }
    }

    /// <summary>The kart's own triangles and colours, every triangle at <see cref="GhostOpacity"/>.</summary>
    public static Mesh CreateGhostMesh(Mesh kart)
    {
        var opacity = new float[kart.TriangleCount];
        Array.Fill(opacity, GhostOpacity);
        return new Mesh(kart.Positions, kart.Indices, kart.TriangleColors) { TriangleOpacity = opacity };
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void EndLap(float seconds)
    {
        if (_lapTimeCount < MaxLaps)
            _lapTimes[_lapTimeCount++] = seconds;

        if (!_lap.Overflowed && _lap.Count > 0 && (_best.Count == 0 || seconds < _best.LapSeconds))
        {
            _lap.LapSeconds = seconds;
            (_best, _lap) = (_lap, _best);
            if (_path != null)
                SaveFailed = !GhostFile.Save(_path, _best, _stepSeconds);
        }

        _lap.Clear();
    }
}
