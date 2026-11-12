using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using TerraForM.Ecs.Assets;
using TerraForM.Ecs.Components;
using TerraForM.Ecs.Systems;

namespace TerraForM.Ecs.Scenes;

/// <summary>
/// One level: map tiles and the rover are created from the map file, plant emitters and grass by systems. The order
/// of <c>Add</c> calls is the order the systems run, and it follows the original <c>GameScene.OnUpdate</c>/
/// <c>OnRender</c>.
/// </summary>
public sealed class GameScene(string mapName) : EcsScene<TerraformEcsGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Folder of the map files, under <c>Assets/</c> next to the exe.</summary>
    private const string MapFolder = "Maps";
    private readonly string _mapName = mapName ?? throw new ArgumentNullException(nameof(mapName));

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var map = Assets.Load<Map>($"{MapFolder}/{_mapName}.txt");
        var level = new Level(_mapName, map.Name);

        CreateTiles(map.Tiles);
        level.Rover = CreateRover(map);

        // The first frame is drawn before the first update, so the camera must already follow the rover.
        Camera.Position = CameraFollowSystem.Target(World.Get<Position>(level.Rover).Value, Console);

        Simulation.Add(new LevelInputSystem(Game, level));
        Simulation.Add(new CommandSystem(Game, level));
        Simulation.Add(new RoverPhysicsSystem());
        Simulation.Add(new MovementSystem());
        Simulation.Add(new RoverCollisionSystem(Game));
        Simulation.Add(new PlantEmitterSystem());
        Simulation.Add(new RestartSystem(Game, level));
        Simulation.Add(new CameraFollowSystem(Camera, Console, level));

        var sprites = SpriteSheet.CreateLibrary();
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.With<MapTile>()));
        Presentation.Add(new GrassRenderSystem(Console, Camera));
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.With<Rover>()));
        Presentation.Add(new HudSystem(Game, level));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Grid tiles column by column, then a border of rocks one tile outside the grid.</summary>
    private void CreateTiles(TileKind[,] tiles)
    {
        var width = tiles.GetLength(0);
        var height = tiles.GetLength(1);

        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            if (tiles[x, y] != TileKind.None)
                CreateTile(tiles[x, y], x, y);
        }

        // Same order as the original, which also places the four corners twice.
        for (var x = -1; x < width + 1; x++)
        {
            CreateTile(TileKind.Rock, x, -1);
            CreateTile(TileKind.Rock, x, height);
        }

        for (var y = -1; y < height + 1; y++)
        {
            CreateTile(TileKind.Rock, -1, y);
            CreateTile(TileKind.Rock, width, y);
        }
    }

    private void CreateTile(TileKind kind, int x, int y)
    {
        var sprite = ToSpriteId(kind);
        World.Create(
            new Position(new Vector2(x, y) * MapLoader.TileSize),
            new MapTile(kind, SpriteSheet.Get(sprite).Width),
            new SpriteRef((int)sprite));
    }

    private Entity CreateRover(Map map) =>
        World.Create(
            new Position(map.StartPosition.Value),
            new Velocity(Vector2.Zero),
            new Rover(map.MaxPower, map.Sequences),
            // Drawn one cell up and left of its collision box, like the original.
            new SpriteRef((int)SpriteId.RoverNorth, new Point(-1, -1)));

    private static SpriteId ToSpriteId(TileKind kind) => kind switch
    {
        TileKind.StartingPoint => SpriteId.StartingPoint,
        TileKind.FinishPoint => SpriteId.FinishPoint,
        TileKind.Rock => SpriteId.Rock,
        TileKind.Crater => SpriteId.Crater,
        TileKind.PlantSpot => SpriteId.PlantSpot,
        TileKind.BonusPoint => SpriteId.BonusPoint,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
