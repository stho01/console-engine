using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Play;

namespace Doom.Systems;

/// <summary>
/// The living player takes every <see cref="Collectible"/> their body touches and can use now
/// (<see cref="Player.Give"/>): it disappears, the HUD says what it was, and the screen flashes gold.
/// </summary>
public sealed class PickupSystem(Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>How far above its feet the player reaches for a pickup.</summary>
    private const float PickupReach = 8;

    /// <summary>Gold tint added per pickup, and its most.</summary>
    private const int BonusFlash = 6;

    private const int MaxBonusFlash = 24;

    private Query _items;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _items = World.Query(Query.With<Thing, Body, Collectible>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var player = level.Player;
        if (player.Dead)
            return;

        ref var reacher = ref level.PlayerBody;
        foreach (var chunk in _items.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                ref var item = ref bodies[i];
                if (thing.Removed)
                    continue;

                var reach = reacher.Radius + item.Radius;
                if (MathF.Abs(item.X - reacher.X) >= reach || MathF.Abs(item.Y - reacher.Y) >= reach)
                    continue;
                if (item.Z > reacher.Z + reacher.Height || item.Z + PickupReach < reacher.Z)
                    continue;

                var info = ThingInfo.ById(thing.Type);
                if (!player.Give(info.Pickup, thing.Dropped))
                    continue;

                level.Say(info.Pickup.Message);
                level.Remove(entities[i]);
                player.BonusCount = Math.Min(player.BonusCount + BonusFlash, MaxBonusFlash);
                if (info.Has(ThingFlags.CountItem))
                    level.Stats.Items++;
            }
        }
    }
}
