using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class BonusPoint : MapTile
    {
        private static readonly Sprite BonusSpotSprite = Sprite.FromStringArray(new []{
            "  ▒  ",
            " ▒▓▒ ",
            "▒▓B▓▒",
            " ▒▓▒ ",
            "  ▒  ",
        }, ConsoleColor.Blue);
        
        public BonusPoint(MyAwesomeGame game) : base(game) { }

        public override Sprite GetSprite() => BonusSpotSprite;
    }
}