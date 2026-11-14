using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Kartster.Modes;
using Kartster.Ui;

namespace Kartster.Scenes.Menus;

/// <summary>
/// Race setup as a table: TRACK &lt; name &gt;, LAPS &lt; 3 &gt;, MODE &lt; RACE &gt; (or TIME TRIAL: alone, against the
/// ghost of the best lap) and START RACE. Up/Down choose a row, Left/Right change it, Enter on any row starts the
/// race, Esc: select kart.
/// </summary>
public sealed class SelectTrackScene : Scene<KartsterGame>, IUiLayer
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = "SELECT TRACK";
    public const string Footer = "LEFT/RIGHT CHANGE   ENTER START   ESC BACK";
    public const int DefaultLapIndex = 2;
    public static readonly string[] LapChoices = ["1", "2", "3", "4", "5"];

    /// <summary>The MODE row's values, by <see cref="RaceMode"/>.</summary>
    public static readonly string[] ModeChoices = ["RACE", "TIME TRIAL"];

    private MenuScreen _menu;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    private IKartsterMenus Menus => (IKartsterMenus)Game;

    public override void OnLoad() => _menu = CreateMenu(Menus.TrackNames);

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        switch (_menu.Update(MenuKeys.From(Input.Instance)))
        {
            case MenuAction.Confirm:
                StartRace(_menu, Menus);
                break;
            case MenuAction.Back:
                Game.Scenes.Set(new SelectKartScene());
                break;
        }
    }

    public override void OnRender() { }

    public void DrawUi(IPixelTarget target) => _menu.Draw(target);

    //**********************************************************
    //** public methods
    //**********************************************************

    public static MenuScreen CreateMenu(string[] trackNames) => new(Title, [
        new MenuItem("TRACK", trackNames.Length > 0 ? trackNames : ["NONE"]),
        new MenuItem("LAPS", LapChoices, DefaultLapIndex),
        new MenuItem("MODE", ModeChoices),
        new MenuItem("START RACE")
    ], Footer);

    /// <summary>Starts the race the menu shows; nothing without tracks.</summary>
    /// <returns>Whether a race started.</returns>
    public static bool StartRace(MenuScreen menu, IKartsterMenus menus)
    {
        if (menus.TrackNames.Length == 0)
            return false;

        menus.StartRace(TrackIndex(menu), Laps(menu), Mode(menu));
        return true;
    }

    public static int TrackIndex(MenuScreen menu) => menu.Items[0].ValueIndex;

    public static int Laps(MenuScreen menu) => menu.Items[1].ValueIndex + 1;

    public static RaceMode Mode(MenuScreen menu) => (RaceMode)menu.Items[2].ValueIndex;
}
