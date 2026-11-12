using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;

namespace Vikingferd.Scenes;

/// <summary>
/// The first screen, over the night backdrop: the logo, the three heroes drawn large, and the level list. ↑↓ choose a
/// level, ENTER / E / SPACE start it, ESC quits. Every level can be played from here; nothing is saved.
/// </summary>
public sealed class TitleScene(int selectedLevel) : Scene<VikingferdGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string KeyHelp = "↑↓ choose level   ENTER start   ESC quit";
    public const string Tagline = "Three vikings, one way home. Nobody gets left behind.";

    private static readonly string[] Logo =
    [
        "█  █ ███ █  █ ███ █   █ ████ ████ ████ ████ ███ ",
        "█  █  █  █ █   █  ██  █ █    █    █    █  █ █  █",
        "█  █  █  ██    █  █ █ █ █ ██ ███  ███  ████ █  █",
        " ██   █  █ █   █  █  ██ █  █ █    █    █ █  █  █",
        " ██  ███ █  █ ███ █   █ ████ █    ████ █  █ ███ "
    ];

    private const int LogoRow = 3;
    private const int TaglineRow = 9;
    private const int HeroRow = 11;
    private const int HeroScale = 3;
    private const int MenuRow = 28;
    private const ConsoleColor LogoColor = ConsoleColor.Yellow;
    private const ConsoleColor LogoShadow = ConsoleColor.DarkYellow;
    private const ConsoleColor ItemColor = ConsoleColor.White;
    private const ConsoleColor HintColor = ConsoleColor.Gray;

    private const int PanelWidth = 44;
    private static readonly string PanelTop = "╔" + new string('═', PanelWidth - 2) + "╗";
    private static readonly string PanelMiddle = "║" + new string(' ', PanelWidth - 2) + "║";
    private static readonly string PanelBottom = "╚" + new string('═', PanelWidth - 2) + "╝";

    private static readonly string[] Items = BuildItems();
    private int _selected = Math.Clamp(selectedLevel, 0, Levels.All.Length - 1);
    private Canvas _canvas;
    private Backdrop _backdrop;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        _canvas = new Canvas(Game.Console.Width, Game.Console.Height);
        _backdrop = new Backdrop(_canvas.Width, _canvas.PixelHeight);
    }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        if (input.GetKey(Key.ESCAPE).Pressed)
        {
            Game.Stop();
            return;
        }

        if (input.GetKey(Key.UP).Pressed || input.GetKey(Key.W).Pressed)
            _selected = (_selected + Items.Length - 1) % Items.Length;
        if (input.GetKey(Key.DOWN).Pressed || input.GetKey(Key.S).Pressed)
            _selected = (_selected + 1) % Items.Length;

        if (input.GetKey(Key.ENTER).Pressed || input.GetKey(Key.E).Pressed || input.GetKey(Key.SPACE).Pressed)
            Game.Scenes.Set(new LevelScene(_selected));
    }

    public override void OnRender()
    {
        _backdrop.Update((float)GameTime.Delta.TotalSeconds);
        Draw(_canvas, _backdrop, _selected);
        _canvas.Flush(Game.Console);
    }

    /// <summary>The whole title screen into a canvas (also used by the snapshot tests).</summary>
    public static void Draw(Canvas canvas, Backdrop backdrop, int selected)
    {
        backdrop.Draw(canvas);
        for (var i = 0; i < Logo.Length; i++)
        {
            canvas.Text(Centered(Logo[i]) + 1, LogoRow + i + 1, Logo[i], LogoShadow);
            canvas.Text(Centered(Logo[i]), LogoRow + i, Logo[i], LogoColor);
        }

        canvas.Text(Centered(Tagline), TaglineRow, Tagline, HintColor);
        DrawHeroes(canvas);

        var panelX = Centered(PanelMiddle);
        canvas.Text(panelX, MenuRow - 2, PanelTop, LogoShadow, ConsoleColor.Black);
        for (var row = MenuRow - 1; row < MenuRow + Items.Length * 2; row++)
            canvas.Text(panelX, row, PanelMiddle, LogoShadow, ConsoleColor.Black);
        canvas.Text(panelX, MenuRow + Items.Length * 2, PanelBottom, LogoShadow, ConsoleColor.Black);

        for (var i = 0; i < Items.Length; i++)
        {
            var isSelected = i == selected;
            var x = Centered(Items[i]);
            var y = MenuRow + i * 2;
            if (isSelected)
                canvas.Text(x - 2, y, "►", LogoColor);
            canvas.Text(x, y, Items[i], isSelected ? ConsoleColor.Black : ItemColor,
                isSelected ? LogoColor : ConsoleColor.Black);
        }

        canvas.Text(Centered(KeyHelp), Layout.KeyHelpRow, KeyHelp, HintColor, ConsoleColor.Black);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void DrawHeroes(Canvas canvas)
    {
        for (var i = 0; i < Heroes.Order.Length; i++)
        {
            var kind = Heroes.Order[i];
            var stats = Heroes.Of(kind);
            var center = Layout.HeroCardWidth * i + Layout.HeroCardWidth / 2;
            var sprite = Looks.HeroSprite(new Actor { Kind = kind, Facing = 1, OnGround = true });
            var width = sprite.Width * HeroScale;
            sprite.Draw(canvas, center - width / 2, HeroRow * 2, scale: HeroScale);

            var textRow = HeroRow + sprite.Height * HeroScale / 2 + 1;
            canvas.Text(center - stats.Name.Length / 2, textRow, stats.Name, Looks.HeroColor(kind),
                ConsoleColor.Black);
            canvas.Text(center - stats.Ability.Length / 2, textRow + 1, stats.Ability, HintColor,
                ConsoleColor.Black);
        }
    }

    private static string[] BuildItems()
    {
        var items = new string[Levels.All.Length];
        for (var i = 0; i < items.Length; i++)
            items[i] = $" {GameTexts.LevelHeader(i)} ";
        return items;
    }

    private static int Centered(string text) => (Layout.ScreenWidth - text.Length) / 2;
}
