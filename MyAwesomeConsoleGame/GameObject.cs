﻿using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public abstract class GameObject
    {
        public MyAwesomeGame Game { get; }
        public Vector2 Position { get; set; }
        public Rectangle BoundingBox => new((int)Position.X, (int)Position.Y, 3, 3);
        
        public GameObject(MyAwesomeGame game)
        {
            Game = game;
        }

        public Point GetScreenPos() {
            return Game.Camera.WorldToScreenPos(Position);
        }
        
        
    }
}