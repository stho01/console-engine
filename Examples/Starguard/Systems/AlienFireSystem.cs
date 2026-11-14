using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// Every <see cref="ShotInterval"/> a random column of the formation fires from its lowest alien, as long as fewer than
/// <see cref="MaxShots"/> alien shots are falling. Holds fire while the cannon is down.
/// </summary>
public sealed class AlienFireSystem(Match match, Random random) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int MaxShots = 3;

    private Query _aliens;
    private Query _alienShots;
    private float _untilNextShot;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _aliens = World.Query(Query.With<Alien, Position>());
        _alienShots = World.Query(Query.With<AlienShot>());
        _untilNextShot = ShotInterval(match.Wave);
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (match.IsOver || match.CannonDown)
            return;

        _untilNextShot -= deltaSeconds;
        if (_untilNextShot > 0f)
            return;

        _untilNextShot = ShotInterval(match.Wave);
        var alive = _aliens.Count;
        if (alive == 0 || _alienShots.Count >= MaxShots)
            return;

        Spawner.CreateAlienShot(Commands, LowestInColumnOf(random.Next(alive)));
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Seconds between alien shots: 1.1 s in wave 1, 0.1 s less each wave, never below 0.35 s.</summary>
    public static float ShotInterval(int wave) => MathF.Max(0.35f, 1.1f - 0.1f * (wave - 1));

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// The position of the lowest alien in the same column as the <paramref name="index"/>th alien. Aliens of one
    /// column always share their x, because the formation moves them together.
    /// </summary>
    private Vector2 LowestInColumnOf(int index)
    {
        var column = 0f;
        foreach (var chunk in _aliens.GetChunks())
        {
            if (index < chunk.Count)
            {
                column = chunk.GetSpan<Position>()[index].Value.X;
                break;
            }

            index -= chunk.Count;
        }

        var lowest = new Vector2(column, float.MinValue);
        foreach (var chunk in _aliens.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Value;
                if (position.X == column && position.Y > lowest.Y)
                    lowest = position;
            }
        }

        return lowest;
    }
}
