using System.IO;
using ConsoleEngine.Assets;

namespace Kartster.Loading;

/// <summary>
/// Where Kartster's content is under <c>Assets/</c>, and the loaders it needs. Everything is read through an
/// <see cref="AssetManager"/>: the game's own (<c>GameBase.Assets</c>, <c>Assets/</c> next to the exe), or one over a
/// folder given on the command line (<see cref="OpenFolder"/>). The <c>Load</c> methods (<c>Track</c>,
/// <c>KartModel</c>, …) build their own objects from the files and keep none of them: each reads inside an
/// <see cref="AssetScope"/> that ends with the method, so the files' text and geometry leave the cache again.
/// </summary>
public static class KartsterAssets
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>One folder per kart, each with the kart's parts as <c>.obj</c> files.</summary>
    public static readonly AssetPath Karts = "Karts";

    /// <summary>The exported tracks: <c>&lt;id&gt;.ribbon.obj</c> and the files that go with it.</summary>
    public static readonly AssetPath Tracks = "Tracks";

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Adds Kartster's loaders (<see cref="ObjLoader"/>) to <paramref name="assets"/> and returns it.</summary>
    public static AssetManager Register(AssetManager assets)
    {
        assets.RegisterLoader(new ObjLoader());
        return assets;
    }

    /// <summary>
    /// An asset manager for a folder anywhere on disk, for example the original Kartster's kart folder. Its root is
    /// the folder's parent, so the folder's neighbours can be reached too (a tracks folder's <c>../Scenery</c>).
    /// </summary>
    public static (AssetManager Assets, AssetPath Folder) OpenFolder(string folder)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var parent = Path.GetDirectoryName(full);
        return parent == null
            ? (Register(new AssetManager(new DirectoryAssetSource(full))), AssetPath.Root)
            : (Register(new AssetManager(new DirectoryAssetSource(parent))), AssetPath.Parse(Path.GetFileName(full)));
    }
}
