using System.Collections.Generic;
using System.IO;

namespace ConsoleEngine.Assets;

/// <summary>
/// Where asset bytes come from: a folder, memory, embedded resources. Knows nothing about formats; that is the job of
/// an <see cref="IAssetLoader{T}"/>.
/// </summary>
public interface IAssetSource
{
    bool Exists(AssetPath path);

    /// <summary>Opens the asset for reading. The caller disposes the stream.</summary>
    /// <exception cref="AssetNotFoundException">There is no asset at <paramref name="path"/>.</exception>
    Stream Open(AssetPath path);

    /// <summary>
    /// The assets directly in <paramref name="directory"/> (not in subfolders) with the given extension, sorted
    /// ordinally. Empty if the folder doesn't exist.
    /// </summary>
    IReadOnlyList<AssetPath> Enumerate(AssetPath directory, string extension);

    /// <summary>
    /// Where <paramref name="path"/> is or would be stored, for error messages. A file path for a folder source.
    /// </summary>
    string Describe(AssetPath path);
}
