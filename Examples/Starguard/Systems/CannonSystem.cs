using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Starguard.Assets;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// Moves the cannon along the ground and fires. Only one player shot can be in flight. While the cannon is down it
/// counts the respawn time instead, then puts the rebuilt cannon back in the middle.
/// </summary>
public sealed class CannonSystem(Match match, Controls controls, Spawner spawner, Playfield playfield) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _cannons;
    private Query _playerShots;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _cannons = World.Query(Query.With<Cannon, Position, SpriteRef>());
        _playerShots = World.Query(Query.With<PlayerShot>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (match.IsOver)
            return;

        if (match.CannonDown)
        {
            match.RespawnSecondsLeft -= deltaSeconds;
            if (match.CannonDown)
                return;

            Rebuild();
        }

        var fire = controls.Fire && _playerShots.Count == 0;
        var maxX = playfield.Width - SpriteSheet.UnitWidth;
        foreach (var chunk in _cannons.GetChunks())
        {
            var cannons = chunk.GetSpan<Cannon>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var position = ref positions[i].Value;
                position.X = Math.Clamp(position.X + controls.Move * cannons[i].Speed * deltaSeconds, 0, maxX);
                if (fire)
                    Spawner.CreatePlayerShot(Commands, position);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Rebuild()
    {
        match.RespawnSecondsLeft = 0f;
        foreach (var chunk in _cannons.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var sprites = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].Value = spawner.CannonStart;
                sprites[i].Id = spawner.Sprites.Cannon;
            }
        }
    }
}
