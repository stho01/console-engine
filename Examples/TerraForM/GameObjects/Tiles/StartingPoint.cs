using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class StartingPoint : MapTile
{
    private static readonly Sprite StartSprite = Sprite.FromStringArray(new []{
        "  ▒  ",
        " ▒▓▒ ",
        "▒▓S▓▒",
        " ▒▓▒ ",
        "  ▒  ",
    }, ConsoleColor.Gray);
                
    public StartingPoint(TerraformGame game) : base(game) { }

    public override Sprite GetSprite() => StartSprite;
}