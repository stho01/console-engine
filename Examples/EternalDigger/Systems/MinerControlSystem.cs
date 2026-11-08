using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Turns input into moves, jumps, digs, ladders, dynamite and rescues while the miner is idle. Falling comes first: an
/// unsupported miner always falls one tile before any key is read. A miner riding the lift only reads the ride keys.
/// <see cref="MinerMotionSystem"/> plays the moves this starts. The keyboard is read once per frame into a
/// <see cref="MinerInput"/>; <see cref="Control"/> holds the rules.
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

    /// <summary>Tiles a jump carries the miner sideways: over a one-tile gap to the tile beyond it.</summary>
    public const int JumpTiles = 2;

    /// <summary>Seconds a jump takes.</summary>
    public const float JumpDuration = .28f;

    /// <summary>Console cells the miner rises at the top of a jump; half of it under a low ceiling.</summary>
    public const float JumpHeight = 2f;
    /// <summary>Seconds between shovel hits with the starting shovel (<see cref="Upgrades.DigInterval"/>).</summary>
    public const float DigInterval = .16f;
    public const float SwingDuration = .12f;
    public const float RescueDuration = 1.5f;

    /// <summary>Seconds the use key must be held underground to plant a stick of dynamite.</summary>
    public const float PlantDuration = .5f;

    /// <summary>Seconds a planted stick's fuse burns.</summary>
    public const float FuseDuration = 2f;

    /// <summary>Seconds between two "TOO HARD" texts while the shovel keeps clanking on obsidian.</summary>
    public const float TooHardInterval = 1f;

    private Query _miners;
    private float _tooHardCooldown;

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
        _tooHardCooldown = Math.Max(0f, _tooHardCooldown - deltaSeconds);
        // The plant timer survives the frame E comes up, so InteractionSystem can tell a tap from a hold.
        if (!input.Use && !input.UseReleased)
            miner.PlantHold = 0f;

        if (UpdateRescue(ref miner, ref position, input.Rescue, deltaSeconds, commands) || miner.IsMoving)
            return;

        if (miner.IsRiding)
        {
            miner.PlantHold = 0f;
            if (Ride(ref miner, input))
                return;
        }

        if (!mine.IsSupported(miner.Column, miner.Row))
        {
            miner.ClimbStreak = 0;
            StartMove(ref miner, 0, 1, FallDuration, MinerMotion.Falling);
            return;
        }

        if (UpdatePlanting(ref miner, input, deltaSeconds, commands))
            return;

        if (input.PlaceLadder)
            PlaceLadder(ref miner, commands);

        if (input.Horizontal != 0 && input.Up && CanJump(miner.Column, miner.Row, input.Horizontal))
        {
            miner.ClimbStreak = 0;
            miner.FacingX = input.Horizontal;
            miner.LampDirection = new Vector2(input.Horizontal, 0f);
            StartMove(ref miner, input.Horizontal * JumpTiles, 0, JumpDuration, MinerMotion.Jumping);
            return;
        }

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
            MoveOrDig(ref miner, 0, -1, ClimbUpDuration(miner.ClimbStreak) * Shop.ClimbLoad(miner),
                MinerMotion.Climbing, commands);
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

    /// <summary>
    /// Whether Up + a side key jumps from this tile: the tile beside and the one beyond it (<see cref="JumpTiles"/>)
    /// are open. The miner lands there and the fall rule takes over if it has no floor, so a jump reaches the ground
    /// across a hole, or the next hole.
    /// </summary>
    public bool CanJump(int column, int row, int directionX)
    {
        for (var step = 1; step <= JumpTiles; step++)
        {
            if (mine.IsSolid(column + directionX * step, row))
                return false;
        }

        return true;
    }

    /// <summary>Whether a climb with this streak is past the start speed (it slides without easing).</summary>
    public static bool IsFastClimb(int streak) => streak > ClimbRampStart;

    /// <summary>Whether a climb with this streak is at the top speed (it trails dust).</summary>
    public static bool IsTopSpeedClimb(int streak) => streak >= ClimbRampStart + ClimbRampTiles;

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Has the kit and a stick: holding the use key underground plants one.</summary>
    public static bool CanPlant(in Miner miner) => miner.HasBlastingKit && miner.Dynamite > 0;

    /// <summary>
    /// Holding the use key underground for <see cref="PlantDuration"/> plants a lit stick in the tile the lamp faces.
    /// An arrow held with it aims: down, up or to a side (the lamp turns there). On a lift landing only with
    /// <see cref="CanPlant"/>, because otherwise <c>E</c> there is the lift's key. Needs the blasting kit, a stick and
    /// rock there; otherwise a dust puff. While the key is held the miner does nothing else (true). After planting, the
    /// key must be released before the next stick.
    /// </summary>
    private bool UpdatePlanting(ref Miner miner, in MinerInput input, float deltaSeconds, CommandBuffer commands)
    {
        if (!input.Use)
            return false;

        var onLanding = miner.Column == Elevator.Column && elevator.IsLanding(miner.Row);
        if (Mine.Depth(miner.Row) <= 0 || onLanding && !CanPlant(miner))
        {
            miner.PlantHold = 0f;
            return false;
        }

        if (miner.PlantHold < 0f)
            return false;

        Aim(ref miner, input);
        miner.PlantHold += deltaSeconds;
        if (miner.PlantHold < PlantDuration)
            return true;

        miner.PlantHold = -1f;
        var column = miner.Column + (int)MathF.Round(miner.LampDirection.X);
        var row = miner.Row + (int)MathF.Round(miner.LampDirection.Y);
        var type = mine.TypeAt(column, row);
        var feet = Layout.TileCenter(miner.Column, miner.Row) + new Vector2(0, Layout.TileHeight / 2f);
        if (!miner.HasBlastingKit || miner.Dynamite <= 0 || !Mine.IsInside(column, row) ||
            !Tiles.IsBreakable(type) && !Tiles.IsBlastOnly(type))
        {
            effects.FootDust(commands, feet, miner.FacingX);
            return true;
        }

        miner.Dynamite--;
        miner.LitDynamite++;
        effects.PlantDynamite(commands, column, row, FuseDuration);
        return true;
    }

    /// <summary>An arrow held while planting turns the lamp, and so the stick, that way. Down wins, then up.</summary>
    private static void Aim(ref Miner miner, in MinerInput input)
    {
        if (input.Down)
            miner.LampDirection = Vector2.UnitY;
        else if (input.Up)
            miner.LampDirection = -Vector2.UnitY;
        else if (input.Horizontal != 0)
        {
            miner.FacingX = input.Horizontal;
            miner.LampDirection = new Vector2(input.Horizontal, 0f);
        }
    }

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

        var type = mine.TypeAt(column, row);
        if (miner.DigCooldown > 0f || !Tiles.IsBreakable(type) && !Tiles.IsBlastOnly(type))
            return;

        miner.DigCooldown = Upgrades.DigInterval(miner.ShovelTier);
        miner.SwingTime = SwingDuration;
        miner.SwingDirection = new Point(dx, dy);
        if (Tiles.IsBlastOnly(type))
        {
            Clank(column, row, miner.SwingDirection, commands);
            return;
        }

        var result = mine.Hit(column, row, Upgrades.FewerHits(miner.PickaxeTier));
        effects.DigHit(commands, column, row, result.Type, miner.SwingDirection);

        if (result.Outcome != DigOutcome.Broken)
            return;

        effects.TileBreak(commands, column, row, result.Type);
        if (Tiles.IsRareFind(result.Type))
            effects.RareFind(commands, column, row, result.Type);

        Shop.Collect(ref miner, result.Type);
    }

    /// <summary>The shovel bounces off obsidian: sparks, no damage, and now and then a floating "TOO HARD".</summary>
    private void Clank(int column, int row, Point direction, CommandBuffer commands)
    {
        effects.Clank(commands, column, row, direction);
        if (_tooHardCooldown > 0f)
            return;

        _tooHardCooldown = TooHardInterval;
        effects.Text(commands, Layout.TileCenter(column - direction.X, row - direction.Y) - new Vector2(0f, 2f),
            FloatingTextKind.TooHard, 0);
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

    /// <summary>
    /// The ladder key: takes the ladder in the miner's tile back into the inventory if there is one, otherwise places
    /// one there. A refused place is a dust puff.
    /// </summary>
    private void PlaceLadder(ref Miner miner, CommandBuffer commands)
    {
        var feet = Layout.TileCenter(miner.Column, miner.Row) + new Vector2(0, Layout.TileHeight / 2f);
        if (mine.TryTakeLadder(miner.Column, miner.Row))
        {
            miner.Ladders++;
            miner.ClimbStreak = 0;
            effects.FootDust(commands, feet, -miner.FacingX);
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

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
        Shop.EmptyBag(ref miner);
        miner.Column = Layout.SpawnColumn;
        miner.Row = Layout.GroundRow;
        miner.MoveFrom = new Vector2(miner.Column, miner.Row);
        miner.MoveDuration = 0f;
        miner.MoveElapsed = 0f;
        miner.Motion = MinerMotion.Idle;
        miner.FallRows = 0;
        miner.ClimbStreak = 0;
        miner.IsRiding = false;
        miner.PlantHold = 0f;
        position.Value = Layout.TileToWorld(miner.Column, miner.Row);

        effects.Pop(commands, Layout.TileCenter(miner.Column, miner.Row));
        effects.LandingDust(commands, Layout.TileToWorld(miner.Column, miner.Row + 1) + new Vector2(2, 0), 2);
        return true;
    }
}
