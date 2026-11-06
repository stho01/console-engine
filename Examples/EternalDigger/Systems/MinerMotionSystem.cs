using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Slides the miner's <c>Position</c> one tile per move started by <see cref="MinerControlSystem"/>, kicks up dust
/// from the feet and on landing, and counts down the dig cooldown and swing animation.
/// </summary>
public sealed class MinerMotionSystem(Mine mine, EffectSpawner effects) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _miners;
    private int _steps;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Move(ref miners[i], ref positions[i], deltaSeconds, Commands);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>One frame of motion. Effects are recorded in <paramref name="commands"/>.</summary>
    public void Move(ref Miner miner, ref Position position, float deltaSeconds, CommandBuffer commands)
    {
        miner.DigCooldown = Math.Max(0f, miner.DigCooldown - deltaSeconds);
        miner.SwingTime = Math.Max(0f, miner.SwingTime - deltaSeconds);

        if (!miner.IsMoving)
            return;

        miner.MoveElapsed += deltaSeconds;
        var t = Math.Min(1f, miner.MoveElapsed / miner.MoveDuration);
        // Walking and climbing ease in and out, falling speeds up.
        var eased = miner.Motion == MinerMotion.Falling ? t * t : t * t * (3f - 2f * t);
        var from = Layout.TileToWorld((int)miner.MoveFrom.X, (int)miner.MoveFrom.Y);
        var to = Layout.TileToWorld(miner.Column, miner.Row);
        position.Value = Vector2.Lerp(from, to, eased);

        if (t < 1f)
            return;

        var motion = miner.Motion;
        miner.MoveDuration = 0f;
        miner.MoveElapsed = 0f;
        miner.Motion = MinerMotion.Idle;
        FinishMove(ref miner, motion, commands);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void FinishMove(ref Miner miner, MinerMotion motion, CommandBuffer commands)
    {
        var feet = Layout.TileToWorld(miner.Column, miner.Row + 1) + new Vector2(Layout.TileWidth / 2f, -.1f);
        switch (motion)
        {
            case MinerMotion.Walking when ++_steps % 2 == 0:
                effects.FootDust(commands, feet, -miner.FacingX);
                break;
            case MinerMotion.Climbing:
                effects.FootDust(commands, feet, miner.FacingX);
                break;
            case MinerMotion.Falling:
                miner.FallRows++;
                if (mine.IsSupported(miner.Column, miner.Row))
                {
                    effects.LandingDust(commands, feet, miner.FallRows);
                    miner.FallRows = 0;
                }

                break;
        }
    }
}
