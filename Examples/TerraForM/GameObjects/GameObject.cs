using Microsoft.Xna.Framework;

namespace TerraForM.GameObjects;

public abstract class GameObject(TerraformGame game)
{
    //**********************************************************
    //** ctor:
    //**********************************************************

    //**********************************************************
    //** props:
    //**********************************************************

    public TerraformGame Game { get; } = game;
    public Vector2 Position { get; set; }
    public virtual Rectangle BoundingBox => new((int)Position.X, (int)Position.Y, World.TileSize, World.TileSize);
              
    //**********************************************************
    //** public:
    //**********************************************************

    public Vector2 GetScreenPos() 
    {
        return Game.Camera.WorldToScreenPos(Position);
    }
}