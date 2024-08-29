using ConsoleEngine;
using Microsoft.Xna.Framework;
using TerraForM.GameObjects;

namespace TerraForM;

public class Camera(GameBase game)
{
    private GameObject _gameObject;

    public Vector2 Position { get; set; }
        
    public void Follow(GameObject gameObject) 
    {
        _gameObject = gameObject;
    }

    public Vector2 WorldToScreenPos(Vector2 worldPos) => worldPos - Position;
    public Vector2 ScreenToWorldPos(Point worldPos) => (worldPos + Position.ToPoint()).ToVector2();
        
    public void Update()
    {
        if (_gameObject == null)
            return;
            
        Position = (_gameObject.Position - new Vector2(game.Console.ScreenCenter.X, 35/2f));
    }
}