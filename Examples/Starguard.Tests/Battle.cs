using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using Microsoft.Xna.Framework;
using Starguard.Assets;
using Starguard.Components;

namespace Starguard.Tests;

/// <summary>
/// A world and one system group with the shared game state, but no game or console: what a test needs to run systems.
/// The create helpers make single entities with the same components the <see cref="Spawner"/> gives them.
/// </summary>
internal sealed class Battle : IDisposable
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public Battle()
    {
        Group = new SystemGroup(World);
        Spawner = new Spawner(Sprites, Playfield);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public World World { get; } = new();
    public SystemGroup Group { get; }
    public Playfield Playfield { get; } = Playfield.Default;
    public SpriteSheet Sprites { get; } = new(new SpriteLibrary());
    public Spawner Spawner { get; }
    public Match Match { get; } = new();
    public Formation Formation { get; } = new();
    public Controls Controls { get; } = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Update(float deltaSeconds) => Group.Update(deltaSeconds);

    public Entity CreateAlien(float x, float y, int points = 10) =>
        World.Create(
            new Position(new Vector2(x, y)),
            new SpriteRef(Sprites.Brute),
            new Hitbox(SpriteSheet.UnitWidth, SpriteSheet.UnitHeight),
            new Alien(Sprites.Brute, points));

    public Entity CreateCannon(float x) =>
        World.Create(
            new Position(new Vector2(x, Playfield.CannonTop)),
            new SpriteRef(Sprites.Cannon),
            new Hitbox(SpriteSheet.UnitWidth, SpriteSheet.UnitHeight),
            new Cannon(Spawner.CannonSpeed));

    public Entity CreatePlayerShot(float x, float y) =>
        World.Create(
            new Position(new Vector2(x, y)),
            new Velocity(new Vector2(0, -Spawner.PlayerShotSpeed)),
            new Glyph('│', ConsoleColor.White),
            new PlayerShot());

    public Entity CreateAlienShot(float x, float y) =>
        World.Create(
            new Position(new Vector2(x, y)),
            new Velocity(new Vector2(0, Spawner.AlienShotSpeed)),
            new Glyph('¦', ConsoleColor.Yellow),
            new AlienShot());

    public Entity CreateShieldCell(float x, float y, byte health = Spawner.ShieldHealth) =>
        World.Create(
            new Position(new Vector2(x, y)),
            new Glyph(Spawner.ShieldGlyph(health), ConsoleColor.Green),
            new ShieldCell(health));

    public int Count<T>() where T : unmanaged => World.Query(Query.With<T>()).Count;

    public void Dispose() => World.Dispose();
}
