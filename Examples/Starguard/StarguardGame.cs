using ConsoleEngine;
using Starguard.Scenes;

namespace Starguard;

/// <summary>
/// Rows of invading aliens march sideways and step down. The player's cannon holds the ground line behind four shields.
/// Built on the ECS only: every alien, shot and shield cell is an entity, all behaviour is in <c>Systems/</c>.
/// </summary>
public class StarguardGame() : GameBase(Playfield.Default.Width, Playfield.Default.Height, 10, 10)
{
    /// <summary>Best score since the game was started. Kept across restarts, not saved to disk.</summary>
    public int HighScore { get; set; }

    protected override void OnInitialize() => Scenes.Set(new BattleScene());

    protected override void OnUpdate() { }

    protected override void OnRender() { }
}
