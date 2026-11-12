using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using Microsoft.Xna.Framework;
using SkyStack.Climbing;
using SkyStack.Components;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Paints the climber into the <see cref="PixelCanvas"/>: the pose for standing, running (two frames, by distance),
/// rising or falling, looking the way it last ran. While fast or in a combo it leaves a trail of shadows, in a combo
/// its jacket flashes, a wall bounce throws a spark and a landing kicks up dust.
/// </summary>
public sealed class ClimberRenderSystem(RunStats run, Camera camera, PixelCanvas canvas) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Above this share of <see cref="ClimbPhysics.MaxRunSpeed"/> the trail shows.</summary>
    private const float TrailSpeedShare = .6f;

    private const float TrailSampleSeconds = .025f;
    private const int TrailLength = 5;
    private const float ComboFlashesPerSecond = 12f;

    /// <summary>Pixels run per running frame.</summary>
    private const int StridePixels = 4;

    /// <summary>Below this speed (pixels/s) a climber on a floor stands.</summary>
    private const float StandSpeed = 4f;

    private const float DustSeconds = .25f;

    /// <summary>Pixels the dust spreads to each side per second.</summary>
    private const float DustSpeed = 40f;

    /// <summary>Only landings falling faster than this (pixels/s) raise dust.</summary>
    private const float DustFallSpeed = 120f;

    private const ConsoleColor SparkColor = ConsoleColor.Yellow;
    private const ConsoleColor SparkCore = ConsoleColor.White;
    private const ConsoleColor DustColor = ConsoleColor.Gray;

    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private int _trailCount;
    private int _trailNext;
    private float _sampleSeconds;
    private float _seconds;

    private bool _wasGrounded = true;
    private float _lastFallSpeed;
    private float _dustSecondsLeft;
    private Vector2 _dustOrigin;

    private Query _climbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _climbers = World.Query(Query.With<Climber, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _seconds += deltaSeconds;
        foreach (var chunk in _climbers.GetChunks())
        {
            var climbers = chunk.GetSpan<Climber>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
                Paint(in climbers[i], positions[i].Value, velocities[i].Value, deltaSeconds);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Paint(in Climber climber, Vector2 position, Vector2 velocity, float deltaSeconds)
    {
        var sprite = Pose(in climber, position, velocity);
        var mirrored = climber.Facing < 0;

        var showTrail = !run.IsOver &&
                        (run.IsComboRunning || MathF.Abs(velocity.X) > TrailSpeedShare * ClimbPhysics.MaxRunSpeed);
        UpdateTrail(position, showTrail, deltaSeconds);
        for (var i = _trailCount - 1; i >= 0; i--)
        {
            var point = _trail[(_trailNext - 1 - i + TrailLength) % TrailLength];
            canvas.Draw(sprite, ScreenX(point.X), ScreenY(point.Y) - sprite.Height, ClimberLooks.Trail, mirrored);
        }

        UpdateDust(in climber, position, velocity, deltaSeconds);
        PaintDust();

        canvas.Draw(sprite, ScreenX(position.X), ScreenY(position.Y) - sprite.Height, Palette(), mirrored);

        if (climber.SparkSeconds > 0f)
            PaintSpark(climber.SparkSide, ScreenY(position.Y) - ClimbPhysics.BodyHeight / 2);
    }

    private static PixelSprite Pose(in Climber climber, Vector2 position, Vector2 velocity)
    {
        if (!climber.IsGrounded)
            return velocity.Y < 0f ? ClimberLooks.Rising : ClimberLooks.Falling;
        if (MathF.Abs(velocity.X) < StandSpeed)
            return ClimberLooks.Standing;

        var stride = (int)MathF.Floor(position.X / StridePixels);
        return TowerLooks.Mod(stride, 2) == 0 ? ClimberLooks.RunningA : ClimberLooks.RunningB;
    }

    private ConsoleColor[] Palette()
    {
        if (run.IsOver)
            return ClimberLooks.Fallen;
        if (!run.IsComboRunning)
            return ClimberLooks.Normal;

        return ClimberLooks.Combo[(int)(_seconds * ComboFlashesPerSecond) % ClimberLooks.Combo.Length];
    }

    private void UpdateTrail(Vector2 position, bool showTrail, float deltaSeconds)
    {
        if (!showTrail)
        {
            _trailCount = 0;
            return;
        }

        _sampleSeconds += deltaSeconds;
        if (_sampleSeconds < TrailSampleSeconds)
            return;

        _sampleSeconds = 0f;
        _trail[_trailNext] = position;
        _trailNext = (_trailNext + 1) % TrailLength;
        _trailCount = Math.Min(_trailCount + 1, TrailLength);
    }

    private void UpdateDust(in Climber climber, Vector2 position, Vector2 velocity, float deltaSeconds)
    {
        _dustSecondsLeft = MathF.Max(0f, _dustSecondsLeft - deltaSeconds);
        if (climber.IsGrounded && !_wasGrounded && _lastFallSpeed > DustFallSpeed)
        {
            _dustSecondsLeft = DustSeconds;
            _dustOrigin = position + new Vector2(ClimbPhysics.BodyWidth / 2f, 0f);
        }

        _wasGrounded = climber.IsGrounded;
        if (!climber.IsGrounded)
            _lastFallSpeed = velocity.Y;
    }

    /// <summary>Two puffs that run apart along the floor; in their first half they are two pixels tall.</summary>
    private void PaintDust()
    {
        if (_dustSecondsLeft <= 0f)
            return;

        var age = DustSeconds - _dustSecondsLeft;
        var spread = (int)(age * DustSpeed) + 2;
        var x = ScreenX(_dustOrigin.X);
        var y = ScreenY(_dustOrigin.Y) - 1;
        canvas.Set(x - spread, y, DustColor);
        canvas.Set(x + spread, y, DustColor);
        if (age >= DustSeconds / 2)
            return;

        canvas.Set(x - spread - 1, y - 1, DustColor);
        canvas.Set(x + spread + 1, y - 1, DustColor);
    }

    /// <summary>A small star on the inner face of the wall that was hit.</summary>
    private void PaintSpark(int side, int y)
    {
        var x = side < 0 ? Layout.InnerLeft : Layout.InnerRight - 1;
        canvas.Set(x, y, SparkCore);
        canvas.Set(x, y - 1, SparkColor);
        canvas.Set(x, y + 1, SparkColor);
        canvas.Set(x - side, y, SparkColor);
        canvas.Set(x - 2 * side, y - 2, SparkColor);
        canvas.Set(x - 2 * side, y + 2, SparkColor);
    }

    private static int ScreenX(float worldX) => (int)MathF.Floor(worldX);

    private int ScreenY(float worldY) => (int)MathF.Round(worldY) - (int)camera.Position.Y;
}
