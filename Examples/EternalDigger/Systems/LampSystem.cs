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
/// from the oil (a carried flask is poured in when it runs low; no light and no burning while a gas flare has blown the
/// lamp out; shorter below the wall and in black damp, where it also burns twice the oil), flicker frames when low (on
/// a timer, so the rate doesn't depend on the FPS), and smoke rising from a low lamp. Runs before
/// <see cref="LightingSystem"/>.
/// </summary>
public sealed class LampSystem(Headlamp headlamp, EffectSpawner effects, Random random, Mine mine = null)
    : EntitySystem
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
    private bool _wasInBlackDamp;

    /// <summary>The miner stood in black damp this frame.</summary>
    public bool InBlackDamp { get; private set; }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            ref var miner = ref chunk.GetSpan<Miner>()[0];
            var position = chunk.GetSpan<Position>()[0].Value;
            InBlackDamp = mine != null && mine.IsBlackDamp(miner.Column, miner.Row);
            if (InBlackDamp && !_wasInBlackDamp)
                effects.Text(Commands, position + new Vector2(Layout.TileWidth / 2f, -2f), FloatingTextKind.BlackDamp, 0);

            _wasInBlackDamp = InBlackDamp;
            if (miner.LampOut > 0f)
                miner.LampOut = Math.Max(0f, miner.LampOut - deltaSeconds);
            else
                Burn(ref miner, deltaSeconds, LoadFactor(miner) * (InBlackDamp ? LampOil.BlackDampBurn : 1f));

            var poured = LampOil.TryPour(ref miner);
            if (poured > 0f)
                Pour(miner, chunk.GetSpan<Position>()[0].Value, poured);

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

    /// <summary>A flask went into the lamp: a puff of smoke and a floating <c>OIL +40</c>.</summary>
    private void Pour(in Miner miner, Vector2 position, float poured)
    {
        effects.LampSmoke(Commands, LampCell(miner, position));
        effects.Text(Commands, position + new Vector2(Layout.TileWidth / 2f, -2f), FloatingTextKind.Oil,
            (int)MathF.Round(poured));
    }

    private void Update(in Miner miner, Vector2 position, float deltaSeconds)
    {
        // Blown out by a gas flare: dark like an empty lamp until it relights.
        var blownOut = miner.LampOut > 0f;
        var belowTheWall = mine != null && mine.IsBelowTheWall(miner.Column, miner.Row);
        headlamp.Reach = blownOut ? 0f
            : LampOil.DarkReach(LampOil.Reach(miner.LampTier, miner.Oil), belowTheWall, InBlackDamp);
        headlamp.IsEmpty = blownOut || LampOil.IsEmpty(miner.Oil);
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
