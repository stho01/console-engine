using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using SkyStack.Climbing;
using SkyStack.Components;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Draws the climber (head and legs, two columns wide), a fading trail while it is fast or in a combo, and a spark on
/// the wall it just bounced off. In a combo the climber flashes through <see cref="ComboColors"/>.
/// </summary>
public sealed class ClimberRenderSystem(RunStats run, Camera camera, RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Above this share of <see cref="ClimbPhysics.MaxRunSpeed"/> the trail shows.</summary>
    private const float TrailSpeedShare = .6f;

    private const float TrailSampleSeconds = .03f;
    private const int TrailLength = 6;
    private const float ComboFlashesPerSecond = 12f;

    private const ConsoleColor HeadColor = ConsoleColor.Yellow;
    private const ConsoleColor LegsColor = ConsoleColor.White;
    private const ConsoleColor FallenColor = ConsoleColor.Red;
    private const ConsoleColor SparkColor = ConsoleColor.Yellow;
    private const ConsoleColor TrailColor = ConsoleColor.DarkCyan;
    private const char TrailChar = '░';

    private static readonly ConsoleColor[] ComboColors =
        [ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.Green];

    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private int _trailCount;
    private int _trailNext;
    private float _sampleSeconds;
    private float _seconds;
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
                Draw(in climbers[i], positions[i].Value, velocities[i].Value, deltaSeconds);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Draw(in Climber climber, Vector2 position, Vector2 velocity, float deltaSeconds)
    {
        var showTrail = !run.IsOver &&
                        (run.IsComboRunning || MathF.Abs(velocity.X) > TrailSpeedShare * ClimbPhysics.MaxRunSpeed);
        UpdateTrail(position, showTrail, deltaSeconds);
        DrawTrail();

        var x = (int)MathF.Floor(position.X);
        var feetRow = ScreenRow(position.Y);
        var (head, legs) = Colors();
        console.Draw(x, feetRow - 2, '(', head);
        console.Draw(x + 1, feetRow - 2, ')', head);

        var (left, right) = Legs(in climber, x, velocity);
        console.Draw(x, feetRow - 1, left, legs);
        console.Draw(x + 1, feetRow - 1, right, legs);

        if (climber.SparkSeconds <= 0f)
            return;

        var sparkColumn = climber.SparkSide < 0 ? Layout.InnerLeft - 1 : Layout.InnerRight;
        console.Draw(sparkColumn, feetRow - 2, '+', SparkColor);
        console.Draw(sparkColumn, feetRow - 1, '*', SparkColor);
    }

    private (ConsoleColor Head, ConsoleColor Legs) Colors()
    {
        if (run.IsOver)
            return (FallenColor, FallenColor);
        if (!run.IsComboRunning)
            return (HeadColor, LegsColor);

        var flash = ComboColors[(int)(_seconds * ComboFlashesPerSecond) % ComboColors.Length];
        return (flash, flash);
    }

    private static (char Left, char Right) Legs(in Climber climber, int x, Vector2 velocity)
    {
        if (!climber.IsGrounded)
            return velocity.Y < 0f ? ('\\', '/') : ('/', '\\');
        if (MathF.Abs(velocity.X) < 1f)
            return ('|', '|');

        return TowerLooks.Mod(x, 2) == 0 ? ('/', '|') : ('|', '\\');
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

    private void DrawTrail()
    {
        for (var i = 0; i < _trailCount; i++)
        {
            var point = _trail[(_trailNext - 1 - i + TrailLength) % TrailLength];
            var x = (int)MathF.Floor(point.X);
            var row = ScreenRow(point.Y) - 1;
            console.Draw(x, row, TrailChar, TrailColor);
            console.Draw(x + 1, row, TrailChar, TrailColor);
        }
    }

    private int ScreenRow(float worldRow) => (int)MathF.Round(worldRow) - (int)camera.Position.Y;
}
