using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class Craves : MapTile
    {
        private static readonly Sprite CravesSprite = Sprite.FromStringArray(new []{
            "C▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.DarkYellow);

        
        public Craves(MyAwesomeGame game) : base(game) {}

        public override Sprite GetSprite() => CravesSprite;
    }
}