using System;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace TerraForM.GameObjects.Tiles;

public class PlantSpot(TerraformGame game) : MapTile(game)
{
    private static readonly Sprite PlantSpotSprite = Sprite.FromStringArray(new []{
        "    ░░    ",
        "   ░▒▒░   ",
        "  ░▒▓▓▒░  ",
        " ░▒▓▒▒▓▒░ ",
        "░▒▓▒PP▒▓▒░",
        "░▒▓▒PP▒▓▒░",
        " ░▒▓▒▒▓▒░ ",
        "  ░░▓▓▒░  ",
        "   ░▒▒░   ",
        "    ░░    ",
    }, ConsoleColor.Green);

    public override Rectangle BoundingBox => new Rectangle(Position.ToPoint(), PlantSpotSprite.Size);

    public override Sprite GetSprite() => PlantSpotSprite;

    public bool HasBeenPlanted { get; set; }
}