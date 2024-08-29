using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class FinishPoint : MapTile
{
    private static readonly Sprite FinishSprite = Sprite.FromStringArray(new []{
        "█ █ █",
        " █ █ ",
        "█ █ █",
        " █ █ ",
        "█ █ █",
    }, ConsoleColor.Green);
        
    public FinishPoint(TerraformGame game) : base(game) {}

    public override Sprite GetSprite() => FinishSprite;
}