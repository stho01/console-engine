using System;
using System.Collections.Generic;

namespace ConsoleEngine.Assets;

/// <summary>
/// Owns the assets loaded through it, like a MonoGame <c>ContentManager</c> per level. The cache is shared: two scopes
/// that load the same path and type get the same instance, and it stays loaded until the last owner lets go.
/// <see cref="Dispose"/> lets go of everything this scope loaded; assets nobody else owns are removed from the cache
/// (and disposed if they are <see cref="IDisposable"/>), and their handles go stale. Every
/// <see cref="Infrastructure.Scenery.Scene"/> has one (<c>Scene.Assets</c>), disposed when the scene is unloaded. Same
/// threading rule as the <see cref="AssetManager"/>.
/// </summary>
public sealed class AssetScope : IDisposable
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly HashSet<AssetEntry> _owned = new();

    //**********************************************************
    //** ctor
    //**********************************************************

    internal AssetScope(AssetManager manager)
    {
        Manager = manager;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public AssetManager Manager { get; }

    /// <summary>Number of assets this scope owns.</summary>
    public int Count => _owned.Count;

    public bool IsDisposed { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Loads (or finds in the cache) the asset and makes this scope one of its owners.</summary>
    /// <exception cref="ObjectDisposedException">The scope was disposed.</exception>
    /// <exception cref="AssetLoadException">See <see cref="AssetManager.Load{T}"/>.</exception>
    public T Load<T>(AssetPath path) => (T)Acquire<T>(path).Asset;

    /// <summary>Like <see cref="Load{T}"/>, but returns <c>false</c> when the asset doesn't exist.</summary>
    public bool TryLoad<T>(AssetPath path, out T asset)
    {
        ThrowIfDisposed();
        if (!Manager.TryGetOrLoad<T>(path, out var entry))
        {
            asset = default;
            return false;
        }

        Own(entry);
        asset = (T)entry.Asset;
        return true;
    }

    /// <summary>
    /// Like <see cref="Load{T}"/>, but returns a handle to keep in an ECS component. Turn it back into the asset with
    /// <see cref="Get{T}"/>; the handle is valid while some scope owns the asset.
    /// </summary>
    public AssetHandle<T> LoadHandle<T>(AssetPath path)
    {
        var entry = Acquire<T>(path);
        return new AssetHandle<T>(entry.Slot, entry.Generation);
    }

    /// <summary>The asset behind <paramref name="handle"/>. Allocation-free; safe in the frame loop.</summary>
    public T Get<T>(AssetHandle<T> handle) => Manager.Get(handle);

    public IReadOnlyList<AssetPath> Enumerate(AssetPath directory, string extension) =>
        Manager.Enumerate(directory, extension);

    /// <summary>Lets go of every asset this scope loaded. Calling it again does nothing.</summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        foreach (var entry in _owned)
            Manager.Release(entry);

        _owned.Clear();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>After <see cref="AssetManager.Clear"/>: the entries are gone, nothing to release.</summary>
    internal void Forget() => _owned.Clear();

    private AssetEntry Acquire<T>(AssetPath path)
    {
        ThrowIfDisposed();
        var entry = Manager.GetOrLoad<T>(path);
        Own(entry);
        return entry;
    }

    private void Own(AssetEntry entry)
    {
        if (_owned.Add(entry))
            entry.RefCount++;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
