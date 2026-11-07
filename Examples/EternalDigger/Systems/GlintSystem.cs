using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Ore veins near the miner twinkle now and then, also through rock and in the dark: that is how ore is found.
/// Samples a few random tiles around the miner at a fixed rate, so the glint rate doesn't depend on the FPS. Ore in
/// the dark glints more, gold more than silver, silver more than copper.
/// </summary>
public sealed class GlintSystem(Mine mine, LightMap light, Camera camera, EffectSpawner effects, Random random)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Seconds between glint samples, and their radius in tiles, with the starting detector.</summary>
    public const float Interval = .08f;
    public const int Radius = 9;

    private const int SamplesPerTick = 6;

    /// <summary>Extra gem samples per tick once the miner is past <see cref="Tiles.HintDepth"/>.</summary>
    private const int HintSamplesPerTick = 3;

    /// <summary>How many rows below the wall the hint samples reach.</summary>
    private const int HintReach = 16;

    private Query _miners;
    private float _elapsed;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _elapsed += deltaSeconds;
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            var interval = Upgrades.GlintInterval(miner.DetectorTier);
            var radius = Upgrades.GlintRadius(miner.DetectorTier);
            for (; _elapsed >= interval; _elapsed -= interval)
            {
                Sample(miner.Column, miner.Row, radius);
                if (Mine.Depth(miner.Row) >= Tiles.HintDepth)
                    SampleBelowTheWall();
            }

            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Sample(int minerColumn, int minerRow, int radius)
    {
        var firstRow = (int)MathF.Floor(camera.Position.Y / Layout.TileHeight);
        var lastRow = firstRow + Layout.ScreenHeight / Layout.TileHeight;
        for (var i = 0; i < SamplesPerTick; i++)
        {
            var column = minerColumn + random.Next(-radius, radius + 1);
            var row = minerRow + random.Next(-radius, radius + 1);
            if (row < firstRow || row > lastRow || !Mine.IsInside(column, row))
                continue;

            var type = mine.TypeAt(column, row);
            if (!Tiles.IsOre(type) || random.NextSingle() >= Chance(type, light.Get(column, row)))
                continue;

            var cell = Layout.TileToWorld(column, row) +
                       new Vector2(random.Next(Layout.TileWidth), random.Next(Layout.TileHeight));
            effects.Glint(Commands, cell, type);
        }
    }

    /// <summary>
    /// Gems just below the wall glint on screen however far the miner is from them, so they are seen before the wall
    /// is reached.
    /// </summary>
    private void SampleBelowTheWall()
    {
        var firstRow = (int)MathF.Floor(camera.Position.Y / Layout.TileHeight);
        var lastRow = firstRow + Layout.ScreenHeight / Layout.TileHeight;
        var wallTop = Mine.SurfaceRow + Tiles.WallDepth;
        for (var i = 0; i < HintSamplesPerTick; i++)
        {
            var column = random.Next(1, Mine.Columns - 1);
            var row = wallTop + Tiles.WallMinThickness + random.Next(HintReach);
            if (row < firstRow || row > lastRow || mine.TypeAt(column, row) != TileType.Gem ||
                random.NextSingle() >= Chance(TileType.Gem, light.Get(column, row)))
                continue;

            var cell = Layout.TileToWorld(column, row) +
                       new Vector2(random.Next(Layout.TileWidth), random.Next(Layout.TileHeight));
            effects.Glint(Commands, cell, TileType.Gem);
        }
    }

    private static float Chance(TileType ore, byte lightLevel)
    {
        var chance = ore switch
        {
            TileType.Gem => .9f,
            TileType.Gold => .9f,
            TileType.Silver => .6f,
            _ => .4f
        };

        return lightLevel == LightMap.Dark ? chance : chance * .6f;
    }
}
