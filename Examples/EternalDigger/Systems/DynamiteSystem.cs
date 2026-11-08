using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Burns the fuses of planted <see cref="Dynamite"/> (sparks and smoke at a fixed rate) and blows them up. A blast
/// clears the 3 x 3 tiles around the stick: any rock, obsidian included, but not bedrock, the grass or the sky. Ore in
/// it goes into the bag, ladders in it are destroyed, other sticks in it go off right after, and a miner inside is
/// pushed one tile away from the stick. Gas within <see cref="GasReach"/> flares up big. The miner is never hurt.
/// </summary>
public sealed class DynamiteSystem(Mine mine, EffectSpawner effects, GasPockets gas = null) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Tiles from the stick a blast reaches: 1 is a 3 x 3 area.</summary>
    public const int BlastRadius = 1;

    /// <summary>Seconds left on a stick's fuse when a blast reaches it.</summary>
    public const float ChainDelay = .12f;

    /// <summary>Seconds a blast push takes to slide the miner one tile.</summary>
    public const float PushDuration = .1f;

    private const float SparkInterval = .07f;

    /// <summary>Tiles from the stick within which a blast sets gas off (a bigger flare, still harmless).</summary>
    public const float GasReach = 3f;

    /// <summary>Pockets one blast can set off.</summary>
    private const int MaxPocketsPerBlast = 6;

    private readonly GasPockets _gas = gas ?? new GasPockets(mine);

    private Query _sticks;
    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _sticks = World.Query(Query.With<Dynamite, Position>());
        _miners = World.Query(Query.With<Miner>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var minerChunk in _miners.GetChunks())
        {
            Burn(ref minerChunk.GetSpan<Miner>()[0], deltaSeconds);
            return;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Clears the blast area around a tile, bags its ore, destroys its ladders and pushes the miner out. Returns the
    /// number of tiles cleared.
    /// </summary>
    public int Blast(int column, int row, ref Miner miner, CommandBuffer commands)
    {
        var cleared = 0;
        for (var r = row - BlastRadius; r <= row + BlastRadius; r++)
        for (var c = column - BlastRadius; c <= column + BlastRadius; c++)
        {
            if (!Mine.IsInside(c, r) || r <= Mine.SurfaceRow)
                continue;

            ref var cell = ref mine.At(c, r);
            if (Tiles.IsBreakable(cell.Type) || Tiles.IsBlastOnly(cell.Type))
            {
                Shop.Collect(ref miner, cell.Type);
                cell = new TileCell { Type = TileType.Air, Seen = true };
                cleared++;
            }
            else if (cell.Type == TileType.Air)
            {
                cell.HasLadder = false;
            }
        }

        Push(ref miner, column, row);
        effects.Blast(commands, column, row, BlastRadius);
        IgniteGas(column, row, commands);
        return cleared;
    }

    /// <summary>Gas near a blast flares up big: no lamp is blown out and nobody is hurt.</summary>
    private void IgniteGas(int column, int row, CommandBuffer commands)
    {
        for (var pocket = 0; pocket < MaxPocketsPerBlast; pocket++)
        {
            if (!_gas.TryFindNear(column, row, GasReach, out var found))
                return;

            _gas.Ignite(found.X, found.Y);
            var tiles = _gas.Burned;
            for (var i = 0; i < tiles.Length; i++)
                effects.GasFlare(commands, tiles[i].X, tiles[i].Y, big: true);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Burn(ref Miner miner, float deltaSeconds)
    {
        foreach (var chunk in _sticks.GetChunks())
        {
            var sticks = chunk.GetSpan<Dynamite>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var stick = ref sticks[i];
                if (stick.Fuse <= 0f)
                    continue;

                stick.Fuse -= deltaSeconds;
                for (stick.SparkTimer -= deltaSeconds; stick.SparkTimer <= 0f; stick.SparkTimer += SparkInterval)
                    effects.FuseSpark(Commands, positions[i].Value);

                if (stick.Fuse > 0f)
                    continue;

                Commands.Destroy(chunk.Entities[i]);
                miner.LitDynamite = Math.Max(0, miner.LitDynamite - 1);
                Blast(stick.Column, stick.Row, ref miner, Commands);
                ShortenFusesNear(stick.Column, stick.Row);
            }
        }
    }

    /// <summary>Sticks inside a blast go off right after it.</summary>
    private void ShortenFusesNear(int column, int row)
    {
        foreach (var chunk in _sticks.GetChunks())
        {
            var sticks = chunk.GetSpan<Dynamite>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var other = ref sticks[i];
                if (other.Fuse > 0f && Math.Abs(other.Column - column) <= BlastRadius &&
                    Math.Abs(other.Row - row) <= BlastRadius)
                    other.Fuse = Math.Min(other.Fuse, ChainDelay);
            }
        }
    }

    /// <summary>A miner inside the blast slides one tile away from the stick, if that tile is open.</summary>
    private void Push(ref Miner miner, int column, int row)
    {
        var dx = miner.Column - column;
        var dy = miner.Row - row;
        if (miner.IsMoving || miner.IsRiding || Math.Abs(dx) > BlastRadius || Math.Abs(dy) > BlastRadius)
            return;

        var pushX = dx != 0 ? Math.Sign(dx) : dy == 0 ? -miner.FacingX : 0;
        var pushY = pushX == 0 ? Math.Sign(dy) : 0;
        if (mine.IsSolid(miner.Column + pushX, miner.Row + pushY))
            return;

        miner.MoveFrom = new Vector2(miner.Column, miner.Row);
        miner.Column += pushX;
        miner.Row += pushY;
        miner.MoveElapsed = 0f;
        miner.MoveDuration = PushDuration;
        miner.Motion = pushY > 0 ? MinerMotion.Falling : MinerMotion.Walking;
    }
}
