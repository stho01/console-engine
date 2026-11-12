using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ConsoleEngine.Assets.Loaders;

namespace ConsoleEngine.Assets;

/// <summary>
/// Loads assets by path and type through registered <see cref="IAssetLoader{T}"/>s and caches them: the same path and
/// type give the same instance. Cached assets are shared, so don't change them. Every cached asset has owners
/// (<see cref="AssetScope"/>s, and assets that loaded it as a dependency) and is released when the last one lets go.
/// <see cref="Load{T}"/> on the manager itself loads into its root scope, which is never released (until
/// <see cref="Clear"/>): use it for assets the whole game shares, and <c>Scene.Assets</c> for a scene's own.
/// Loading reads files and allocates, so do it in <c>OnInitialize</c>/<c>OnLoad</c>/<c>OnCreate</c>, never per frame;
/// <see cref="Get{T}"/> with a handle is the frame-loop lookup. Not thread-safe: use it from the game loop thread, like
/// the rest of the engine.
/// </summary>
public sealed class AssetManager
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Dictionary<(Type Type, string Extension), object> _loaders = new();
    private readonly Dictionary<(AssetPath Path, Type Type), AssetEntry> _cache = new();
    private readonly List<(AssetPath Path, Type Type)> _loading = new();
    private readonly AssetScope _root;
    private object[] _stores = new object[8];

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <summary>A manager over <paramref name="source"/> with the built-in loaders (<see cref="TextLoader"/>,
    /// <see cref="LinesLoader"/>, <see cref="SpriteLoader"/>) registered.</summary>
    public AssetManager(IAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        _root = new AssetScope(this);
        RegisterLoader(new TextLoader());
        RegisterLoader(new LinesLoader());
        RegisterLoader(new SpriteLoader());
    }

    //**********************************************************
    //** props
    //**********************************************************

    public IAssetSource Source { get; }

    /// <summary>Number of cached assets.</summary>
    public int Count => _cache.Count;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Makes <paramref name="loader"/> the loader for <typeparamref name="T"/> assets with its extensions.
    /// </summary>
    /// <exception cref="ArgumentException">The loader lists no extensions, or an empty one.</exception>
    /// <exception cref="InvalidOperationException">
    /// Another loader is already registered for <typeparamref name="T"/> and one of the extensions. Nothing is registered.
    /// </exception>
    public void RegisterLoader<T>(IAssetLoader<T> loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var extensions = loader.Extensions;
        if (extensions is null || extensions.Count == 0)
            throw new ArgumentException($"{loader.GetType().Name} lists no extensions.", nameof(loader));

        var keys = new (Type, string)[extensions.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            var extension = AssetPath.NormalizeExtension(extensions[i]);
            if (extension.Length == 0)
                throw new ArgumentException($"{loader.GetType().Name} lists an empty extension.", nameof(loader));

            keys[i] = (typeof(T), extension);
            if (_loaders.TryGetValue(keys[i], out var existing))
                throw new InvalidOperationException(
                    $"A loader for {typeof(T).Name} assets with extension '.{extension}' is already registered " +
                    $"({existing.GetType().Name}).");
        }

        foreach (var key in keys)
            _loaders[key] = loader;
    }

    /// <summary>A new, empty scope. Dispose it to let go of what it loaded.</summary>
    public AssetScope CreateScope() => new(this);

    /// <summary>
    /// The cached asset, or loads it with the loader registered for <typeparamref name="T"/> and its extension. The
    /// manager's root scope owns it, so it stays loaded until <see cref="Clear"/>.
    /// </summary>
    /// <exception cref="AssetNotFoundException">The source has no such asset.</exception>
    /// <exception cref="AssetLoaderNotFoundException">No loader for <typeparamref name="T"/> and the extension.</exception>
    /// <exception cref="AssetLoadException">
    /// The loader failed, returned <c>null</c>, or the asset depends on itself.
    /// </exception>
    public T Load<T>(AssetPath path) => _root.Load<T>(path);

    /// <summary>
    /// Like <see cref="Load{T}"/>, but returns <c>false</c> when the asset doesn't exist. A missing loader or a loader
    /// failure still throws: those are bugs, not optional content.
    /// </summary>
    public bool TryLoad<T>(AssetPath path, out T asset) => _root.TryLoad(path, out asset);

    /// <summary>Like <see cref="Load{T}"/>, but returns a handle. See <see cref="AssetScope.LoadHandle{T}"/>.</summary>
    public AssetHandle<T> LoadHandle<T>(AssetPath path) => _root.LoadHandle<T>(path);

    /// <summary>
    /// The asset behind <paramref name="handle"/>: an array index and a generation check, no allocation, safe in the
    /// frame loop.
    /// </summary>
    /// <exception cref="InvalidOperationException">The asset was released or the handle is <c>Null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(AssetHandle<T> handle)
    {
        var id = AssetTypeId<T>.Value;
        var stores = _stores;
        if ((uint)id < (uint)stores.Length && stores[id] is { } store)
            return Unsafe.As<AssetStore<T>>(store).Get(handle);

        return AssetStore<T>.ThrowNotLoaded(handle);
    }

    /// <summary>
    /// The store of every loaded <typeparamref name="T"/>. Get it once (in <c>OnCreate</c>) and keep it: its
    /// <see cref="AssetStore{T}.Get"/> skips the per-type lookup <see cref="Get{T}"/> does, so it is the fastest way to
    /// turn many handles into assets each frame. The same store stays valid for the life of the manager.
    /// </summary>
    public AssetStore<T> GetStore<T>() => StoreOf<T>();

    /// <summary>Whether <see cref="Get{T}"/> would return an asset for <paramref name="handle"/>.</summary>
    public bool IsLoaded<T>(AssetHandle<T> handle)
    {
        var id = AssetTypeId<T>.Value;
        return (uint)id < (uint)_stores.Length && _stores[id] is AssetStore<T> store && store.IsLoaded(handle);
    }

    public bool IsLoaded<T>(AssetPath path) => _cache.ContainsKey((path, typeof(T)));

    /// <summary>The assets directly in <paramref name="directory"/> with <paramref name="extension"/>, sorted.</summary>
    public IReadOnlyList<AssetPath> Enumerate(AssetPath directory, string extension) =>
        Source.Enumerate(directory, extension);

    /// <summary>
    /// Drops every cached asset, whoever owns it, and disposes the <see cref="IDisposable"/> ones. Every handle goes
    /// stale. Scopes stay usable; the next load reads from the source again.
    /// </summary>
    public void Clear()
    {
        foreach (var entry in _cache.Values)
            Remove(entry, releaseDependencies: false);

        _cache.Clear();
        _root.Forget();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The cached entry, or a new one with no owners yet. The caller adds itself as an owner.</summary>
    internal AssetEntry GetOrLoad<T>(AssetPath path)
    {
        if (_cache.TryGetValue((path, typeof(T)), out var cached))
            return cached;

        return LoadUncached(path, FindLoader<T>(path));
    }

    internal bool TryGetOrLoad<T>(AssetPath path, out AssetEntry entry)
    {
        if (_cache.TryGetValue((path, typeof(T)), out entry))
            return true;

        var loader = FindLoader<T>(path);
        if (!Source.Exists(path))
            return false;

        entry = LoadUncached(path, loader);
        return true;
    }

    /// <summary>One owner lets go. The last one removes the asset and releases its dependencies.</summary>
    internal void Release(AssetEntry entry)
    {
        if (entry.IsRemoved || --entry.RefCount > 0)
            return;

        _cache.Remove((entry.Path, entry.Type));
        Remove(entry, releaseDependencies: true);
    }

    private void Remove(AssetEntry entry, bool releaseDependencies)
    {
        entry.IsRemoved = true;
        entry.Store.Remove(entry.Slot);
        (entry.Asset as IDisposable)?.Dispose();

        if (!releaseDependencies)
            return;

        foreach (var dependency in entry.Dependencies)
            Release(dependency);
    }

    private IAssetLoader<T> FindLoader<T>(AssetPath path)
    {
        if (_loaders.TryGetValue((typeof(T), path.Extension), out var loader))
            return (IAssetLoader<T>)loader;

        var registered = new List<string>();
        foreach (var key in _loaders.Keys)
        {
            if (key.Type == typeof(T))
                registered.Add(key.Extension);
        }

        registered.Sort(StringComparer.Ordinal);
        throw new AssetLoaderNotFoundException(path, typeof(T), registered);
    }

    private AssetEntry LoadUncached<T>(AssetPath path, IAssetLoader<T> loader)
    {
        var key = (path, typeof(T));
        if (_loading.Contains(key))
            throw AssetLoadException.Circular(path, DescribeChain(key));

        _loading.Add(key);
        var context = new AssetLoadContext(this, path);
        try
        {
            T asset;
            using (var stream = Source.Open(path))
            {
                try
                {
                    asset = loader.Load(stream, context);
                }
                catch (Exception e)
                {
                    throw AssetLoadException.LoaderFailed(path, typeof(T), loader.GetType(), e);
                }
            }

            if (asset is null)
                throw AssetLoadException.LoaderReturnedNull(path, loader.GetType());

            var store = StoreOf<T>();
            var handle = store.Add(asset);
            var entry = new AssetEntry
            {
                Path = path,
                Type = typeof(T),
                Asset = asset,
                Store = store,
                Slot = handle.Index,
                Generation = handle.Generation,
                Dependencies = context.Dependencies
            };
            _cache[key] = entry;
            return entry;
        }
        catch
        {
            // The failed asset owns nothing: let go of the dependencies it had loaded.
            foreach (var dependency in context.Dependencies)
                Release(dependency);

            throw;
        }
        finally
        {
            _loading.RemoveAt(_loading.Count - 1);
        }
    }

    private AssetStore<T> StoreOf<T>()
    {
        var id = AssetTypeId<T>.Value;
        if (id >= _stores.Length)
            Array.Resize(ref _stores, Math.Max(id + 1, _stores.Length * 2));

        return (AssetStore<T>)(_stores[id] ??= new AssetStore<T>());
    }

    private string DescribeChain((AssetPath Path, Type Type) repeated)
    {
        var chain = new StringBuilder();
        for (var i = _loading.IndexOf(repeated); i < _loading.Count; i++)
            chain.Append($"{_loading[i].Path} ({_loading[i].Type.Name}) -> ");

        return chain.Append($"{repeated.Path} ({repeated.Type.Name})").ToString();
    }
}
