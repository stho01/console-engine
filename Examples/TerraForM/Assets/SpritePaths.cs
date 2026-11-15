namespace TerraForM.Assets;

/// <summary>
/// Asset paths of the screens in <c>Assets/Sprites/</c>, for <c>Assets.Load&lt;Sprite&gt;</c> in a scene (its own
/// scope, released with the scene) or <c>Game.Assets</c> (kept for the whole game). Load them in <c>OnLoad</c> or a
/// constructor, not per frame. The sprites are shared: <c>Clone()</c> one before drawing into it.
/// </summary>
public static class SpritePaths
{
    public const string GameOver = "Sprites/game-over.sprite";
    public const string TopMenu = "Sprites/top-menu.sprite";
    public const string InputName = "Sprites/input-name.sprite";

    /// <summary>The story screen. Its <c>###</c> on row 22, column 32 is where the player's name goes.</summary>
    public const string Story = "Sprites/story.sprite";
}
