using System.Collections.Generic;
using System.IO;
using ConsoleEngine.Assets;
using Microsoft.Xna.Framework;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs;

/// <summary>
/// A parsed map file: header values and one <see cref="TileKind"/> per grid cell. Cached by
/// <see cref="ConsoleEngine.GameBase.Assets"/> and shared by every load of the same map (a restart, the next round of
/// the rotation), so it is only read, never changed.
/// </summary>
public sealed class Map
{
    public string Name { get; init; }
    public int MaxPower { get; init; }
    public int Sequences { get; init; }
    public TileKind[,] Tiles { get; init; }

    /// <summary>World position of the last <c>S</c>. <c>null</c> if the map has none.</summary>
    public Vector2? StartPosition { get; init; }
}

/// <summary>
/// Reads <c>Assets/Maps/mapN.txt</c> exactly like the original's <c>WorldLoader</c>, quirks included: grid rows before
/// <c>!width</c> and <c>!height</c> are dropped, and every header line after both are set starts a new, empty grid.
/// Registered with <see cref="AssetManager.RegisterLoader{T}"/> in <see cref="TerraformEcsGame"/>.
/// </summary>
public sealed class MapLoader : IAssetLoader<Map>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Width and height of a grid cell, in console cells.</summary>
    public const int TileSize = 5;

    //**********************************************************
    //** props
    //**********************************************************

    public IReadOnlyList<string> Extensions { get; } = ["txt"];

    //**********************************************************
    //** public methods
    //**********************************************************

    public Map Load(Stream stream, AssetLoadContext context)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        return Parse(lines);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static Map Parse(List<string> lines)
    {
        var name = string.Empty;
        var sequences = 0;
        var width = 0;
        var height = 0;
        var maxPower = 0;
        TileKind[,] tiles = null;
        Vector2? startPosition = null;
        var y = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith('!'))
            {
                var keyValue = line.Split(':');
                var key = keyValue[0].ToLower().Trim();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "!name": name = value; break;
                    case "!sequences": sequences = int.Parse(value); break;
                    case "!width": width = int.Parse(value); break;
                    case "!height": height = int.Parse(value); break;
                    case "!power": maxPower = int.Parse(value); break;
                }

                if (width != 0 && height != 0)
                    tiles = new TileKind[width, height];

                continue;
            }

            for (var x = 0; x < line.Length; x++)
            {
                var kind = ToTileKind(line[x]);

                // The original kept its StartingPoint object even when the row was dropped, at position (0, 0).
                if (kind == TileKind.StartingPoint)
                    startPosition = tiles != null ? new Vector2(x, y) * TileSize : Vector2.Zero;

                if (tiles != null && kind != TileKind.None)
                    tiles[x, y] = kind;
            }

            y++;
        }

        return new Map
        {
            Name = name,
            MaxPower = maxPower,
            Sequences = sequences,
            Tiles = tiles,
            StartPosition = startPosition
        };
    }

    private static TileKind ToTileKind(char character) => character switch
    {
        'S' => TileKind.StartingPoint,
        'F' => TileKind.FinishPoint,
        'H' => TileKind.Rock,
        'P' => TileKind.PlantSpot,
        'C' => TileKind.Crater,
        'B' => TileKind.BonusPoint,
        _ => TileKind.None
    };
}
