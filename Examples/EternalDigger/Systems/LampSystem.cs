using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Burns the lamp's oil while the miner is underground and works out this frame's <see cref="Headlamp"/>: the reach
/// from the oil, flicker frames when low (on a timer, so the rate doesn't depend on the FPS), and smoke rising from a
/// low lamp. Runs before <see cref="LightingSystem"/>.
/// </summary>
public sealed class LampSystem(Headlamp headlamp, EffectSpawner effects, Random random) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Seconds between flicker changes, at most; the emptier the tank, the more of them are dim.</summary>
    private const float FlickerStep = .12f;

    private const float SmokeInterval = .45f;

    private Query _miners;
    private float _flickerTimer;
    private float _smokeTimer;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            ref var miner = ref chunk.GetSpan<Miner>()[0];
            Burn(ref miner, deltaSeconds, LoadFactor(miner));
            Update(miner, chunk.GetSpan<Position>()[0].Value, deltaSeconds);
            return;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One frame of oil: burns underground only. <paramref name="loadFactor"/> is 1 unless the bag weighs on a climb.
    /// </summary>
    public static void Burn(ref Miner miner, float deltaSeconds, float loadFactor = 1f)
    {
        if (Mine.Depth(miner.Row) <= 0)
            return;

        miner.Oil = Math.Max(0f, miner.Oil - LampOil.BurnRate(miner.LampTier, loadFactor) * deltaSeconds);
    }

    /// <summary>A heavy bag burns more oil while climbing up (<see cref="Shop.OilLoad"/>); 1 otherwise.</summary>
    public static float LoadFactor(in Miner miner) =>
        miner.Motion == MinerMotion.Climbing && miner.Row < miner.MoveFrom.Y ? Shop.OilLoad(miner) : 1f;

    //**********************************************************
    //** internals
    //**********************************************************

    private void Update(in Miner miner, Vector2 position, float deltaSeconds)
    {
        headlamp.Reach = LampOil.Reach(miner.LampTier, miner.Oil);
        headlamp.IsEmpty = LampOil.IsEmpty(miner.Oil);
        headlamp.IsLow = LampOil.IsLow(miner.Oil) && Mine.Depth(miner.Row) > 0;
        if (!headlamp.IsLow || headlamp.IsEmpty)
        {
            headlamp.IsDimmed = false;
            return;
        }

        _flickerTimer -= deltaSeconds;
        if (_flickerTimer <= 0f)
        {
            var emptiness = 1f - miner.Oil / (LampOil.TankSize * LampOil.LowShare);
            headlamp.IsDimmed = random.NextSingle() < .2f + .5f * emptiness;
            _flickerTimer = FlickerStep * (.3f + random.NextSingle() * .7f);
        }

        for (_smokeTimer -= deltaSeconds; _smokeTimer <= 0f; _smokeTimer += SmokeInterval)
            effects.LampSmoke(Commands, LampCell(miner, position));
    }

    /// <summary>The world cell of the lamp glyph: the front of the miner's top row.</summary>
    private static Vector2 LampCell(in Miner miner, Vector2 position) =>
        position + new Vector2(miner.FacingX >= 0 ? Layout.TileWidth - .5f : .5f, -.2f);
}
