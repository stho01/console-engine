using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Keeps up to <see cref="MaxMist"/> mist particles drifting through dug-out tiles on screen. Tries a few random
/// tiles per frame and spawns in lit ones first, so the mist shows where the lamp shines. Black damp has no mist.
/// </summary>
public sealed class MistSystem(Mine mine, LightMap light, Camera camera, EffectSpawner effects, Random random)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int MaxMist = 40;

    /// <summary>Mist spawned per second at most, so a fresh cave fills slowly instead of all at once.</summary>
    private const float SpawnRate = 12f;

    private const int TriesPerSpawn = 4;

    private Query _mist;
    private float _budget;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _mist = World.Query(Query.With<Mist>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _budget = Math.Min(1f, _budget + SpawnRate * deltaSeconds);
        if (_budget < 1f || _mist.Count >= MaxMist)
            return;

        _budget = 0f;
        var firstRow = Math.Max(Mine.SurfaceRow + 1, (int)MathF.Floor(camera.Position.Y / Layout.TileHeight));
        var lastRow = (int)MathF.Floor(camera.Position.Y / Layout.TileHeight) + Layout.ScreenHeight / Layout.TileHeight;
        if (lastRow < firstRow)
            return;

        for (var i = 0; i < TriesPerSpawn; i++)
        {
            var column = random.Next(1, Mine.Columns - 1);
            var row = random.Next(firstRow, lastRow + 1);
            if (mine.IsSolid(column, row) || light.Get(column, row) == LightMap.Dark || mine.IsBlackDamp(column, row))
                continue;

            var cell = Layout.TileToWorld(column, row) +
                       new Vector2(random.NextSingle() * Layout.TileWidth, random.NextSingle() * Layout.TileHeight);
            effects.Mist(Commands, cell);
            return;
        }
    }
}
