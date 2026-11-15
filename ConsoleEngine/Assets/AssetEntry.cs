using System;
using System.Collections.Generic;

namespace ConsoleEngine.Assets;

/// <summary>
/// One cached asset. <see cref="RefCount"/> counts its owners: each <see cref="AssetScope"/> that loaded it once, plus
/// each cached asset that loaded it as a dependency. At 0 it is removed from the cache and its store slot is freed.
/// </summary>
internal sealed class AssetEntry
{
    public AssetPath Path { get; init; }
    public Type Type { get; init; }
    public object Asset { get; init; }
    public IAssetStore Store { get; init; }
    public int Slot { get; init; }
    public int Generation { get; init; }

    /// <summary>Assets this one loaded through <see cref="AssetLoadContext.Load{T}"/>. Released with it.</summary>
    public List<AssetEntry> Dependencies { get; init; }

    public int RefCount { get; set; }

    /// <summary>Released or cleared. Scopes that still list it skip it.</summary>
    public bool IsRemoved { get; set; }
}
