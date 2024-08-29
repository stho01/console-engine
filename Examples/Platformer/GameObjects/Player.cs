using System;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using Platformer.Utils;

namespace Platformer.GameObjects;

public class Player(PlatformerGame game) : GameObject
{
    private static readonly Sprite PlayerSprite = Sprite.FromStringArray(new[]{
        "▓O▓",
        "▓▓▓",
        "▓O▓"
    }, ConsoleColor.Red);

    private bool _jump;

    public bool IsAirborne { get; private set; } = true;
    public float MovementStrength => IsAirborne ? 75f : 150f;
        
    public void MoveLeft() => ApplyForce(new Vector2(-1, 0) * MovementStrength);
    public void MoveRight() => ApplyForce(new Vector2(1, 0) * MovementStrength);
    public void Jump() => _jump = true;

    public void ApplyForce(Vector2 force) => Acceleration += force * (float)GameTime.Delta.TotalSeconds;

    public void Update()
    {
        ApplyForce(new Vector2(0f, 200f));

        PerformJumpIfApplicable();
            
        // Drag
        Velocity += new Vector2(-3f, -.1f) * Velocity * (float) GameTime.Delta.TotalSeconds;
        Velocity += Acceleration * (float) GameTime.Delta.TotalSeconds;
            
        Position = CheckCollisions(Position + Velocity);
           
        Acceleration = Vector2.Zero;
        if (Velocity.LengthSquared() < 0.000001)
            Velocity = Vector2.Zero;


        if (game.IsDebugMode)
        {
            game.Console.Draw(0, 0, $"POS: {Position}");
            game.Console.Draw(0, 1, $"VEL: {Velocity}");
            game.Console.Draw(0, 2, $"AIR: {IsAirborne}");    
        }
    }
     
    public void Draw()
    {
        var (x, y) = game.Camera.WorldToScreenPos(Position);

        game.Console.Draw(
            (int)x,
            (int)y,
            PlayerSprite
        );
    }

    private void PerformJumpIfApplicable()
    {
        if (!_jump || IsAirborne) return;
            
        Velocity = new Vector2(Velocity.X, -.05f);

        IsAirborne = true;
        _jump = false;
    }
        
    private Vector2 CheckCollisions(Vector2 newPos)
    {
        var newX = newPos.X;
        var newY = newPos.Y;
            
        var playerBounds = new RectangleF(
            newPos.X, 
            newPos.Y, 
            PlayerSprite.Width, 
            PlayerSprite.Height
        );

        const float off = .05f;
        var yRbTile = game.World.GetTile(playerBounds.Right - off, playerBounds.Bottom);
        var yLbTile = game.World.GetTile(playerBounds.Left + off, playerBounds.Bottom);
        var yLtTile = game.World.GetTile(playerBounds.Left + off, playerBounds.Top);
        var yRtTile = game.World.GetTile(playerBounds.Right - off, playerBounds.Top);
        var xRbTile = game.World.GetTile(playerBounds.Right, playerBounds.Bottom - off);
        var xLtTile = game.World.GetTile(playerBounds.Left, playerBounds.Top + off);
        var xLbTile = game.World.GetTile(playerBounds.Left, playerBounds.Bottom - off);
        var xRtTile = game.World.GetTile(playerBounds.Right, playerBounds.Top + off);

        bool IsWall(char c) => c is not ('.' or ' ');
            
        if (Velocity.Y > 0 && (IsWall(yRbTile) || IsWall(yLbTile)))
        {
            newY = (int) newY;
            IsAirborne = false;
            Velocity = new Vector2(Velocity.X, 0f);
        }
        else if (Velocity.Y < 0 && (IsWall(yRtTile) || IsWall(yLtTile)))
        {
            newY = (int) (newY + .5f);
            Velocity = new Vector2(Velocity.X, 0f);
        }

        if (Velocity.X > 0 && (IsWall(xRbTile) || IsWall(xLbTile)))
        {
            newX = (int) newX;
            Velocity = new Vector2(0f, Velocity.Y);
        } 
        else if (Velocity.X < 0 && (IsWall(xRtTile) || IsWall(xLtTile)))
        {
            newX = (int) (newX + .5f);
            Velocity = new Vector2(0f, Velocity.Y);
        }

        if (game.IsDebugMode)
        {
            game.Console.Draw(0, 3, playerBounds.ToString());
            game.Console.Draw(0, 4, $"X - RB: {xRbTile}  LB: {xLbTile}  RT: {xRtTile}  LT: {xLtTile}");
            game.Console.Draw(0, 5, $"Y - RB: {yRbTile}  LB: {yLbTile}  RT: {yRtTile}  LT: {yLtTile}");
        }
            
        return new Vector2(newX, newY);
    }
}