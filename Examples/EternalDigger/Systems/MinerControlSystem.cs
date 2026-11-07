using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Turns input into moves, digs, ladders and rescues while the miner is idle. Falling comes first: an unsupported
/// miner always falls one tile before any key is read. A miner riding the lift only reads the ride keys.
/// <see cref="MinerMotionSystem"/> plays the moves this starts. The keyboard is read once per frame into a <see cref="MinerInput"/>; <see cref="Control"/> holds the rules.
/// </summary>
public sealed class MinerControlSystem(Mine mine, Elevator elevator, EffectSpawner effects) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float WalkDuration = .13f;
    public const float ClimbDuration = .17f;

    /// <summary>Tiles climbed up at <see cref="ClimbDuration"/> before the climb starts to speed up.</summary>
    public const int ClimbRampStart = 4;

    /// <summary>Tiles over which a long climb speeds up from <see cref="ClimbDuration"/> to the top speed.</summary>
    public const int ClimbRampTiles = 10;

    /// <summary>Seconds per tile at the top climbing speed.</summary>
    public const float TopClimbDuration = .06f;

    public const float FallDuration = .06f;
    public const float DigInterval = .16f;
    public const float SwingDuration = .12f;
    public const float RescueDuration = 1.5f;

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = MinerInput.FromKeyboard();
        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Control(ref miners[i], ref positions[i], input, deltaSeconds, Commands);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>One frame of the miner's rules. Effects are recorded in <paramref name="commands"/>.</summary>
    public void Control(ref Miner miner, ref Position position, in MinerInput input, float deltaSeconds,
        CommandBuffer commands)
    {
        if (UpdateRescue(ref miner, ref position, input.Rescue, deltaSeconds, commands) || miner.IsMoving)
            return;

        if (miner.IsRiding && Ride(ref miner, input))
            return;

        if (!mine.IsSupported(miner.Column, miner.Row))
        {
            miner.ClimbStreak = 0;
            StartMove(ref miner, 0, 1, FallDuration, MinerMotion.Falling);
            return;
        }

        if (input.PlaceLadder)
            PlaceLadder(ref miner, commands);

        if (input.Horizontal != 0)
        {
            miner.ClimbStreak = 0;
            miner.FacingX = input.Horizontal;
            MoveOrDig(ref miner, input.Horizontal, 0, WalkDuration, MinerMotion.Walking, commands);
            return;
        }

        if (input.Down)
        {
            miner.ClimbStreak = 0;
            MoveOrDig(ref miner, 0, 1, ClimbDuration, MinerMotion.Climbing, commands);
            return;
        }

        if (input.Up && mine.HasLadder(miner.Column, miner.Row))
        {
            var climbing = !mine.IsSolid(miner.Column, miner.Row - 1);
            MoveOrDig(ref miner, 0, -1, ClimbUpDuration(miner.ClimbStreak), MinerMotion.Climbing, commands);
            miner.ClimbStreak = climbing ? miner.ClimbStreak + 1 : 0;
            return;
        }

        miner.ClimbStreak = 0;
    }

    /// <summary>
    /// Seconds for the next tile of a climb up, after <paramref name="streak"/> tiles climbed without letting go:
    /// <see cref="ClimbDuration"/> for the first <see cref="ClimbRampStart"/>, then down to
    /// <see cref="TopClimbDuration"/> over <see cref="ClimbRampTiles"/>.
    /// </summary>
    public static float ClimbUpDuration(int streak)
    {
        if (streak < ClimbRampStart)
            return ClimbDuration;

        var ramp = Math.Min(1f, (streak - ClimbRampStart + 1f) / ClimbRampTiles);
        return ClimbDuration + (TopClimbDuration - ClimbDuration) * ramp;
    }

    /// <summary>Whether a climb with this streak is past the start speed (it slides without easing).</summary>
    public static bool IsFastClimb(int streak) => streak > ClimbRampStart;

    /// <summary>Whether a climb with this streak is at the top speed (it trails dust).</summary>
    public static bool IsTopSpeedClimb(int streak) => streak >= ClimbRampStart + ClimbRampTiles;

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// The ride keys while in the stopped car: Up to the top, Down to the bottom. Left/Right, or Down at the bottom,
    /// step off (returns false, so the key is handled as usual). True when the frame is used up.
    /// </summary>
    private bool Ride(ref Miner miner, in MinerInput input)
    {
        if (elevator.IsCarMoving)
            return true;

        var atTop = miner.Row == Elevator.TopRow;
        if (input.Up && !atTop)
        {
            elevator.Send(Elevator.TopRow);
            return true;
        }

        if (input.Down && atTop)
        {
            elevator.Send(elevator.BottomRow);
            return true;
        }

        if (input.Horizontal == 0 && !input.Down)
            return true;

        miner.IsRiding = false;
        return false;
    }

    /// <summary>Walks/climbs into air, digs anything else. Unbreakable tiles just block.</summary>
    private void MoveOrDig(ref Miner miner, int dx, int dy, float duration, MinerMotion motion,
        CommandBuffer commands)
    {
        var column = miner.Column + dx;
        var row = miner.Row + dy;
        miner.LampDirection = new Vector2(dx, dy);

        if (!mine.IsSolid(column, row))
        {
            StartMove(ref miner, dx, dy, duration, motion);
            return;
        }

        if (miner.DigCooldown > 0f || !Tiles.IsBreakable(mine.TypeAt(column, row)))
            return;

        var result = mine.Hit(column, row);
        miner.DigCooldown = DigInterval;
        miner.SwingTime = SwingDuration;
        miner.SwingDirection = new Point(dx, dy);
        effects.DigHit(commands, column, row, result.Type, miner.SwingDirection);

        if (result.Outcome != DigOutcome.Broken)
            return;

        effects.TileBreak(commands, column, row, result.Type);
        Shop.Collect(ref miner, result.Type);
    }

    private static void StartMove(ref Miner miner, int dx, int dy, float duration, MinerMotion motion)
    {
        miner.MoveFrom = new Vector2(miner.Column, miner.Row);
        miner.Column += dx;
        miner.Row += dy;
        miner.MoveElapsed = 0f;
        miner.MoveDuration = duration;
        miner.Motion = motion;
    }

    private void PlaceLadder(ref Miner miner, CommandBuffer commands)
    {
        var feet = Layout.TileCenter(miner.Column, miner.Row) + new Vector2(0, Layout.TileHeight / 2f);
        if (miner.Ladders > 0 && mine.TryPlaceLadder(miner.Column, miner.Row))
        {
            miner.Ladders--;
            effects.FootDust(commands, feet, -miner.FacingX);
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

        effects.FootDust(commands, feet, miner.FacingX);
    }

    /// <summary>Hold R to be pulled back to the surface. The bag is lost. True when the rescue just happened.</summary>
    private bool UpdateRescue(ref Miner miner, ref Position position, bool held, float deltaSeconds,
        CommandBuffer commands)
    {
        if (!held)
        {
            miner.RescueHold = 0f;
            return false;
        }

        miner.RescueHold += deltaSeconds;
        if (miner.RescueHold < RescueDuration)
            return false;

        miner.RescueHold = 0f;
        miner.CopperOre = 0;
        miner.SilverOre = 0;
        miner.GoldOre = 0;
        miner.Column = Layout.SpawnColumn;
        miner.Row = Layout.GroundRow;
        miner.MoveFrom = new Vector2(miner.Column, miner.Row);
        miner.MoveDuration = 0f;
        miner.MoveElapsed = 0f;
        miner.Motion = MinerMotion.Idle;
        miner.FallRows = 0;
        miner.ClimbStreak = 0;
        miner.IsRiding = false;
        position.Value = Layout.TileToWorld(miner.Column, miner.Row);

        effects.Pop(commands, Layout.TileCenter(miner.Column, miner.Row));
        effects.LandingDust(commands, Layout.TileToWorld(miner.Column, miner.Row + 1) + new Vector2(2, 0), 2);
        return true;
    }
}
