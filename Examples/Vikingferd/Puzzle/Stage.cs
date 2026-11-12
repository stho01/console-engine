using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Vikingferd.Components;

namespace Vikingferd.Puzzle;

/// <summary>
/// A level's tiles and switches. Doors and bridges belong to one of <see cref="ChannelCount"/> channels; a channel is
/// on while any of its levers is on or any of its plates is pressed. A gate (door or bridge) only becomes solid when
/// nothing overlaps it, so it never closes on a hero or crate. Everything outside the map is rock.
/// </summary>
public sealed class Stage
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int ChannelCount = 5;

    /// <summary>A plate is pressed by anything overlapping this much of its tile, from the bottom.</summary>
    private const float PlateHeight = .25f;

    private readonly TileKind[] _tiles;
    private readonly byte[] _channels;

    /// <summary>Per tile: door open, bridge solid, plate pressed or lever on.</summary>
    private readonly bool[] _on;

    private readonly bool[] _channelOn = new bool[ChannelCount];
    private readonly int[] _levers;
    private readonly int[] _plates;
    private readonly int[] _gates;

    //**********************************************************
    //** ctor
    //**********************************************************

    private Stage(int columns, int rows, TileKind[] tiles, byte[] channels)
    {
        Columns = columns;
        Rows = rows;
        _tiles = tiles;
        _channels = channels;
        _on = new bool[tiles.Length];
        var levers = new List<int>();
        var plates = new List<int>();
        var gates = new List<int>();
        for (var i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == TileKind.Lever)
                levers.Add(i);
            else if (tiles[i] == TileKind.Plate)
                plates.Add(i);
            else if (tiles[i] is TileKind.Door or TileKind.Bridge)
                gates.Add(i);
        }

        _levers = levers.ToArray();
        _plates = plates.ToArray();
        _gates = gates.ToArray();
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Columns { get; }
    public int Rows { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Builds the stage from a map (characters on <see cref="Levels"/>) and lists where the heroes and crates start,
    /// in map order (top row first). Throws <see cref="FormatException"/> for an empty map, rows of different widths
    /// or an unknown character.
    /// </summary>
    public static Stage Parse(string[] rows, out Spawn[] spawns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length == 0 || rows[0].Length == 0)
            throw new FormatException("A level needs at least one row and one column.");

        var columns = rows[0].Length;
        var tiles = new TileKind[columns * rows.Length];
        var channels = new byte[columns * rows.Length];
        var found = new List<Spawn>();
        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].Length != columns)
                throw new FormatException($"Row {row} is {rows[row].Length} tiles wide, expected {columns}.");

            for (var column = 0; column < columns; column++)
            {
                var i = row * columns + column;
                tiles[i] = Read(rows[row][column], column, row, out channels[i], found);
            }
        }

        spawns = found.ToArray();
        return new Stage(columns, rows.Length, tiles, channels);
    }

    public TileKind KindAt(int column, int row) => Contains(column, row) ? _tiles[row * Columns + column] : TileKind.Rock;

    /// <summary>The channel (0 = a) of a door, bridge, plate or lever. 0 for any other tile.</summary>
    public int ChannelAt(int column, int row) => Contains(column, row) ? _channels[row * Columns + column] : 0;

    /// <summary>Door open, bridge solid, plate pressed or lever on. False for any other tile.</summary>
    public bool IsOn(int column, int row) => Contains(column, row) && _on[row * Columns + column];

    public bool IsChannelOn(int channel) => _channelOn[channel];

    public bool IsSolid(int column, int row)
    {
        if (!Contains(column, row))
            return true;

        var i = row * Columns + column;
        return _tiles[i] switch
        {
            TileKind.Rock or TileKind.Cracked => true,
            TileKind.Door => !_on[i],
            TileKind.Bridge => _on[i],
            _ => false
        };
    }

    /// <summary>Switches a lever. Returns false if there is no lever at that tile.</summary>
    public bool ToggleLever(int column, int row)
    {
        if (KindAt(column, row) != TileKind.Lever)
            return false;

        var i = row * Columns + column;
        _on[i] = !_on[i];
        return true;
    }

    /// <summary>Turns cracked stone into air. Returns false if the tile isn't cracked stone.</summary>
    public bool Smash(int column, int row)
    {
        if (KindAt(column, row) != TileKind.Cracked)
            return false;

        _tiles[row * Columns + column] = TileKind.Air;
        return true;
    }

    /// <summary>
    /// Presses the plates that something stands on, works out which channels are on, then opens or closes the gates.
    /// Run after everything moved. Gates that move are reported to <paramref name="events"/>, if given.
    /// </summary>
    public void UpdateSwitches(ReadOnlySpan<Actor> actors, PuzzleEvents events = null)
    {
        Array.Clear(_channelOn);
        for (var l = 0; l < _levers.Length; l++)
        {
            if (_on[_levers[l]])
                _channelOn[_channels[_levers[l]]] = true;
        }

        for (var p = 0; p < _plates.Length; p++)
        {
            var i = _plates[p];
            var column = i % Columns;
            var row = i / Columns;
            _on[i] = IsOccupied(actors, column, row + 1f - PlateHeight, 1f, PlateHeight);
            if (_on[i])
                _channelOn[_channels[i]] = true;
        }

        for (var g = 0; g < _gates.Length; g++)
        {
            var i = _gates[g];
            var wanted = _channelOn[_channels[i]];
            if (wanted == _on[i])
                continue;

            // Door: on = open, so turning off makes it solid. Bridge: on = solid.
            var becomesSolid = _tiles[i] == TileKind.Door ? !wanted : wanted;
            if (becomesSolid && IsOccupied(actors, i % Columns, i / Columns, 1f, 1f))
                continue;

            _on[i] = wanted;
            events?.Add(PuzzleEventKind.GateMoved, new Vector2(i % Columns + .5f, i / Columns + .5f),
                channel: _channels[i]);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool Contains(int column, int row) =>
        (uint)column < (uint)Columns && (uint)row < (uint)Rows;

    private static bool IsOccupied(ReadOnlySpan<Actor> actors, float x, float y, float width, float height)
    {
        for (var i = 0; i < actors.Length; i++)
        {
            ref readonly var actor = ref actors[i];
            if (actor.Min.X < x + width - PuzzleRules.Epsilon && actor.Min.X + actor.Size.X > x + PuzzleRules.Epsilon &&
                actor.Min.Y < y + height - PuzzleRules.Epsilon && actor.Min.Y + actor.Size.Y > y + PuzzleRules.Epsilon)
                return true;
        }

        return false;
    }

    private static TileKind Read(char symbol, int column, int row, out byte channel, List<Spawn> spawns)
    {
        channel = 0;
        switch (symbol)
        {
            case '.':
                return TileKind.Air;
            case '#':
                return TileKind.Rock;
            case '%':
                return TileKind.Cracked;
            case '^':
                return TileKind.Spikes;
            case 'X':
                return TileKind.Exit;
            case >= 'a' and <= 'e':
                channel = (byte)(symbol - 'a');
                return TileKind.Door;
            case >= 'p' and <= 't':
                channel = (byte)(symbol - 'p');
                return TileKind.Bridge;
            case >= 'A' and <= 'E':
                channel = (byte)(symbol - 'A');
                return TileKind.Plate;
            case >= '1' and <= '5':
                channel = (byte)(symbol - '1');
                return TileKind.Lever;
            case 'Y':
                spawns.Add(new Spawn(ActorKind.Ylva, column, row));
                return TileKind.Air;
            case 'G':
                spawns.Add(new Spawn(ActorKind.Gorm, column, row));
                return TileKind.Air;
            case 'S':
                spawns.Add(new Spawn(ActorKind.Sigrid, column, row));
                return TileKind.Air;
            case 'o':
                spawns.Add(new Spawn(ActorKind.Crate, column, row));
                return TileKind.Air;
            default:
                throw new FormatException($"Unknown map character '{symbol}' at column {column}, row {row}.");
        }
    }
}
