using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.GameObjects.Tiles;

public class BonusPoint(TerraformGame game) : MapTile(game)
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

    public override Sprite GetSprite() => HasBeenConsumed ? BonusSpotConsumedSprite : BonusSpotSprite;

    public bool HasBeenConsumed;
}