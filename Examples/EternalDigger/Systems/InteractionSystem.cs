using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Saving;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// <c>E</c> on the surface and at the lift: deliver the bag at the door, buy the selected item at the shop counter
/// (Up/Down choose it there), build a lift segment at the lift sign; at a lift landing, call the car or get in. Only
/// while the miner stands still. Every sale and purchase asks for a save.
/// </summary>
public sealed class InteractionSystem(
    Mine mine, Elevator elevator, ShopCounter counter, EffectSpawner effects, GameSaver saver) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int GoldPerCoin = 10;
    private const int MaxCoins = 30;

    /// <summary>Where coins and texts come from: above the door / shop counter / lift sign, in world cells.</summary>
    public static readonly Vector2 DoorEffectOrigin = Layout.TileCenter(
        (Layout.DeliverFirstColumn + Layout.DeliverLastColumn) / 2, Layout.GroundRow) + new Vector2(2f, -6f);

    public static readonly Vector2 ShopEffectOrigin = Layout.TileCenter(
        (Layout.ShopFirstColumn + Layout.ShopLastColumn) / 2, Layout.GroundRow) + new Vector2(2f, -6f);

    public static readonly Vector2 LiftEffectOrigin = Layout.TileCenter(
        (Layout.LiftSignFirstColumn + Layout.LiftSignLastColumn) / 2, Layout.GroundRow) + new Vector2(2f, -6f);

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = MinerInput.FromKeyboard();
        if (!input.UsePressed && !input.UseReleased && !input.UpPressed && !input.DownPressed)
            return;

        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            for (var i = 0; i < chunk.Count; i++)
                Interact(ref miners[i], input, Commands);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One frame of <c>E</c> (and Up/Down at the counter) for the miner. Effects are recorded in
    /// <paramref name="commands"/>.
    /// </summary>
    public void Interact(ref Miner miner, in MinerInput input, CommandBuffer commands)
    {
        if (miner.IsMoving || miner.IsRiding)
            return;

        if (Layout.IsAtShop(miner.Column, miner.Row))
        {
            if (input.UpPressed)
                counter.Previous();
            else if (input.DownPressed)
                counter.Next();
        }

        var feet = Layout.TileToWorld(miner.Column, miner.Row + 1) + new Vector2(Layout.TileWidth / 2f, -.1f);
        if (IsTapToUseLift(miner))
        {
            // Holding E here plants dynamite, so the lift answers to a short tap: on release, before the fuse lights.
            if (input.UseReleased && miner.PlantHold >= 0f && miner.PlantHold < MinerControlSystem.PlantDuration)
                UseLift(ref miner, feet, commands);
            return;
        }

        if (!input.UsePressed)
            return;

        if (Layout.IsAtDeliverSpot(miner.Column, miner.Row))
            Deliver(ref miner, feet, commands);
        else if (Layout.IsAtShop(miner.Column, miner.Row))
            Buy(ref miner, feet, commands);
        else if (Layout.IsAtLiftSign(miner.Column, miner.Row))
            BuildLift(ref miner, feet, commands);
        else if (miner.Column == Elevator.Column && elevator.IsLanding(miner.Row))
            UseLift(ref miner, feet, commands);
    }

    /// <summary>
    /// On a lift landing underground (the bottom one) while carrying dynamite: <c>E</c> held plants a stick, so a tap
    /// uses the lift.
    /// </summary>
    public bool IsTapToUseLift(in Miner miner) =>
        miner.Column == Elevator.Column && elevator.IsLanding(miner.Row) && Mine.Depth(miner.Row) > 0 &&
        MinerControlSystem.CanPlant(miner);

    //**********************************************************
    //** internals
    //**********************************************************

    private void Deliver(ref Miner miner, Vector2 feet, CommandBuffer commands)
    {
        var earned = Shop.Deliver(ref miner);
        if (earned == 0)
        {
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

        effects.Coins(commands, DoorEffectOrigin, Math.Clamp(earned / GoldPerCoin, 3, MaxCoins));
        effects.Pop(commands, DoorEffectOrigin);
        effects.Number(commands, DoorEffectOrigin + new Vector2(0f, -2f), earned);
        saver.Request();
    }

    /// <summary>Buys the counter's selected item: stars and the price floating up, or a dust puff.</summary>
    private void Buy(ref Miner miner, Vector2 feet, CommandBuffer commands)
    {
        var price = Shop.Price(counter.Selected, miner);
        if (!Shop.TryBuy(counter.Selected, ref miner))
        {
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

        effects.Pop(commands, ShopEffectOrigin);
        effects.Number(commands, ShopEffectOrigin + new Vector2(0f, -2f), -price);
        saver.Request();
    }

    private void BuildLift(ref Miner miner, Vector2 feet, CommandBuffer commands)
    {
        if (elevator.TryBuild(mine, ref miner) != ElevatorBuild.Built)
        {
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

        effects.Pop(commands, LiftEffectOrigin);
        effects.Text(commands, LiftEffectOrigin + new Vector2(0f, -2f), FloatingTextKind.Elevator,
            Elevator.SegmentMetres);
        saver.Request();
    }

    /// <summary>Gets in if the car is here, otherwise calls it (the caller gets in when it arrives).</summary>
    private void UseLift(ref Miner miner, Vector2 feet, CommandBuffer commands)
    {
        if (elevator.IsCarAt(miner.Row))
        {
            miner.IsRiding = true;
            effects.FootDust(commands, feet, miner.FacingX);
            return;
        }

        elevator.Send(miner.Row, isCall: true);
    }
}
