using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;
using TerraForM.Ecs.Assets;
using TerraForM.Ecs.Commands;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// Runs the current command of the level's <see cref="CommandQueue"/> on the rover, once per frame until it is done.
/// A move thrusts the rover and turns it. A plant plants every untouched plant spot under the rover.
/// </summary>
public sealed class CommandSystem(TerraformEcsGame game, Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>
    /// The original thrusted and paid power once per frame, so it moved further and used more power on faster PCs.
    /// Here both are per second, scaled so the rover behaves like the original did at this frame rate.
    /// </summary>
    private const float ReferenceFrameRate = 300f;

    /// <summary>Acceleration while a move runs, cells/s² (the original's 0.8 per frame).</summary>
    private const float Thrust = 0.8f * ReferenceFrameRate;

    /// <summary>Power used per second of thrust (the original's 0.8² = 0.64 per frame): ≈ 19.2 per 100 ms move.</summary>
    private const float PowerPerThrustSecond = 0.64f * ReferenceFrameRate;

    private const int GeneratorScore = 10000;

    private Query _tiles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _tiles = World.Query(Query.With<Position, MapTile>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var queue = level.CommandQueue;
        if (!queue.TryGetCurrent(out var command))
            return;

        var planted = 0;
        if (command.Kind == CommandKind.Move)
            Move(command.Direction, deltaSeconds);
        else
            planted = Plant();

        queue.Advance(GameTime.Delta.TotalMilliseconds);

        // After the tile loop: creating an entity while a query is iterated would throw. Created directly rather than
        // through Commands so the emitter runs this frame, as in the original.
        var position = World.Get<Position>(level.Rover).Value;
        for (var i = 0; i < planted; i++)
            World.Create(new Position(position), new PlantEmitter());
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Move(Direction direction, float deltaSeconds)
    {
        ref var rover = ref World.Get<Rover>(level.Rover);
        ref var position = ref World.Get<Position>(level.Rover).Value;
        ref var sprite = ref World.Get<SpriteRef>(level.Rover);

        // Paid even without power, so power keeps falling below 0. Thrust only works while some is left.
        rover.RemainingPower -= PowerPerThrustSecond * deltaSeconds;
        if (!rover.PowerDepleted)
            rover.Acceleration += ToUnitVector(direction) * Thrust;

        sprite.Id = (int)ToRoverSprite(direction);

        // Original quirk, kept as is (one cell per frame): moving north while touching a rock or crater nudges the
        // rover one cell further north.
        if (direction == Direction.North && TouchesObstacle(Bounds.OfRover(position, sprite.Id)))
            position.Y -= 1;
    }

    /// <summary>Returns how many plant spots were planted.</summary>
    private int Plant()
    {
        ref var rover = ref World.Get<Rover>(level.Rover);
        if (!rover.StandingOnPlantSpot)
            return 0;

        var roverBounds = Bounds.OfRover(World.Get<Position>(level.Rover).Value, World.Get<SpriteRef>(level.Rover).Id);
        var planted = 0;
        foreach (var chunk in _tiles.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var tiles = chunk.GetSpan<MapTile>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var tile = ref tiles[i];
                if (tile.Kind != TileKind.PlantSpot || tile.HasBeenPlanted ||
                    !Bounds.OfTile(positions[i].Value, tile).Intersects(roverBounds))
                    continue;

                tile.HasBeenPlanted = true;
                rover.GeneratorsPlanted++;
                game.Score += GeneratorScore;
                planted++;
            }
        }

        return planted;
    }

    private bool TouchesObstacle(Rectangle roverBounds)
    {
        foreach (var chunk in _tiles.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var tiles = chunk.GetSpan<MapTile>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (tiles[i].Kind is TileKind.Rock or TileKind.Crater &&
                    Bounds.OfTile(positions[i].Value, tiles[i]).Intersects(roverBounds))
                    return true;
            }
        }

        return false;
    }

    private static Vector2 ToUnitVector(Direction direction) => direction switch
    {
        Direction.North => new Vector2(0, -1f),
        Direction.South => new Vector2(0, 1f),
        Direction.East => new Vector2(1f, 0),
        _ => new Vector2(-1f, 0)
    };

    private static SpriteId ToRoverSprite(Direction direction) => direction switch
    {
        Direction.North => SpriteId.RoverNorth,
        Direction.South => SpriteId.RoverSouth,
        Direction.East => SpriteId.RoverEast,
        _ => SpriteId.RoverWest
    };
}
