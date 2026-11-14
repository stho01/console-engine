using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using EternalDigger.Saving;

namespace EternalDigger.Scenes;

/// <summary>
/// The first screen: loads the save once, then shows the <see cref="TitleMenu"/>. CONTINUE starts the saved mine; NEW
/// MINE starts a mine from <paramref name="newSeed"/> and writes its first save right away, so a confirmed start-over
/// can't come back. An unreadable save shows a message and only NEW MINE (the file was moved aside by
/// <see cref="SaveFile.Load"/>).
/// </summary>
public sealed class TitleScene(int newSeed, string savePath) : Scene<EternalDiggerGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string KeyHelp = "↑↓ choose   E select   ESC quit";
    public const string CorruptMessage = "The saved mine could not be read. Starting a new mine.";
    public const string UnsupportedMessage = "The saved mine is from another version of the game. Starting a new mine.";

    /// <summary>Title art, one string per row. Placeholder for the asset designer.</summary>
    private static readonly string[] Logo =
    [
        "▄▄▄ ▄▄▄ ▄▄▄ ▄▄▄ ▄  ▄ ▄▄▄ ▄       ▄▄  ▄ ▄▄▄ ▄▄▄ ▄▄▄ ▄▄▄",
        "█▄   █  █▄  █▄▀ █▀▄█ █▄█ █       █ █ █ █ ▄ █ ▄ █▄  █▄▀",
        "█▄▄  █  █▄▄ █ █ █  █ █ █ █▄▄     █▄▀ █ █▄█ █▄█ █▄▄ █ █"
    ];

    private const int LogoRow = 12;
    private const int MenuRow = 20;
    private const ConsoleColor LogoColor = ConsoleColor.Yellow;
    private const ConsoleColor ItemColor = ConsoleColor.White;
    private const ConsoleColor SelectedColor = ConsoleColor.Black;
    private const ConsoleColor SelectedBackground = ConsoleColor.Yellow;
    private const ConsoleColor HintColor = ConsoleColor.DarkGray;
    private const ConsoleColor MessageColor = ConsoleColor.Red;

    private TitleMenu _menu;
    private SaveData _save;
    private string _message;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        var status = SaveFile.Load(savePath, out _save);
        _menu = new TitleMenu(status == SaveStatus.Loaded);
        _message = status switch
        {
            SaveStatus.Corrupt => CorruptMessage,
            SaveStatus.Unsupported => UnsupportedMessage,
            _ => null
        };
    }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        if (input.GetKey(Key.UP).Pressed || input.GetKey(Key.W).Pressed)
            _menu.Up();
        if (input.GetKey(Key.DOWN).Pressed || input.GetKey(Key.S).Pressed)
            _menu.Down();
        if (!input.GetKey(Key.E).Pressed)
            return;

        switch (_menu.Select())
        {
            case TitleChoice.Continue:
                Game.Scenes.Set(new MineScene(_save, savePath));
                break;
            case TitleChoice.NewMine:
                StartNewMine();
                break;
        }
    }

    public override void OnRender()
    {
        var console = Game.Console;
        for (var i = 0; i < Logo.Length; i++)
            console.Draw(Centered(Logo[i]), LogoRow + i, Logo[i], LogoColor);

        var row = MenuRow;
        if (_menu.IsConfirming)
        {
            console.Draw(Centered(TitleMenu.ConfirmQuestion), row, TitleMenu.ConfirmQuestion, ItemColor);
            row += 2;
        }

        var items = _menu.Items;
        for (var i = 0; i < items.Length; i++)
        {
            var selected = i == _menu.Selected;
            var x = Centered(items[i]);
            console.Draw(x - 2, row + i * 2, selected ? "► " : "  ", LogoColor);
            console.Draw(x, row + i * 2, items[i], selected ? SelectedColor : ItemColor,
                selected ? SelectedBackground : ConsoleColor.Black);
        }

        if (_message != null)
            console.Draw(Centered(_message), row + items.Length * 2 + 1, _message, MessageColor);

        console.Draw(Centered(KeyHelp), Layout.ScreenHeight - 3, KeyHelp, HintColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void StartNewMine()
    {
        var start = SaveData.NewMine(newSeed);
        try
        {
            SaveFile.Save(savePath, start);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            // The mine still starts; the next sale, purchase or Esc tries again (and the HUD says if it fails).
        }

        Game.Scenes.Set(new MineScene(start, savePath));
    }

    private static int Centered(string text) => (Layout.ScreenWidth - text.Length) / 2;
}
