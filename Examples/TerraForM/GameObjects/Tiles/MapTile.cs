using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles
{
    public abstract class MapTile : GameObject
    {
        public abstract Sprite GetSprite();
        
        protected MapTile(TerraformGame game) : base(game) { }
        
        public void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw((int)screenPos.X, (int)screenPos.Y, GetSprite());
            // Game.Console.Draw((int)screenPos.X, (int)screenPos.Y, Position.X.ToString(), ConsoleColor.Black, ConsoleColor.White);
            // Game.Console.Draw((int)screenPos.X, (int)screenPos.Y+1, Position.Y.ToString(), ConsoleColor.Black, ConsoleColor.White);
        }
    }
}