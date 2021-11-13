using System;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class PlantSpot : MapTile
    {
        private static readonly Sprite PlantSpotSprite = Sprite.FromStringArray(new []{
            "    ░░    ",
            "   ░▒▒░   ",
            "  ░▒▓▓▒░  ",
            " ░▒▓▒▒▓▒░ ",
            "░▒▓▒PP▒▓▒░",
            "░▒▓▒PP▒▓▒░",
            " ░▒▓▒▒▓▒░ ",
            "  ░░▓▓▒░  ",
            "   ░▒▒░   ",
            "    ░░    ",
        }, ConsoleColor.Green);

        public override Rectangle BoundingBox => new Rectangle(Position.ToPoint(), PlantSpotSprite.Size);

        public PlantSpot(MyAwesomeGame game) : base(game) { }

        public override Sprite GetSprite() => PlantSpotSprite;

        public bool HasBeenPlanted { get; set; }
    }
}