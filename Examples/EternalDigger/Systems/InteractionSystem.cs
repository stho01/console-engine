using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using EternalDigger.Components;
using EternalDigger.Effects;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// <c>E</c> at the house: deliver the bag at the door, buy one ladder at the shopfront. Only while the miner stands
/// still on the spot.
/// </summary>
public sealed class InteractionSystem(EffectSpawner effects) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int GoldPerCoin = 10;
    private const int MaxCoins = 30;

    /// <summary>Where coins and numbers come from: above the door / shopfront, in world cells.</summary>
    public static readonly Vector2 DoorEffectOrigin = Layout.TileCenter(
        (Layout.DeliverFirstColumn + Layout.DeliverLastColumn) / 2, Layout.GroundRow) + new Vector2(2f, -6f);

    public static readonly Vector2 ShopEffectOrigin = Layout.TileCenter(
        (Layout.ShopFirstColumn + Layout.ShopLastColumn) / 2, Layout.GroundRow) + new Vector2(2f, -6f);

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (!Input.Instance.GetKey(Key.E).Pressed)
            return;

        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            for (var i = 0; i < chunk.Count; i++)
                Interact(ref miners[i]);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Interact(ref Miner miner)
    {
        if (miner.IsMoving)
            return;

        var feet = Layout.TileToWorld(miner.Column, miner.Row + 1) + new Vector2(Layout.TileWidth / 2f, -.1f);
        if (Layout.IsAtDeliverSpot(miner.Column, miner.Row))
        {
            var earned = Shop.Deliver(ref miner);
            if (earned == 0)
            {
                effects.FootDust(Commands, feet, miner.FacingX);
                return;
            }

            effects.Coins(Commands, DoorEffectOrigin, Math.Clamp(earned / GoldPerCoin, 3, MaxCoins));
            effects.Pop(Commands, DoorEffectOrigin);
            effects.Number(Commands, DoorEffectOrigin + new Vector2(0f, -2f), earned);
            return;
        }

        if (!Layout.IsAtShop(miner.Column, miner.Row))
            return;

        if (!Shop.TryBuyLadder(ref miner))
        {
            effects.FootDust(Commands, feet, miner.FacingX);
            return;
        }

        effects.Pop(Commands, ShopEffectOrigin);
        effects.Number(Commands, ShopEffectOrigin + new Vector2(0f, -2f), -Shop.LadderPrice);
    }
}
