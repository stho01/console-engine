using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles
{
    public class Rock : MapTile
    {
        // ░ ▒ ▓ █
        private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
            " █▓▓ ",
            "███▒▓",
            "▓█▓▒▓",
            "▓▓░▓░",
            " ▓▓░░", 
        }, ConsoleColor.Gray);

        public Rock(TerraformGame game) : base(game) {}

        public override Sprite GetSprite() => Sprite;
    }
}