using System;
using System.Collections.Generic;
using Doom.Wad;

namespace Doom.Maps;

/// <summary>Map names (<c>ExMy</c> for Doom, <c>MAPxx</c> for Doom II) and which map follows which.</summary>
public static class MapNames
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>True for <c>E1M1</c>-style and <c>MAP01</c>-style names.</summary>
    public static bool IsMapName(string name) =>
        (name.Length == 4 && name[0] == 'E' && char.IsDigit(name[1]) && name[2] == 'M' && char.IsDigit(name[3]))
        || (name.Length == 5 && name.StartsWith("MAP", StringComparison.Ordinal) && char.IsDigit(name[3])
            && char.IsDigit(name[4]));

    /// <summary>The maps in the WAD, in lump order (a marker followed by <c>THINGS</c>).</summary>
    public static List<string> Find(WadFile wad)
    {
        var maps = new List<string>();
        for (var i = 0; i + 1 < wad.Count; i++)
        {
            var name = wad.Lumps[i].Name;
            if (IsMapName(name) && wad.Lumps[i + 1].Name == "THINGS" && !maps.Contains(name))
                maps.Add(name);
        }

        return maps;
    }

    /// <summary>
    /// The map after <paramref name="current"/>, or null when the episode ends. Doom: M8 ends the episode, the secret
    /// exit leads to M9, and M9 returns to the map after the one with the secret exit. Doom II: MAP15 → 31 → 32 → 16.
    /// </summary>
    public static string Next(string current, bool secret)
    {
        if (current.Length == 4 && current[0] == 'E')
        {
            var episode = current[1] - '0';
            var map = current[3] - '0';
            if (secret)
                return $"E{episode}M9";
            if (map == 9)
                return $"E{episode}M{SecretReturn(episode)}";
            if (map >= 8)
                return null;

            return $"E{episode}M{map + 1}";
        }

        if (current.Length == 5 && int.TryParse(current[3..], out var number))
        {
            if (secret)
                return number == 31 ? "MAP32" : "MAP31";
            if (number is 31 or 32)
                return "MAP16";
            if (number >= 30)
                return null;

            return $"MAP{number + 1:00}";
        }

        return null;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The map the secret level (M9) returns to.</summary>
    private static int SecretReturn(int episode) => episode switch
    {
        1 => 4,
        2 => 6,
        3 => 7,
        _ => 3
    };
}
