using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class Rock(TerraformGame game) : MapTile(game)
{
    // ░ ▒ ▓ █
    private static readonly Sprite Sprite = Sprite.FromStringArray(new []{
        " █▓▓ ",
        "███▒▓",
        "▓█▓▒▓",
        "▓▓░▓░",
        " ▓▓░░", 
    }, ConsoleColor.Gray);

    public override Sprite GetSprite() => Sprite;
}