using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class PlantSpot : MapTile
    {
        private static readonly Sprite PlantSpotSprite = Sprite.FromStringArray(new []{
            "P▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.Magenta);
        
        public PlantSpot(MyAwesomeGame game) : base(game) { }

        public override Sprite GetSprite() => PlantSpotSprite;
    }
}