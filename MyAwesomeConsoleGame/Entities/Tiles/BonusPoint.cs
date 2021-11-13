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

        private static readonly Sprite BonusSpotConsumedSprite = Sprite.FromStringArray(new[]
        {
            "     ",
            " ▒ ▒ ",
            "▒▓▼▓▒",
            " ▒ ▒ ",
            "     ",
        }, ConsoleColor.Green);
        
        public BonusPoint(MyAwesomeGame game) : base(game) { }

        public override Sprite GetSprite() => HasBeenConsumed ? BonusSpotConsumedSprite : BonusSpotSprite;

        public bool HasBeenConsumed;
    }
}