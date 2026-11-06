using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace Platformer.Ecs.Assets;

/// <summary>
/// The sprites of <c>Examples/Platformer</c>, unchanged. Registered in a <c>SpriteLibrary</c> by <c>PlatformerScene</c>.
/// </summary>
public static class Sprites
{
    public static readonly Sprite Player = Sprite.FromStringArray([
        "▓O▓",
        "▓▓▓",
        "▓O▓"
    ], ConsoleColor.Red);

    public static readonly Sprite Tile = Sprite.FromStringArray([
        "▒▓▒▓",
        "▓▒▓▒",
        "▒▓▒▓",
        "▓▒▓▒"
    ], ConsoleColor.Gray);
}
