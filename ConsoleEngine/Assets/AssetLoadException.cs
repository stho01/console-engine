using System;
using System.Collections.Generic;

namespace ConsoleEngine.Assets;

/// <summary>
/// An asset couldn't be loaded. Catch this to handle every asset failure; the subclasses say why. A loader's own
/// exception is the <see cref="Exception.InnerException"/>.
/// </summary>
public class AssetLoadException : Exception
{
    public AssetLoadException(AssetPath path, string message, Exception innerException = null)
        : base(message, innerException)
    {
        Path = path;
    }

    public AssetPath Path { get; }

    internal static AssetLoadException LoaderFailed(AssetPath path, Type assetType, Type loaderType, Exception inner) =>
        new(path, $"Failed to load asset '{path}' as {assetType.Name} with {loaderType.Name}: {inner.Message}", inner);

    internal static AssetLoadException LoaderReturnedNull(AssetPath path, Type loaderType) =>
        new(path, $"{loaderType.Name} returned null for asset '{path}'.");

    internal static AssetLoadException Circular(AssetPath path, string chain) =>
        new(path, $"Circular asset dependency: {chain}.");
}

/// <summary>There is no file (or entry) for the asset in the <see cref="IAssetSource"/>.</summary>
public sealed class AssetNotFoundException : AssetLoadException
{
    public AssetNotFoundException(AssetPath path, string location, Exception innerException = null)
        : base(path, $"Asset '{path}' not found at '{location}'.", innerException)
    {
        Location = location;
    }

    /// <summary>Where the source looked, from <see cref="IAssetSource.Describe"/>.</summary>
    public string Location { get; }
}

/// <summary>No <see cref="IAssetLoader{T}"/> is registered for the asset's type and extension.</summary>
public sealed class AssetLoaderNotFoundException : AssetLoadException
{
    public AssetLoaderNotFoundException(AssetPath path, Type assetType, IReadOnlyList<string> registeredExtensions)
        : base(path, $"No loader for {assetType.Name} assets with extension '.{path.Extension}' (asset '{path}'). " +
                     $"Registered extensions for {assetType.Name}: {Describe(registeredExtensions)}. " +
                     "Register one with AssetManager.RegisterLoader.")
    {
        AssetType = assetType;
    }

    public Type AssetType { get; }

    private static string Describe(IReadOnlyList<string> extensions) =>
        extensions.Count == 0 ? "none" : "." + string.Join(", .", extensions);
}
