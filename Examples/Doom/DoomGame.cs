using System;
using System.IO;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Doom.Scenes;
using Doom.Wad;

namespace Doom;

/// <summary>
/// Plays the levels of a Doom WAD (the shareware <c>DOOM1.WAD</c>) in the console. The WAD is not part of the
/// repository: <see cref="WadLocator"/> finds it at start-up, and every level, texture and sprite comes from it.
/// The rendering and game code are this example's own. Starts on the <see cref="TitleScene"/>, or the
/// <see cref="MissingWadScene"/> when no WAD is found or it can't be read. Asks for 24-bit colour; C toggles it.
/// </summary>
public class DoomGame(Layout layout)
    : GameBase(layout.ScreenWidth, layout.ScreenHeight, layout.FontWidth, layout.FontHeight)
{
    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The screen size picked at start-up.</summary>
    public Layout Layout { get; } = layout;

    /// <summary>The loaded WAD, or null when none was found.</summary>
    public GameAssets Wad { get; private set; }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnInitialize()
    {
        // Exact WAD colours where the console can show 24-bit colour; else the 16-colour dither. C switches.
        Console.TrueColor = true;
        var path = WadLocator.Find();
        if (path == null)
        {
            Scenes.Set(new MissingWadScene(null));
            return;
        }

        try
        {
            Wad = GameAssets.Load(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException
                                              or UnauthorizedAccessException or ArgumentOutOfRangeException
                                              or IndexOutOfRangeException)
        {
            Scenes.Set(new MissingWadScene(exception.Message));
            return;
        }

        Scenes.Set(new TitleScene());
    }

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.C).Pressed)
            Console.TrueColor = !Console.TrueColor;
    }

    protected override void OnRender() { }
}
