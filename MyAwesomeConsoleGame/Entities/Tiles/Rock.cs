using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class Rock : MapTile
    {
        private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
            "▒▓▒▓▒",
            "▓▒▓▒▓",
            "▒▓▒▓▒",
            "▓▒▓▒▓",
            "▒▓▒▓▒",
        }, ConsoleColor.DarkGray);

        public Rock(MyAwesomeGame game) : base(game) {}

        public override Sprite GetSprite() => Sprite;
    }
}