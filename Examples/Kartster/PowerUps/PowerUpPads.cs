using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Kartster.PowerUps;

/// <summary>
/// Reads <c>&lt;id&gt;.powerups.txt</c>: the pads a normal race in Kartster has (<c>TrackPowerUpPadResolver</c>, the
/// <c>IsExtra</c> ones left out), exported with Kartster's own code. One line per pad, <c>#</c> starts a comment:
/// <code>pad x y z setId</code>
/// World metres (+Y up). Only the <c>normal</c> set exists in the port; another set id is read as normal.
/// </summary>
public static class PowerUpPads
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The pads of track <paramref name="id"/> in <paramref name="folder"/>; none without the file.</summary>
    public static Vector3[] Load(string folder, string id)
    {
        var path = Path.Combine(folder, id + ".powerups.txt");
        return File.Exists(path) ? Parse(File.ReadLines(path), Path.GetFileName(path)) : [];
    }

    /// <exception cref="InvalidDataException">A line is not <c>pad x y z setId</c>.</exception>
    public static Vector3[] Parse(IEnumerable<string> lines, string source)
    {
        var pads = new List<Vector3>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            var hash = line.IndexOf('#');
            var parts = (hash < 0 ? line : line[..hash]).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts.Length != 5 || parts[0] != "pad"
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                throw new InvalidDataException($"{source}:{lineNumber}: expected 'pad x y z setId'");

            pads.Add(new Vector3(x, y, z));
        }

        return pads.ToArray();
    }
}
