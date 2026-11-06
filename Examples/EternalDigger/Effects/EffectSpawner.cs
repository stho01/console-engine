using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Effects;

/// <summary>
/// Creates the game's effect particles and floating numbers. Every method records entities in the command buffer it
/// is given (a system's <c>Commands</c>), so it is safe inside query loops. Positions are world cells. Console cells
/// are about twice as tall as wide, so vertical speeds and gravity are about half the horizontal ones.
/// </summary>
public sealed class EffectSpawner(Random random, ScreenShake shake)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float DebrisGravity = 40f;
    private const float CoinGravity = 45f;

    //**********************************************************
    //** props
    //**********************************************************

    public ScreenShake Shake { get; } = shake;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One shovel hit: chunks fly out of the struck face back towards the miner. <paramref name="digDirection"/> is
    /// the tile offset from the miner to the hit tile.
    /// </summary>
    public void DigHit(CommandBuffer commands, int column, int row, TileType type, Point digDirection)
    {
        // Just outside the struck face, on the miner's side, so the debris starts in air.
        var face = Layout.TileCenter(column, row) - new Vector2(digDirection.X * 2.5f, digDirection.Y * 1.5f);
        var style = ParticleLooks.DebrisStyle(type);
        var count = random.Next(4, 8);
        for (var i = 0; i < count; i++)
        {
            var velocity = new Vector2(
                -digDirection.X * Range(10f, 26f) + Range(-8f, 8f),
                -digDirection.Y * Range(4f, 10f) - Range(4f, 12f));
            Debris(commands, face, velocity, style, Range(.5f, 1.1f));
        }

        if (!Tiles.IsOre(type))
            return;

        var sparks = random.Next(2, 5);
        for (var i = 0; i < sparks; i++)
            Spawn(commands, face, RandomDirection(14f, 30f), ParticleStyle.Spark, Range(.2f, .45f), 0f, 3f);
    }

    /// <summary>The tile broke: a burst of chunks, a dust cloud and a small shake. Ore adds glitter.</summary>
    public void TileBreak(CommandBuffer commands, int column, int row, TileType type)
    {
        var center = Layout.TileCenter(column, row);
        var style = ParticleLooks.DebrisStyle(type);
        var chunks = random.Next(14, 23);
        for (var i = 0; i < chunks; i++)
        {
            var velocity = RandomDirection(8f, 28f) - new Vector2(0f, Range(4f, 10f));
            Debris(commands, center + RandomOffset(1.5f, .8f), velocity, style, Range(.8f, 1.6f));
        }

        var dust = random.Next(6, 11);
        for (var i = 0; i < dust; i++)
            Spawn(commands, center + RandomOffset(2f, 1f), RandomDirection(2f, 7f), ParticleStyle.Dust,
                Range(.5f, 1f), -2f, 1.5f);

        var ore = Tiles.IsOre(type);
        Shake.Add(ore ? .9f : .6f);
        if (!ore)
            return;

        var glitter = random.Next(6, 11);
        for (var i = 0; i < glitter; i++)
            Spawn(commands, center, RandomDirection(16f, 36f), ParticleStyle.Spark, Range(.3f, .7f), 6f, 2.5f);
    }

    /// <summary>One or two puffs kicked back from the feet.</summary>
    public void FootDust(CommandBuffer commands, Vector2 feet, int directionX)
    {
        var count = random.Next(1, 3);
        for (var i = 0; i < count; i++)
            Spawn(commands, feet + RandomOffset(1f, 0f),
                new Vector2(directionX * Range(3f, 8f), -Range(.5f, 2f)), ParticleStyle.Dust, Range(.3f, .6f), 0f, 3f);
    }

    /// <summary>Puffs to both sides of the feet, more for longer falls. Long falls shake the screen.</summary>
    public void LandingDust(CommandBuffer commands, Vector2 feet, int rowsFallen)
    {
        var count = Math.Min(12, 2 + rowsFallen * 2);
        for (var i = 0; i < count; i++)
        {
            var side = (i & 1) == 0 ? -1f : 1f;
            Spawn(commands, feet, new Vector2(side * Range(4f, 12f), -Range(.5f, 3f)), ParticleStyle.Dust,
                Range(.3f, .7f), 0f, 3f);
        }

        if (rowsFallen >= 3)
            Shake.Add(Math.Min(1.2f, rowsFallen * .15f));
    }

    /// <summary>One twinkle over an ore vein.</summary>
    public void Glint(CommandBuffer commands, Vector2 position, TileType ore) =>
        commands.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            new Glyph('·', ConsoleColor.White),
            new Particle(ParticleStyle.Glint, Range(.5f, .8f)) { Variant = ParticleLooks.GlintVariant(ore) },
            new Physics(0f, 0f));

    /// <summary>One slow mist particle drifting through a dug-out cave.</summary>
    public void Mist(CommandBuffer commands, Vector2 position) =>
        commands.Create(
            new Position(position),
            new Velocity(new Vector2(Range(-1.2f, 1.2f), Range(-.4f, .4f))),
            new Glyph('·', ConsoleColor.DarkGray),
            new Particle(ParticleStyle.Mist, Range(4f, 8f)) { Variant = (byte)random.Next(256) },
            new Physics(0f, 0f),
            new Components.Mist());

    /// <summary>Coins burst up and cascade down onto the grass.</summary>
    public void Coins(CommandBuffer commands, Vector2 origin, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var velocity = new Vector2(Range(-16f, 16f), -Range(10f, 22f));
            commands.Create(
                new Position(origin + RandomOffset(1.5f, .5f)),
                new Velocity(velocity),
                new Glyph('●', ConsoleColor.Yellow),
                new Particle(ParticleStyle.Coin, Range(1.6f, 2.4f)) { Variant = (byte)random.Next(256) },
                new Physics(CoinGravity, .3f),
                new TileCollider());
        }
    }

    /// <summary>A ring of stars.</summary>
    public void Pop(CommandBuffer commands, Vector2 center)
    {
        var count = random.Next(8, 13);
        for (var i = 0; i < count; i++)
        {
            var angle = MathF.Tau * i / count + Range(-.2f, .2f);
            var velocity = new Vector2(MathF.Cos(angle) * 24f, MathF.Sin(angle) * 12f);
            Spawn(commands, center, velocity, ParticleStyle.Pop, Range(.35f, .5f), 0f, 4f);
        }
    }

    /// <summary>Floating <c>+150g</c> / <c>-10g</c> that drifts up.</summary>
    public void Number(CommandBuffer commands, Vector2 position, int amount) =>
        commands.Create(new Position(position), new Velocity(new Vector2(0f, -5f)), new FloatingNumber(amount, 1.4f));

    //**********************************************************
    //** internals
    //**********************************************************

    private void Debris(CommandBuffer commands, Vector2 position, Vector2 velocity, ParticleStyle style, float lifetime) =>
        commands.Create(
            new Position(position),
            new Velocity(velocity),
            new Glyph('■', ConsoleColor.DarkYellow),
            new Particle(style, lifetime) { Variant = (byte)random.Next(256) },
            new Physics(DebrisGravity, .4f),
            new TileCollider());

    private void Spawn(CommandBuffer commands, Vector2 position, Vector2 velocity, ParticleStyle style,
        float lifetime, float gravity, float drag) =>
        commands.Create(
            new Position(position),
            new Velocity(velocity),
            new Glyph(' ', ConsoleColor.Gray),
            new Particle(style, lifetime) { Variant = (byte)random.Next(256) },
            new Physics(gravity, drag));

    private float Range(float min, float max) => min + random.NextSingle() * (max - min);

    /// <summary>A random direction with a speed in the range, squashed vertically to match the cell shape.</summary>
    private Vector2 RandomDirection(float minSpeed, float maxSpeed)
    {
        var angle = random.NextSingle() * MathF.Tau;
        var speed = Range(minSpeed, maxSpeed);
        return new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed * .5f);
    }

    private Vector2 RandomOffset(float x, float y) => new(Range(-x, x), Range(-y, y));
}
