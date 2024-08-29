using System;
using System.Collections.Generic;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace Asteroids.GameObjects;

public class Player : GameObject
{
    private readonly AsteroidsGame _game;
    private readonly Vector2[] _vertices = {
        new( 0f, -1f),
        new( .7f,  .7f),
        new(-.7f,  .7f)
    };
        
    public Player(AsteroidsGame game)
    {
        _game = game;
    }
        
    public float Thrust { get; set; } = 150f;
    public float SteeringStrength { get; set; } = 360f; // deg/sec

    public void SteerLeft() => Angle -= SteeringStrength * (float)GameTime.Delta.TotalSeconds;
    public void SteerRight() => Angle += SteeringStrength * (float)GameTime.Delta.TotalSeconds;

    public void Move()
    {
        var heading = new Vector2(
            (float)Math.Cos(MathHelper.ToRadians(Angle - 90)),
            (float)Math.Sin(MathHelper.ToRadians(Angle - 90))
        );

        ApplyForce(heading * Thrust);
    }

    public void ApplyForce(Vector2 force) => Acceleration += force * (float)GameTime.Delta.TotalSeconds;

    public void Update()
    {
        Velocity += -3 * Velocity * (float)GameTime.Delta.TotalSeconds;
            
        Velocity += Acceleration * (float)GameTime.Delta.TotalSeconds;
        Position += Velocity;
        Acceleration = Vector2.Zero;
    }

    public void Draw()
    {
        var screenPos = _game.Camera.WorldToScreenPos(Position);

        var matrix = Matrix.Identity;
        matrix *= Matrix.CreateRotationZ(MathHelper.ToRadians(Angle));
        matrix *= Matrix.CreateScale(5f);
        matrix *= Matrix.CreateTranslation(new Vector3(screenPos, 0f));
            
        var transformed = new List<Vector2>();
        foreach (var vertex in _vertices)
            transformed.Add(Vector2.Transform(vertex, matrix));

        for (var index = 0; index < transformed.Count; index++)
        {
            var nextIndex = index + 1;
            var p1 = transformed[index % transformed.Count];
            var p2 = transformed[nextIndex % transformed.Count];
                
            _game.Console.DrawLine(p1.ToPoint(), p2.ToPoint(), '#');
        }
    }
}