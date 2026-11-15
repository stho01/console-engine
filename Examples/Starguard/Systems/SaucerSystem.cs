using System;
using ConsoleEngine.Ecs;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// Sends a saucer across the top every 15 to 25 seconds, from a random side. Never more than one at a time. The
/// engine's <c>MovementSystem</c> flies it, <see cref="BoundsSystem"/> removes it on the far side.
/// </summary>
public sealed class SaucerSystem(Match match, Spawner spawner, Random random) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MinDelaySeconds = 15f;
    public const float MaxDelaySeconds = 25f;

    private Query _saucers;
    private float _untilNextSaucer;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _saucers = World.Query(Query.With<Saucer>());
        _untilNextSaucer = NextDelay();
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (match.IsOver || _saucers.Count > 0)
            return;

        _untilNextSaucer -= deltaSeconds;
        if (_untilNextSaucer > 0f)
            return;

        spawner.CreateSaucer(Commands, fromLeft: random.Next(2) == 0, random);
        _untilNextSaucer = NextDelay();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private float NextDelay() =>
        MinDelaySeconds + (float)random.NextDouble() * (MaxDelaySeconds - MinDelaySeconds);
}
