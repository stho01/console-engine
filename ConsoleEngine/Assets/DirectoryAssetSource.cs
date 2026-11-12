using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleEngine.Assets;

/// <summary>
/// Reads assets from a folder on disk. <see cref="AssetPath"/> can't be rooted or leave its root, so every read stays
/// inside <see cref="RootDirectory"/>.
/// </summary>
public sealed class DirectoryAssetSource : IAssetSource
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Folder name, next to the game's exe, that <see cref="CreateDefault"/> reads from.</summary>
    public const string DefaultFolder = "Assets";

    //**********************************************************
    //** ctor
    //**********************************************************

    public DirectoryAssetSource(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Absolute path of the folder assets are read from.</summary>
    public string RootDirectory { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// A source for <c>Assets/</c> next to the exe (<see cref="AppContext.BaseDirectory"/>), so it doesn't matter which
    /// folder the game was started from.
    /// </summary>
    public static DirectoryAssetSource CreateDefault() =>
        new(Path.Combine(AppContext.BaseDirectory, DefaultFolder));

    public bool Exists(AssetPath path) => !path.IsRoot && File.Exists(Describe(path));

    public Stream Open(AssetPath path)
    {
        var file = Describe(path);
        try
        {
            return File.OpenRead(file);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new AssetNotFoundException(path, file, e);
        }
    }

    public IReadOnlyList<AssetPath> Enumerate(AssetPath directory, string extension)
    {
        var folder = Describe(directory);
        if (!Directory.Exists(folder))
            return [];

        var wanted = AssetPath.NormalizeExtension(extension);
        var paths = new List<AssetPath>();
        foreach (var file in Directory.GetFiles(folder))
        {
            // Filtered here, not with a "*.txt" search pattern: on Windows that pattern also matches ".txt2".
            var path = directory.Resolve(Path.GetFileName(file));
            if (path.Extension == wanted)
                paths.Add(path);
        }

        paths.Sort(static (a, b) => string.CompareOrdinal(a.Value, b.Value));
        return paths;
    }

    public string Describe(AssetPath path) => path.IsRoot
        ? RootDirectory
        : Path.Combine(RootDirectory, path.Value.Replace('/', Path.DirectorySeparatorChar));
}
