using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class StartingPoint(TerraformGame game) : MapTile(game)
{
    private static readonly Sprite StartSprite = Sprite.FromStringArray(new []{
        "  ▒  ",
        " ▒▓▒ ",
        "▒▓S▓▒",
        " ▒▓▒ ",
        "  ▒  ",
    }, ConsoleColor.Gray);

    public override Sprite GetSprite() => StartSprite;
}