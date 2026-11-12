using System;
using System.Collections.Generic;
using System.IO;
using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger.Saving;

/// <summary>
/// Saves the running mine to its slot. Gameplay asks for a save with <see cref="Request"/> (a sale or a purchase);
/// <c>SaveSystem</c> writes it at the end of the frame's simulation, and on its autosave timer. Esc saves directly
/// through <c>MineScene.Save</c>. Writing happens only on those triggers, never per frame. A new mine's first save is
/// written by <c>TitleScene</c>.
/// </summary>
public sealed class GameSaver(string path, int seed, Mine mine, Elevator elevator)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Seconds underground between autosaves. 0 turns the timer off; sales, purchases and Esc still save.
    /// </summary>
    public const float AutosaveInterval = 60f;

    private readonly List<TileChange> _changes = [];

    //**********************************************************
    //** props
    //**********************************************************

    public string Path { get; } = path;
    public int Seed { get; } = seed;

    /// <summary>A sale or purchase asked for a save this frame.</summary>
    public bool IsRequested { get; private set; }

    /// <summary>Saves written or tried so far. The HUD shows its hint when this changes.</summary>
    public int Saves { get; private set; }

    /// <summary>The last save couldn't be written (disk full, no permission…). The game goes on.</summary>
    public bool LastSaveFailed { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Request() => IsRequested = true;

    /// <summary>Writes the miner and the mine's changed tiles. False if the file couldn't be written.</summary>
    public bool Save(in Miner miner)
    {
        IsRequested = false;
        try
        {
            SaveFile.Save(Path, SaveData.Capture(Seed, miner, mine, elevator, _changes));
            LastSaveFailed = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastSaveFailed = true;
        }

        Saves++;
        return !LastSaveFailed;
    }
}
