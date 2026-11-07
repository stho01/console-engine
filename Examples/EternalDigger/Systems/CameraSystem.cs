using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Follows the miner vertically, keeping them at <see cref="FollowHeight"/> of the screen, smoothed independently of
/// the frame rate. Never shows anything above the sky. Adds the <see cref="ScreenShake"/> on top. The camera itself
/// always sits on whole cells; the smooth position is kept here.
/// </summary>
public sealed class CameraSystem(Camera camera, RenderConsole console, ScreenShake shake, Random random)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float FollowHeight = .4f;
    private const float Smoothing = 8f;

    private Query _miners;
    private float _y;
    private bool _placed;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var target = chunk.GetSpan<Position>()[0].Value.Y + Layout.TileHeight / 2f - console.Height * FollowHeight;
            target = Math.Max(target, -Layout.HudHeight);

            // Snap on the first frame and after a rescue, ease otherwise.
            if (!_placed || Math.Abs(target - _y) > console.Height)
                _y = target;
            else
                _y += (target - _y) * (1f - MathF.Exp(-Smoothing * deltaSeconds));

            _placed = true;
            break;
        }

        var strength = shake.Strength;
        var offset = strength > 0f
            ? new Vector2(random.NextSingle() * 2f - 1f, random.NextSingle() * 2f - 1f) * strength
            : Vector2.Zero;
        // Whole cells: every renderer then maps world to screen the same way, and the view doesn't shimmer.
        camera.Position = new Vector2(MathF.Round(offset.X), MathF.Round(_y + offset.Y));
        shake.Update(deltaSeconds);
    }
}
