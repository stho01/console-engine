using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Swarm.Components;

namespace Swarm;

/// <summary>Creates particles at random positions on screen with a random velocity.</summary>
public sealed class ParticleSpawner(int width, int height)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float MaxStartSpeed = 20f;
    private const char Dot = '•';
    private const float MinPull = 30f;
    private const float MaxPull = 180f;

    private readonly Random _random = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Creates them right away. Only call this outside a query loop (e.g. when the scene is set up).</summary>
    public void Create(World world, int count)
    {
        for (var i = 0; i < count; i++)
            world.Create(RandomPosition(), RandomVelocity(), RandomAgility(), new Glyph(Dot, ConsoleColor.DarkBlue),
                new Particle());
    }

    /// <summary>Records them in a command buffer, for systems.</summary>
    public void Create(CommandBuffer commands, int count)
    {
        for (var i = 0; i < count; i++)
            commands.Create(RandomPosition(), RandomVelocity(), RandomAgility(), new Glyph(Dot, ConsoleColor.DarkBlue),
                new Particle());
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Position RandomPosition() =>
        new(new Vector2(_random.NextSingle() * width, _random.NextSingle() * height));

    private Velocity RandomVelocity() =>
        new(new Vector2(_random.NextSingle() * 2f - 1f, _random.NextSingle() * 2f - 1f) * MaxStartSpeed);

    private Agility RandomAgility() => new(MinPull + _random.NextSingle() * (MaxPull - MinPull));
}
