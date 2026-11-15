using System;

namespace ConsoleEngine.Assets;

/// <summary>
/// A small id for a loaded asset, from <see cref="AssetScope.LoadHandle{T}"/>. Only <c>int</c>s, so it is
/// <c>unmanaged</c> and fits in an ECS component; <see cref="AssetManager.Get{T}"/> turns it back into the asset with an
/// array lookup. It does not keep the asset loaded (the scope does): once the asset is released the handle is stale and
/// <c>Get</c> throws, even if the slot is reused. <c>default</c> is <see cref="Null"/>, never loaded.
/// </summary>
public readonly struct AssetHandle<T> : IEquatable<AssetHandle<T>>
{
    //**********************************************************
    //** fields
    //**********************************************************

    public static readonly AssetHandle<T> Null = default;

    //**********************************************************
    //** ctor
    //**********************************************************

    internal AssetHandle(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Slot in the manager's store for <typeparamref name="T"/>.</summary>
    public int Index { get; }

    /// <summary>Which use of the slot this handle belongs to. Never 0 for a loaded asset.</summary>
    public int Generation { get; }

    public bool IsNull => Generation == 0;

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool Equals(AssetHandle<T> other) => Index == other.Index && Generation == other.Generation;

    public override bool Equals(object obj) => obj is AssetHandle<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Index, Generation);

    public override string ToString() => $"AssetHandle<{typeof(T).Name}>({Index}v{Generation})";

    public static bool operator ==(AssetHandle<T> left, AssetHandle<T> right) => left.Equals(right);

    public static bool operator !=(AssetHandle<T> left, AssetHandle<T> right) => !left.Equals(right);
}
