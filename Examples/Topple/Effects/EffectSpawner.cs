using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Rendering;

namespace Topple.Effects;

/// <summary>
/// Spawns what makes hits feel heavy: puffs (drawn only), splinters and sparks (physics bodies that only hit the
/// ground), fireballs and screen shake. Everything goes through a <see cref="CommandBuffer"/>.
/// </summary>
public sealed class EffectSpawner(Random random, ScreenShake shake)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int SplintersPerBlock = 4;
    public const int SparksPerBlast = 12;
    public const float BlastShake = .9f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>A block broke: splinters, a dust cloud in its colour and a small jolt of the screen.</summary>
    public void Shatter(CommandBuffer commands, Vector2 position, ConsoleColor color, float shakeAmount)
    {
        for (var i = 0; i < SplintersPerBlock; i++)
            BodyFactory.CreateDebris(commands, position, RandomUpwards(12f), '·', color, .8f + random.NextSingle() * .6f);
        Dust(commands, position, color, 6);
        shake.Add(shakeAmount);
    }

    /// <summary>A bomb went off: fireball, flames, smoke, sparks and a big shake.</summary>
    public void Explosion(CommandBuffer commands, Vector2 center)
    {
        commands.Create(new Position(center), new Burst(Impact.BlastRadius * .75f, .55f));
        for (var i = 0; i < 10; i++)
            Spawn(commands, PuffKind.Flame, center, RandomDirection(14f), .5f, ConsoleColor.Yellow);
        for (var i = 0; i < 8; i++)
            Spawn(commands, PuffKind.Smoke, center, RandomDirection(6f) - Vector2.UnitY * 3f, 1.4f, ConsoleColor.Gray);
        for (var i = 0; i < SparksPerBlast; i++)
        {
            var color = (i & 1) == 0 ? ConsoleColor.Red : ConsoleColor.Yellow;
            BodyFactory.CreateDebris(commands, center, RandomUpwards(22f), '*', color, .4f + random.NextSingle() * .5f);
        }

        shake.Add(BlastShake);
    }

    public void Dust(CommandBuffer commands, Vector2 position, ConsoleColor color, int count)
    {
        for (var i = 0; i < count; i++)
            Spawn(commands, PuffKind.Dust, position, RandomDirection(5f), .5f + random.NextSingle() * .5f, color);
    }

    /// <summary>One puff behind a flying shot.</summary>
    public void Trail(CommandBuffer commands, Vector2 position, ShotKind kind)
    {
        if (kind == ShotKind.Bomb)
            Spawn(commands, PuffKind.Flame, position, RandomDirection(1.5f), .25f, ConsoleColor.Yellow);
        else
            Spawn(commands, PuffKind.Smoke, position, RandomDirection(.8f), .45f, ConsoleColor.Gray);
    }

    public void Splash(CommandBuffer commands, Vector2 position)
    {
        for (var i = 0; i < 8; i++)
            Spawn(commands, PuffKind.Spray, position, RandomUpwards(9f), .6f, ConsoleColor.Cyan);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Spawn(CommandBuffer commands, PuffKind kind, Vector2 position, Vector2 velocity, float life,
        ConsoleColor color) =>
        commands.Create(new Position(position), new Puff(kind, velocity, life * (.8f + random.NextSingle() * .4f), color));

    private Vector2 RandomDirection(float speed)
    {
        var angle = random.NextSingle() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (speed * (.3f + random.NextSingle() * .7f));
    }

    /// <summary>A direction in the upper half (screen y points down).</summary>
    private Vector2 RandomUpwards(float speed)
    {
        var angle = MathF.PI * (1f + random.NextSingle());
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (speed * (.5f + random.NextSingle() * .5f));
    }
}
