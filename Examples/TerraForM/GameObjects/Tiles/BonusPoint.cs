using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class BonusPoint : MapTile
{
    private static readonly Sprite BonusSpotSprite = Sprite.FromStringArray(new []{
        "  ▒  ",
        " ▒▓▒ ",
        "▒▓B▓▒",
        " ▒▓▒ ",
        "  ▒  ",
    }, ConsoleColor.Blue);

    private static readonly Sprite BonusSpotConsumedSprite = Sprite.FromStringArray(new[]
    {
        "     ",
        " ▒ ▒ ",
        "▒▓▼▓▒",
        " ▒ ▒ ",
        "     ",
    }, ConsoleColor.Green);
        
    public BonusPoint(TerraformGame game) : base(game) { }

    public override Sprite GetSprite() => HasBeenConsumed ? BonusSpotConsumedSprite : BonusSpotSprite;

    public bool HasBeenConsumed;
}