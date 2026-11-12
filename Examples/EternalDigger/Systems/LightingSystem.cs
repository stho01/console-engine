using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Computes the <see cref="LightMap"/> for the rows on screen from the miner's headlamp (its reach and flicker from
/// the <see cref="Headlamp"/>), plus the deep camp's lantern once it is built. Uses the sliding
/// <c>Position</c>, so the light moves smoothly with the miner.
/// </summary>
public sealed class LightingSystem(Mine mine, LightMap light, Camera camera, Headlamp headlamp) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int VisibleRows = Layout.ScreenHeight / Layout.TileHeight + 2;

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var firstRow = (int)MathF.Floor(camera.Position.Y / Layout.TileHeight);
        foreach (var chunk in _miners.GetChunks())
        {
            var position = chunk.GetSpan<Position>()[0].Value;
            var lamp = new Vector2(position.X / Layout.TileWidth + .5f, position.Y / Layout.TileHeight + .5f);
            var miner = chunk.GetSpan<Miner>()[0];
            light.Compute(mine, firstRow, VisibleRows, lamp, miner.LampDirection, headlamp.Reach, headlamp.IsDimmed);
            if (miner.HasDeepCamp)
                light.AddLantern(mine, DeepCamp.LanternCenter, DeepCamp.LanternRadius);
            return;
        }
    }
}
