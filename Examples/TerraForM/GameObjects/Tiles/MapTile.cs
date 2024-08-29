using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public abstract class MapTile(TerraformGame game) : GameObject(game)
{
    public abstract Sprite GetSprite();

    public void Draw()
    {
        var screenPos = GetScreenPos();
        Game.Console.Draw((int)screenPos.X, (int)screenPos.Y, GetSprite());
        // Game.Console.Draw((int)screenPos.X, (int)screenPos.Y, Position.X.ToString(), ConsoleColor.Black, ConsoleColor.White);
        // Game.Console.Draw((int)screenPos.X, (int)screenPos.Y+1, Position.Y.ToString(), ConsoleColor.Black, ConsoleColor.White);
    }
}