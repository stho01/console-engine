using System.Collections.Generic;

namespace ConsoleEngine.Assets;

/// <summary>What an <see cref="IAssetLoader{T}"/> knows about the asset it is loading, and how it loads others.</summary>
public sealed class AssetLoadContext
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly AssetManager _manager;

    //**********************************************************
    //** ctor
    //**********************************************************

    internal AssetLoadContext(AssetManager manager, AssetPath path)
    {
        _manager = manager;
        Path = path;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The asset being loaded.</summary>
    public AssetPath Path { get; }

    /// <summary>The dependencies loaded so far. They belong to the asset being loaded and are released with it.</summary>
    internal List<AssetEntry> Dependencies { get; } = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Loads an asset this one depends on, for example the tileset a map names. <paramref name="relativePath"/> is
    /// resolved against the folder of <see cref="Path"/>: <c>../Tiles/rock.txt</c> from <c>Maps/map1.txt</c> is
    /// <c>Tiles/rock.txt</c>. Goes through the cache like any other load, and stays loaded as long as the asset that
    /// loaded it.
    /// </summary>
    public T Load<T>(string relativePath)
    {
        var entry = _manager.GetOrLoad<T>(Path.Directory.Resolve(relativePath));
        if (!Dependencies.Contains(entry))
        {
            entry.RefCount++;
            Dependencies.Add(entry);
        }

        return (T)entry.Asset;
    }
}
