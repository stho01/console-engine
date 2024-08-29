using ConsoleEngine;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects;

public class Camera(GameBase game)
{
    private GameObject _gameObject;

    public Vector2 Position { get; set; }
        
    public void Follow(GameObject gameObject) 
    {
        _gameObject = gameObject;
    }

    public Vector2 WorldToScreenPos(Vector2 worldPos) => (worldPos - Position);
    public Vector2 ScreenToWorldPos(Vector2 worldPos) => (worldPos + Position);
        
    public void Update()
    {
        Position = _gameObject.Position - game.Console.ScreenCenter;
    }
}