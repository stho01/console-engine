using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Gas pockets in deep caves. Keeps up to <see cref="MaxShimmer"/> faint green shimmer particles over gas tiles near
/// the miner (seen in the dark too: gas is a warning, never a trap). A lit lamp within <see cref="TriggerRadius"/>
/// tiles of gas makes the pocket flare: a burst of fire and a shake, the gas is gone, and the lamp is blown out for
/// <see cref="LampOutSeconds"/> and loses <see cref="OilDrain"/> oil. The miner is never hurt. Runs before
/// <see cref="LampSystem"/>, so a blown-out lamp is dark in the same frame.
/// </summary>
public sealed class GasSystem(Mine mine, GasPockets gas, EffectSpawner effects, Random random) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Tiles from the miner's tile within which a lit lamp sets gas off.</summary>
    public const float TriggerRadius = 2f;

    /// <summary>Seconds a flare blows the lamp out.</summary>
    public const float LampOutSeconds = 3f;

    /// <summary>Oil a flare burns off.</summary>
    public const float OilDrain = 5f;

    public const int MaxShimmer = 30;

    /// <summary>Tiles around the miner where gas shimmers.</summary>
    public const int ShimmerRange = 9;

    /// <summary>Shimmer particles spawned per second at most.</summary>
    private const float ShimmerRate = 10f;

    private const int TriesPerSpawn = 6;

    private Query _miners;
    private Query _shimmer;
    private float _budget;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _miners = World.Query(Query.With<Miner>());
        _shimmer = World.Query(Query.With<Gas>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            ref var miner = ref chunk.GetSpan<Miner>()[0];
            Check(ref miner, Commands);
            Shimmer(miner, deltaSeconds);
            return;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Sets off the nearest pocket within <see cref="TriggerRadius"/> if the miner's lamp is lit. Returns the tiles
    /// burned (0: nothing happened).
    /// </summary>
    public int Check(ref Miner miner, CommandBuffer commands)
    {
        if (!IsLampLit(miner) || Mine.Depth(miner.Row) <= 0 ||
            !gas.TryFindNear(miner.Column, miner.Row, TriggerRadius, out var found))
            return 0;

        var burned = gas.Ignite(found.X, found.Y);
        var tiles = gas.Burned;
        for (var i = 0; i < tiles.Length; i++)
            effects.GasFlare(commands, tiles[i].X, tiles[i].Y, big: false);

        effects.Shake.Add(.6f);
        effects.Text(commands, Layout.TileCenter(found.X, found.Y) - new Vector2(0f, 2f), FloatingTextKind.Gas, 0);
        miner.LampOut = LampOutSeconds;
        miner.Oil = Math.Max(0f, miner.Oil - OilDrain);
        return burned;
    }

    /// <summary>The lamp is burning: not blown out by gas and not dry.</summary>
    public static bool IsLampLit(in Miner miner) => miner.LampOut <= 0f && !LampOil.IsEmpty(miner.Oil);

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A few random tiles near the miner per spawn; gas tiles get a shimmer particle.</summary>
    private void Shimmer(in Miner miner, float deltaSeconds)
    {
        _budget = Math.Min(1f, _budget + ShimmerRate * deltaSeconds);
        if (_budget < 1f || _shimmer.Count >= MaxShimmer || Mine.Depth(miner.Row) < Tiles.GasDepth - ShimmerRange)
            return;

        _budget = 0f;
        for (var i = 0; i < TriesPerSpawn; i++)
        {
            var column = miner.Column + random.Next(-ShimmerRange, ShimmerRange + 1);
            var row = miner.Row + random.Next(-ShimmerRange, ShimmerRange + 1);
            if (!Mine.IsInside(column, row) || !mine.At(column, row).HasGas)
                continue;

            var cell = Layout.TileToWorld(column, row) +
                       new Vector2(random.NextSingle() * Layout.TileWidth, random.NextSingle() * Layout.TileHeight);
            effects.GasShimmer(Commands, cell);
            return;
        }
    }
}
