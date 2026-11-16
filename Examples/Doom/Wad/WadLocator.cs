using System;
using System.Collections.Generic;
using System.IO;

namespace Doom.Wad;

/// <summary>
/// Finds the WAD to play: the path in the <c>DOOM_WAD</c> environment variable, else <c>DOOM1.WAD</c> (or
/// <c>doom1.wad</c>) next to the exe, in the working directory, or in a <c>wad</c> folder in either.
/// </summary>
public static class WadLocator
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string EnvironmentVariable = "DOOM_WAD";
    public const string ShareWareName = "DOOM1.WAD";

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Every path that is tried, in order.</summary>
    public static List<string> Candidates()
    {
        var candidates = new List<string>();
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            candidates.Add(fromEnvironment);

        foreach (var folder in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            candidates.Add(Path.Combine(folder, ShareWareName));
            candidates.Add(Path.Combine(folder, ShareWareName.ToLowerInvariant()));
            candidates.Add(Path.Combine(folder, "wad", ShareWareName));
            candidates.Add(Path.Combine(folder, "wad", ShareWareName.ToLowerInvariant()));
        }

        return candidates;
    }

    /// <summary>The first existing candidate, or null.</summary>
    public static string Find()
    {
        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
