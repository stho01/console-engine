using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>
/// Turns the rules' events into particles: debris from smashed stone, dust from jumps and hard landings, sparks from
/// levers and moving gates, and a shower of gold over the vikings once the level is won. Effects only: nothing here
/// changes the game.
/// </summary>
public sealed class EffectSystem(Expedition expedition, Random random) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float WinSparklesPerSecond = 40f;

    private Query _actors;
    private float _winSparkleDebt;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _actors = World.Query(Query.With<Actor>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var events = expedition.Events.All;
        for (var i = 0; i < events.Length; i++)
            Spawn(events[i]);
        expedition.Events.Clear();

        if (expedition.Outcome == Outcome.Won)
            SpawnWinSparkles(deltaSeconds);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Spawn(in PuzzleEvent e)
    {
        switch (e.Kind)
        {
            case PuzzleEventKind.Smashed:
                Burst(e.Position, 14, 6f, ConsoleColor.DarkYellow, ConsoleColor.DarkGray, 40f, .9f, .5f);
                Burst(e.Position, 6, 4f, ConsoleColor.Gray, ConsoleColor.DarkGray, -2f, .7f, .5f);
                break;
            case PuzzleEventKind.Jumped:
                Dust(e.Position, 3);
                break;
            case PuzzleEventKind.Landed:
            case PuzzleEventKind.CrateLanded:
                Dust(e.Position, 7);
                break;
            case PuzzleEventKind.LeverPulled:
            case PuzzleEventKind.GateMoved:
                var color = Looks.ChannelColors[e.Channel];
                Burst(e.Position, 6, 3f, ConsoleColor.White, color, 10f, .45f, .4f);
                break;
            case PuzzleEventKind.KnockedOut:
                Burst(e.Position, 10, 5f, ConsoleColor.Yellow, ConsoleColor.DarkYellow, 15f, .8f, .2f);
                break;
        }
    }

    /// <summary>Little puffs that roll out sideways along the ground and rise.</summary>
    private void Dust(Vector2 feet, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var side = i % 2 == 0 ? -1f : 1f;
            Create(new Particle
            {
                Position = feet + new Vector2(side * random.NextSingle() * .3f, -.1f),
                Velocity = new Vector2(side * (1f + random.NextSingle() * 2f), -random.NextSingle() * 1.2f),
                Gravity = -1f,
                Life = .35f + random.NextSingle() * .3f,
                Color = ConsoleColor.Gray,
                FadeColor = ConsoleColor.DarkGray
            });
        }
    }

    private void Burst(Vector2 center, int count, float speed, ConsoleColor color, ConsoleColor fade, float gravity,
        float life, float spread)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var power = speed * (.4f + random.NextSingle() * .6f);
            Create(new Particle
            {
                Position = center + new Vector2(random.NextSingle() - .5f, random.NextSingle() - .5f) * spread,
                Velocity = new Vector2(MathF.Cos(angle) * power, MathF.Sin(angle) * power - speed * .3f),
                Gravity = gravity,
                Life = life * (.6f + random.NextSingle() * .4f),
                Color = color,
                FadeColor = fade
            });
        }
    }

    private void SpawnWinSparkles(float deltaSeconds)
    {
        _winSparkleDebt += WinSparklesPerSecond * deltaSeconds;
        while (_winSparkleDebt >= 1f)
        {
            _winSparkleDebt -= 1f;
            var hero = random.Next(Heroes.Order.Length);
            foreach (var chunk in _actors.GetChunks())
            {
                var actors = chunk.GetSpan<Actor>();
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (actors[i].Kind != Heroes.Order[hero])
                        continue;

                    var top = actors[i].Min + new Vector2(actors[i].Size.X / 2f, -.3f);
                    Burst(top, 1, 4f, random.Next(2) == 0 ? ConsoleColor.Yellow : ConsoleColor.White,
                        ConsoleColor.DarkYellow, 6f, 1.1f, 1.2f);
                }
            }
        }
    }

    private void Create(Particle particle)
    {
        particle.MaxLife = particle.Life;
        Commands.Create(particle);
    }
}
