using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame.Entities.Tiles
{
    public class Craves : MapTile
    {
        
         // 
        // private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
        //     " ▓▓░░", 
        //     "▓▓░▓░", 
        //     "▓█▓▒▓", 
        //     "███▒▓", 
        //     " █▓▓ " 
        // }, ConsoleColor.DarkGray);
        
        
        // ░ ▒ ▓ █
        private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
            " ░▓▓░", 
            "▓░░░▒", 
            "░░ ░░", 
            "▓░░░▓", 
            " ▒▓▒ " 
        }, ConsoleColor.DarkGray);
        
        public Craves(MyAwesomeGame game) : base(game) {}

        public override Sprite GetSprite() => Sprite;
    }
}