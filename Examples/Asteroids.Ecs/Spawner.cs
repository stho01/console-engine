using System;
using Asteroids.Ecs.Components;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using Microsoft.Xna.Framework;

namespace Asteroids.Ecs;

/// <summary>Creates the three kinds of entities. Each kind is one archetype (one fixed set of components).</summary>
public sealed class Spawner
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int AsteroidOutlineCount = 32;
    private const int AsteroidSides = 20;
    private const float ShipSize = 5f;
    private const float BulletSpeed = 80f;
    private const float BulletLifetime = 1.5f;
    private const float AsteroidMaxDrift = 6f;

    private readonly World _world;
    private readonly Random _random;
    private readonly int _shipOutline;
    private readonly int[] _asteroidOutlines = new int[AsteroidOutlineCount];
    private readonly float[] _asteroidRadii = new float[AsteroidOutlineCount];

    //**********************************************************
    //** ctor
    //**********************************************************

    public Spawner(World world, OutlineLibrary outlines, Random random)
    {
        _world = world;
        _random = random;

        _shipOutline = outlines.Add([
            new Vector2(0f, -1f) * ShipSize,
            new Vector2(.7f, .7f) * ShipSize,
            new Vector2(-.7f, .7f) * ShipSize
        ]);

        // A few dozen shared outlines look as varied as one per asteroid, and keep the vertex data small.
        for (var i = 0; i < AsteroidOutlineCount; i++)
        {
            _asteroidRadii[i] = _random.Next(8, 12);
            _asteroidOutlines[i] = outlines.Add(CreateAsteroidOutline(_asteroidRadii[i]));
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public Entity CreateShip(Vector2 position) =>
        _world.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            new Rotation(0f),
            new Outline(_shipOutline, '#', ConsoleColor.White),
            new Ship { Thrust = 60f, SteeringSpeed = MathF.Tau, Drag = 1.5f });

    public Entity CreateAsteroid(Vector2 position)
    {
        var outline = _random.Next(AsteroidOutlineCount);
        var drift = new Vector2(RandomSigned(), RandomSigned()) * AsteroidMaxDrift;

        return _world.Create(
            new Position(position),
            new Velocity(drift),
            new Outline(_asteroidOutlines[outline], '#', ConsoleColor.Green),
            new Collider(_asteroidRadii[outline]),
            new Asteroid());
    }

    /// <summary>
    /// Records a bullet fired from the ship's nose. Goes through a command buffer because it's called while the ship
    /// query is being iterated.
    /// </summary>
    public static void CreateBullet(CommandBuffer commands, Vector2 shipPosition, Vector2 heading,
        Vector2 shipVelocity) =>
        commands.Create(
            new Position(shipPosition + heading * ShipSize),
            new Velocity(shipVelocity + heading * BulletSpeed),
            new Glyph('*', ConsoleColor.Yellow),
            new Collider(.5f),
            new Lifetime(BulletLifetime),
            new Bullet());

    //**********************************************************
    //** internals
    //**********************************************************

    private Vector2[] CreateAsteroidOutline(float radius)
    {
        var outline = new Vector2[AsteroidSides];
        const float step = MathF.Tau / AsteroidSides;
        for (var i = 0; i < AsteroidSides; i++)
        {
            outline[i] = new Vector2(
                MathF.Sin(step * i) * radius + RandomSigned(),
                MathF.Cos(step * i) * radius + RandomSigned());
        }

        return outline;
    }

    private float RandomSigned() => (float)(_random.NextDouble() * 2.0 - 1.0);
}
