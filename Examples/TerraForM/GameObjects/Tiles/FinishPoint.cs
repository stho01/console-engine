using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class FinishPoint(TerraformGame game) : MapTile(game)
{
    private static readonly Sprite FinishSprite = Sprite.FromStringArray(new []{
        "█ █ █",
        " █ █ ",
        "█ █ █",
        " █ █ ",
        "█ █ █",
    }, ConsoleColor.Green);

    public override Sprite GetSprite() => FinishSprite;
}