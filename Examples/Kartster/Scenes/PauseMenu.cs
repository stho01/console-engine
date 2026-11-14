using ConsoleEngine.Ecs;
using Kartster.Scenes.Menus;
using Kartster.Ui;

namespace Kartster.Scenes;

/// <summary>What the player picked in the <see cref="PauseMenu"/>.</summary>
public enum PauseChoice
{
    None,
    Resume,
    Restart,
    QuitToTitle
}

/// <summary>
/// The race's pause menu, after Kartster's (<c>PauseMenu</c>, <c>PauseOverlayRenderer</c>): a dimmed, frozen scene
/// with "PAUSED" and RESUME, RESTART, QUIT TO TITLE on a <see cref="MenuScreen"/>. Esc resumes, as in Kartster. While
/// open, <see cref="Timestep"/> hands the scene a timestep that never steps, so the race's fixed step and clock stop,
/// and the race's own one keeps the time it had, so resuming neither jumps nor catches up a backlog.
/// </summary>
public sealed class PauseMenu
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = "PAUSED";
    public const string Resume = "RESUME";
    public const string Restart = "RESTART";
    public const string QuitToTitle = "QUIT TO TITLE";
    public const string Footer = "ENTER SELECT   ESC RESUME";

    // shortcut: a step this long never comes due, so the engine's EcsScene (which has no pause) runs no fixed step;
    // give EcsScene a pause flag if another game needs one.
    private readonly FixedTimestep _frozen = new(float.MaxValue, 1);

    private readonly MenuScreen _menu =
        new(Title, [new MenuItem(Resume), new MenuItem(Restart), new MenuItem(QuitToTitle)], Footer)
        {
            DrawBackground = false
        };

    //**********************************************************
    //** props
    //**********************************************************

    public bool IsOpen { get; private set; }
    public MenuScreen Menu => _menu;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Opens on RESUME.</summary>
    public void Open()
    {
        IsOpen = true;
        _menu.Selected = 0;
    }

    /// <summary>
    /// Esc resumes, Enter picks the selected item, the arrows move. Closes on RESUME; RESTART and QUIT TO TITLE leave
    /// it open, as the scene is about to go.
    /// </summary>
    public PauseChoice Update(MenuKeys keys)
    {
        if (!IsOpen)
            return PauseChoice.None;

        var choice = _menu.Update(keys) switch
        {
            MenuAction.Back => PauseChoice.Resume,
            MenuAction.Confirm => (PauseChoice)(_menu.Selected + 1),
            _ => PauseChoice.None
        };
        if (choice == PauseChoice.Resume)
            IsOpen = false;
        return choice;
    }

    /// <summary>The timestep the scene should run: <paramref name="race"/>'s, or one that never steps while open.</summary>
    public FixedTimestep Timestep(FixedTimestep race) => IsOpen ? _frozen : race;

    /// <summary>Every other pixel dark (the console has no alpha to dim with), then the menu.</summary>
    public void Draw(IPixelTarget target)
    {
        if (!IsOpen)
            return;

        for (var y = 0; y < target.Height; y++)
        for (var x = y & 1; x < target.Width; x += 2)
            target.Set(x, y, UiColors.TextShadow);
        _menu.Draw(target);
    }
}
