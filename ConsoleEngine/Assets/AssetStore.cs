using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ConsoleEngine.Assets;

/// <summary>Frees a slot without knowing the asset type. Used when a cache entry is released.</summary>
internal interface IAssetStore
{
    void Remove(int index);
}

/// <summary>Process-wide index per asset type into <see cref="AssetManager"/>'s store array.</summary>
internal static class AssetTypeIds
{
    internal static int Next;
}

/// <summary>The id of <typeparamref name="T"/>. A constant in JIT-compiled generic code, so the lookup is free.</summary>
internal static class AssetTypeId<T>
{
    internal static readonly int Value = Interlocked.Increment(ref AssetTypeIds.Next) - 1;
}

/// <summary>
/// The loaded assets of one type in a dense array, so <see cref="Get"/> is an index and a generation compare. Freed
/// slots are reused with the next generation, which makes old handles to them stale. Like Bevy's <c>Assets&lt;T&gt;</c>:
/// a system that looks up many handles per frame gets the store once with <see cref="AssetManager.GetStore{T}"/> (in
/// <c>OnCreate</c>) and keeps it; that is the fastest lookup. Only the <see cref="AssetManager"/> adds and removes.
/// </summary>
public sealed class AssetStore<T> : IAssetStore
{
    //**********************************************************
    //** fields
    //**********************************************************

    private struct Slot
    {
        public T Asset;
        public int Generation;
    }

    private Slot[] _slots = new Slot[16];
    private readonly Stack<int> _free = new();
    private int _used;

    //**********************************************************
    //** public methods
    //**********************************************************

    internal AssetStore() { }

    internal AssetHandle<T> Add(T asset)
    {
        int index;
        if (_free.Count > 0)
        {
            index = _free.Pop();
        }
        else
        {
            if (_used == _slots.Length)
                Array.Resize(ref _slots, _slots.Length * 2);

            index = _used++;
        }

        ref var slot = ref _slots[index];
        slot.Asset = asset;
        if (slot.Generation == 0)
            slot.Generation = 1;

        return new AssetHandle<T>(index, slot.Generation);
    }

    void IAssetStore.Remove(int index)
    {
        ref var slot = ref _slots[index];
        slot.Asset = default;
        slot.Generation = slot.Generation == int.MaxValue ? 1 : slot.Generation + 1;
        _free.Push(index);
    }

    /// <summary>A removed slot already has the next generation, so a matching generation means loaded.</summary>
    public bool IsLoaded(AssetHandle<T> handle) =>
        handle.Generation != 0 &&
        (uint)handle.Index < (uint)_used &&
        _slots[handle.Index].Generation == handle.Generation;

    /// <summary>The asset behind <paramref name="handle"/>. No allocation.</summary>
    /// <exception cref="InvalidOperationException">The asset was released or the handle is <c>Null</c>.</exception>
    public T Get(AssetHandle<T> handle)
    {
        var slots = _slots;
        if ((uint)handle.Index < (uint)slots.Length)
        {
            ref var slot = ref slots[handle.Index];
            if (slot.Generation == handle.Generation && handle.Generation != 0)
                return slot.Asset;
        }

        return ThrowNotLoaded(handle);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    [DoesNotReturn]
    internal static T ThrowNotLoaded(AssetHandle<T> handle) =>
        throw new InvalidOperationException(
            $"{handle} is not loaded: the scope that loaded it was disposed, or the AssetManager was cleared.");
}
