using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles
{
    public class Craves : MapTile
    {
        // ░ ▒ ▓ █
        private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
            " ░▓▓░", 
            "▓░░░▒", 
            "░░ ░░", 
            "▓░░░▓", 
            " ▒▓▒ " 
        }, ConsoleColor.DarkGray);
        
        public Craves(TerraformGame game) : base(game) {}

        public override Sprite GetSprite() => Sprite;
    }
}