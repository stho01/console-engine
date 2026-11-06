using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using Platformer.Ecs.Assets;
using Platformer.Ecs.Components;
using Platformer.Ecs.Systems;

namespace Platformer.Ecs.Scenes;

/// <summary>
/// The whole game: one player entity, one entity per wall tile of <see cref="Maps.Level"/>, and the systems from
/// <c>Systems/</c>. The order of <c>Add</c> calls is the order they run. The draw order matches
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

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var sprites = new SpriteLibrary();
        var playerSprite = sprites.Add(Sprites.Player);
        var tileSprite = sprites.Add(Sprites.Tile);
        var map = new TileMap(Maps.Level, Sprites.Tile.Width, Sprites.Tile.Height);

        CreatePlayer(new Vector2(10, Console.Height - 5), playerSprite);
        CreateTiles(map, tileSprite);

        Simulation.Add(new DebugToggleSystem(Game));
        Simulation.Add(new PlayerControlSystem());
        Simulation.Add(new PlayerPhysicsSystem(map));
        Simulation.Add(new CameraFollowSystem(Camera, Console));

        Presentation.Add(new PlayerDebugOverlaySystem(Game, Console));
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.Without<Tile>()));
        Presentation.Add(new SpriteRenderSystem(Console, Camera, sprites, Query.With<Tile>()));
        Presentation.Add(new TileDebugLabelSystem(Game, Console, Camera));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void CreatePlayer(Vector2 position, int sprite) =>
        World.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            new SpriteRef(sprite),
            new PlayerBody
            {
                Size = new Vector2(Sprites.Player.Width, Sprites.Player.Height),
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
