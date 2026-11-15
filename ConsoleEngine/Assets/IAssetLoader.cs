using System.Collections.Generic;
using System.IO;

namespace ConsoleEngine.Assets;

/// <summary>
/// Turns the bytes of one file format into a <typeparamref name="T"/>. Register it with
/// <see cref="AssetManager.RegisterLoader{T}"/>; a game adds its own formats (maps, levels, dialog) the same way the
/// engine adds its built-in ones.
/// </summary>
public interface IAssetLoader<T>
{
    /// <summary>File extensions this loader reads, without the dot (<c>txt</c>). Matched case-insensitively.</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Reads one asset. Don't dispose <paramref name="stream"/>: the manager does. Throw on bad data; the manager wraps
    /// the exception in an <see cref="AssetLoadException"/> that names the asset. Never return <c>null</c>.
    /// </summary>
    T Load(Stream stream, AssetLoadContext context);
}
