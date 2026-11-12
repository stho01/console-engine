using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Platformer.Ecs.Components;
using Platformer.Ecs.Systems;

namespace Platformer.Ecs.Scenes;

/// <summary>
/// The whole game: one player entity, one entity per wall tile of the level (<c>Assets/Maps/level.txt</c>: one string
/// per row, <c>'.'</c> is empty, anything else is a wall) to draw it, the level as
/// the physics' tile grid (to collide with it), and the systems from <c>Systems/</c>. The player moves in the fixed
/// step; input, camera and drawing run every frame. The order of <c>Add</c> calls is the order they run. The draw order
/// matches
/// <c>Examples/Platformer</c>: debug text, then the player, then the tiles (which cover both).
/// </summary>
public sealed class PlatformerScene : EcsScene<PlatformerEcsGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    // Physics tuning in cells and seconds. Run/air acceleration, gravity (in PlayerPhysicsSystem) and drag are the
    // original's numbers. JumpSpeed replaces the original per-frame jump of 0.05 cells, converted at 1000 FPS.
    private const float GroundAcceleration = 150f;
    private const float AirAcceleration = 75f;
    private const float JumpSpeed = 50f;
    private static readonly Vector2 Drag = new(3f, .1f);

    // The level, player and tile of Examples/Platformer, unchanged, as files under Assets/.
    private const string LevelPath = "Maps/level.txt";
    private const string PlayerSpritePath = "Sprites/player.sprite";
    private const string TileSpritePath = "Sprites/tile.sprite";

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var player = Assets.Load<Sprite>(PlayerSpritePath);
        var tile = Assets.Load<Sprite>(TileSpritePath);

        var sprites = new SpriteLibrary();
        var playerSprite = sprites.Add(player);
        var tileSprite = sprites.Add(tile);
        var map = new TileMap(Assets.Load<string[]>(LevelPath), tile.Width, tile.Height);

        CreatePlayer(new Vector2(10, Console.Height - 5), new Vector2(player.Width, player.Height), playerSprite);
        CreateTiles(map, tileSprite);

        // No bodies here: the physics only answers the player's moves against the level's tile grid.
        var physics = FixedSimulation.Add(new PhysicsSystem(cellSize: map.TileWidth) { TileGrid = map.ToTileGrid() });
        FixedSimulation.Add(new PlayerPhysicsSystem(physics));

        Simulation.Add(new DebugToggleSystem(Game));
        Simulation.Add(new PlayerControlSystem());
        Simulation.Add(new CameraFollowSystem(Camera, Console));

        Presentation.Add(new PlayerDebugOverlaySystem(Game, Console));
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.Without<Tile>()));
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.With<Tile>()));
        Presentation.Add(new TileDebugLabelSystem(Game, Console, Camera));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void CreatePlayer(Vector2 position, Vector2 size, int sprite) =>
        World.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            new SpriteRef(sprite),
            new PlayerBody
            {
                Size = size,
                GroundAcceleration = GroundAcceleration,
                AirAcceleration = AirAcceleration,
                JumpSpeed = JumpSpeed,
                Drag = Drag,
                IsAirborne = true
            },
            new CollisionProbe());

    private void CreateTiles(TileMap map, int sprite)
    {
        for (var row = 0; row < map.Height; row++)
        for (var column = 0; column < map.Width; column++)
        {
            if (!TileMap.IsWall(map.GetTileAt(column, row)))
                continue;

            World.Create(
                new Position(new Vector2(column * map.TileWidth, row * map.TileHeight)),
                new SpriteRef(sprite),
                new MapCell(column, row),
                new Tile());
        }
    }
}
