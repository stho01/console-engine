using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public abstract class MapTile : GameObject
    {
        public abstract Sprite GetSprite();
        
        protected MapTile(MyAwesomeGame game) : base(game) { }
        
        public void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw(screenPos.X, screenPos.Y, GetSprite());            
        }
    }
}