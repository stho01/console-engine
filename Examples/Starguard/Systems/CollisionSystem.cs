using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Starguard.Components;

namespace Starguard.Systems;

/// <summary>
/// Everything that happens on contact. Runs after the engine's <c>MovementSystem</c>, and tests each shot along the
/// path it moved this frame (<see cref="Collide.ShotHits"/>).
/// <list type="bullet">
/// <item>A player shot chips the nearest shield cell, or destroys an alien or the saucer and scores its points.</item>
/// <item>An alien shot chips the nearest shield cell, or wrecks the cannon and costs a life.</item>
/// <item>Aliens that march into a shield erase the cells they cover.</item>
/// </list>
/// Each shot stops at its first hit. Destroyed entities stay in the world until the group plays its commands back,
/// so cells and aliens already hit this frame are skipped by their health or by a second, harmless destroy.
/// </summary>
public sealed class CollisionSystem(Match match, Spawner spawner) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private static readonly Hitbox Cell = new(1, 1);

    private Query _playerShots;
    private Query _alienShots;
    private Query _aliens;
    private Query _saucers;
    private Query _shields;
    private Query _cannons;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _playerShots = World.Query(Query.With<PlayerShot, Position, Velocity>());
        _alienShots = World.Query(Query.With<AlienShot, Position, Velocity>());
        _aliens = World.Query(Query.With<Alien, Position, Hitbox>());
        _saucers = World.Query(Query.With<Saucer, Position, Hitbox>());
        _shields = World.Query(Query.With<ShieldCell, Position, Glyph>());
        _cannons = World.Query(Query.With<Cannon, Position, Hitbox>().With<SpriteRef>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        ResolvePlayerShots(deltaSeconds);
        ResolveAlienShots(deltaSeconds);
        CrushShields();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void ResolvePlayerShots(float deltaSeconds)
    {
        foreach (var chunk in _playerShots.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var shot = positions[i].Value;
                var previousY = shot.Y - velocities[i].Value.Y * deltaSeconds;
                if (TryChipShield(shot, previousY) || TryHitAlien(shot, previousY) || TryHitSaucer(shot, previousY))
                    Commands.Destroy(chunk.Entities[i]);
            }
        }
    }

    private void ResolveAlienShots(float deltaSeconds)
    {
        foreach (var chunk in _alienShots.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var shot = positions[i].Value;
                var previousY = shot.Y - velocities[i].Value.Y * deltaSeconds;
                if (TryChipShield(shot, previousY) || TryHitCannon(shot, previousY))
                    Commands.Destroy(chunk.Entities[i]);
            }
        }
    }

    /// <summary>Damages the intact shield cell on the shot's path that is closest to where the shot came from.</summary>
    private bool TryChipShield(Vector2 shot, float previousY)
    {
        var nearest = Entity.Null;
        var nearestDistance = float.MaxValue;
        foreach (var chunk in _shields.GetChunks())
        {
            var cells = chunk.GetSpan<ShieldCell>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (cells[i].Health == 0 || !Collide.ShotHits(shot, previousY, positions[i].Value, Cell))
                    continue;

                var distance = MathF.Abs(positions[i].Value.Y - previousY);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = chunk.Entities[i];
                }
            }
        }

        if (nearest == Entity.Null)
            return false;

        // Get and component writes are not structural changes, so they are allowed while queries are iterated.
        ref var cell = ref World.Get<ShieldCell>(nearest);
        cell.Health--;
        World.Get<Glyph>(nearest).Character = Spawner.ShieldGlyph(cell.Health);
        if (cell.Health == 0)
            Commands.Destroy(nearest);
        return true;
    }

    private bool TryHitAlien(Vector2 shot, float previousY)
    {
        foreach (var chunk in _aliens.GetChunks())
        {
            var aliens = chunk.GetSpan<Alien>();
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!Collide.ShotHits(shot, previousY, positions[i].Value, hitboxes[i]))
                    continue;

                match.AddPoints(aliens[i].Points);
                spawner.CreateExplosion(Commands, positions[i].Value);
                Commands.Destroy(chunk.Entities[i]);
                return true;
            }
        }

        return false;
    }

    private bool TryHitSaucer(Vector2 shot, float previousY)
    {
        foreach (var chunk in _saucers.GetChunks())
        {
            var saucers = chunk.GetSpan<Saucer>();
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!Collide.ShotHits(shot, previousY, positions[i].Value, hitboxes[i]))
                    continue;

                match.AddPoints(saucers[i].Points);
                spawner.CreateExplosion(Commands, positions[i].Value);
                Commands.Destroy(chunk.Entities[i]);
                return true;
            }
        }

        return false;
    }

    /// <summary>A wrecked cannon can't be hit again until it respawns, and nothing is hit after game over.</summary>
    private bool TryHitCannon(Vector2 shot, float previousY)
    {
        if (match.CannonDown || match.IsOver)
            return false;

        foreach (var chunk in _cannons.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            var sprites = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!Collide.ShotHits(shot, previousY, positions[i].Value, hitboxes[i]))
                    continue;

                sprites[i].Id = spawner.Sprites.Wreck;
                match.HitCannon();
                return true;
            }
        }

        return false;
    }

    /// <summary>Erases shield cells under any alien. Skipped until the formation's bottom reaches the shields.</summary>
    private void CrushShields()
    {
        var formationBottom = float.MinValue;
        foreach (var chunk in _aliens.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            for (var i = 0; i < chunk.Count; i++)
                formationBottom = MathF.Max(formationBottom, positions[i].Value.Y + hitboxes[i].Height);
        }

        foreach (var chunk in _shields.GetChunks())
        {
            var cells = chunk.GetSpan<ShieldCell>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (cells[i].Health == 0 || positions[i].Value.Y >= formationBottom ||
                    !IsUnderAlien(positions[i].Value))
                    continue;

                cells[i].Health = 0;
                Commands.Destroy(chunk.Entities[i]);
            }
        }
    }

    private bool IsUnderAlien(Vector2 cell)
    {
        foreach (var chunk in _aliens.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var hitboxes = chunk.GetSpan<Hitbox>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (Collide.Overlaps(cell, Cell, positions[i].Value, hitboxes[i]))
                    return true;
            }
        }

        return false;
    }
}
