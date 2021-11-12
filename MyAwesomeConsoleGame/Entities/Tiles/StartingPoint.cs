using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class StartingPoint : MapTile
    {
        private static readonly Sprite StartSprite = Sprite.FromStringArray(new []{
            "▒▓▒▓▒",
            "▓▒▓▒▓",
            "▒▓▒▓▒",
            "▓▒▓▒▓",
            "▒▓▒▓▒",
        }, ConsoleColor.Green);
        
        public StartingPoint(MyAwesomeGame game) : base(game) { }

        public override Sprite GetSprite() => StartSprite;
    }
}