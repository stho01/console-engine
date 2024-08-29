using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class Craves(TerraformGame game) : MapTile(game)
{
    // ░ ▒ ▓ █
    private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
        " ░▓▓░", 
        "▓░░░▒", 
        "░░ ░░", 
        "▓░░░▓", 
        " ▒▓▒ " 
    }, ConsoleColor.DarkGray);

    public override Sprite GetSprite() => Sprite;
}