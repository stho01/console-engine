using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleEngine.Assets;

/// <summary>Assets held in memory. For tests, and for content a game generates at startup.</summary>
public sealed class MemoryAssetSource : IAssetSource
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Dictionary<AssetPath, byte[]> _assets = new();

    //**********************************************************
    //** props
    //**********************************************************

    public int Count => _assets.Count;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Adds or replaces an asset. Text is stored as UTF-8 without BOM.</summary>
    public MemoryAssetSource Add(AssetPath path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Add(path, Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Adds or replaces an asset. The array is used as is, not copied.</summary>
    public MemoryAssetSource Add(AssetPath path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (path.IsRoot)
            throw new ArgumentException("An asset path can't be empty.", nameof(path));

        _assets[path] = bytes;
        return this;
    }

    public bool Remove(AssetPath path) => _assets.Remove(path);

    public bool Exists(AssetPath path) => _assets.ContainsKey(path);

    public Stream Open(AssetPath path) => _assets.TryGetValue(path, out var bytes)
        ? new MemoryStream(bytes, writable: false)
        : throw new AssetNotFoundException(path, Describe(path));

    public IReadOnlyList<AssetPath> Enumerate(AssetPath directory, string extension)
    {
        var wanted = AssetPath.NormalizeExtension(extension);
        var paths = new List<AssetPath>();
        foreach (var path in _assets.Keys)
        {
            if (path.Directory == directory && path.Extension == wanted)
                paths.Add(path);
        }

        paths.Sort(static (a, b) => string.CompareOrdinal(a.Value, b.Value));
        return paths;
    }

    public string Describe(AssetPath path) => $"memory:{path.Value}";
}
