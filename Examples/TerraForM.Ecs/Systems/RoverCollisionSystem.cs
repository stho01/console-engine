using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using TerraForM.Ecs.Assets;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// Applies the effect of every tile the moved rover overlaps, in tile creation order: rocks (+10 damage) and craters
/// (power to 0) stop it where it was before moving, plant spots allow planting, a bonus point gives power and score
/// once, and the finish scores the level and loads the next map. Clears the frame's thrust at the end.
/// </summary>
public sealed class RoverCollisionSystem(TerraformEcsGame game) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int RockDamage = 10;
    private const int BonusPower = 1000;
    private const int BonusScore = 10000;
    private const int GeneratorScore = 10000;

    private Query _rovers;
    private Query _tiles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _rovers = World.Query(Query.With<Rover, Position, Velocity>().With<SpriteRef>());
        _tiles = World.Query(Query.With<Position, MapTile, SpriteRef>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _rovers.GetChunks())
        {
            var rovers = chunk.GetSpan<Rover>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var sprites = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var rover = ref rovers[i];
                if (HitsObstacle(ref rover, Bounds.OfRover(positions[i].Value, sprites[i].Id)))
                {
                    positions[i].Value = rover.PositionBeforeMove;
                    velocities[i].Value = Vector2.Zero;
                }

                rover.Acceleration = Vector2.Zero;
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Applies every overlapped tile. Returns whether one of them stops the rover.</summary>
    private bool HitsObstacle(ref Rover rover, Rectangle roverBounds)
    {
        var stop = false;
        foreach (var chunk in _tiles.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var tiles = chunk.GetSpan<MapTile>();
            var sprites = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var tile = ref tiles[i];
                if (!Bounds.OfTile(positions[i].Value, tile).Intersects(roverBounds))
                    continue;

                switch (tile.Kind)
                {
                    case TileKind.Rock:
                        stop = true;
                        rover.DamageTaken += RockDamage;
                        break;
                    case TileKind.Crater:
                        stop = true;
                        rover.RemainingPower = 0;
                        break;
                    case TileKind.PlantSpot:
                        rover.StandingOnPlantSpot = true;
                        break;
                    case TileKind.BonusPoint when !tile.HasBeenConsumed:
                        tile.HasBeenConsumed = true;
                        sprites[i].Id = (int)SpriteId.BonusPointConsumed;
                        rover.RemainingPower += BonusPower;
                        game.Score += BonusScore;
                        break;
                    case TileKind.FinishPoint:
                        // Once per overlapped finish tile and frame. Loads the next map, which replaces this scene.
                        game.Score += (int)rover.RemainingPower - rover.DamageTaken;
                        game.Score += rover.GeneratorsPlanted * GeneratorScore;
                        game.RotateMap();
                        break;
                }
            }
        }

        return stop;
    }
}
