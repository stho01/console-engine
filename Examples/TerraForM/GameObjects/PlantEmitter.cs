using System;
using System.Collections.Generic;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;
using TerraForM.Scenes;

namespace TerraForM.GameObjects;

public class PlantEmitter(GameScene scene) : GameObject(scene.Game)
{
    //**********************************************************
    //** fields:
    //**********************************************************

    public const float LifeTime = 3000;
    private float _ellapsed = 0f;
    private const float EmitFreq = 150;
    private float _emitTimer = 0f;
    private readonly List<Vector2> _pixels = new();
    private int _iteration = 0;
    private readonly GameScene _scene = scene;
    private static readonly Random Random = new();
    private static readonly Pixel Grass = new()
    {
        Char = '√',
        ForegroundColor = ConsoleColor.DarkGreen
    };
              
    //**********************************************************
    //** ctor:
    //**********************************************************

    //**********************************************************
    //** public:
    //**********************************************************
        
    public void Update()
    {
        if (_ellapsed < LifeTime)
        {
            var dt = (float)GameTime.Delta.TotalMilliseconds;
            _ellapsed += dt;
            _emitTimer += dt;

            if (_emitTimer > EmitFreq)
            {
                SpawnPixels();
                _emitTimer = 0f;
            }
        }
    }

    public void Draw()
    {
        foreach (var pos in _pixels)
        {
            var screenPos = Game.Camera.WorldToScreenPos(pos).ToPoint();

            if (Game.Console.GetCharAt(screenPos.X, screenPos.Y) == ' ')
                Game.Console.Draw(screenPos.X, screenPos.Y, Grass);
        }
    }
              
    //**********************************************************
    //** private
    //**********************************************************

    private void SpawnPixels()
    {
        _iteration++;

            
        for (var i = 0; i < 30; i++)
        {
            var angle = (MathF.Tau / 30) * (float)(Random.NextDouble() - .25);
            var x = MathF.Sin(angle * i);
            var y = MathF.Cos(angle * i);
                
            var pos = new Vector2(x, y) * (_iteration + 1);

            pos += Position;
                
            _pixels.Add(pos);
        }
    }
}