using System;
using System.Collections.Generic;
using System.IO;
using ConsoleEngine.Assets;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public class AssetScopeTests
{
    //**********************************************************
    //** test assets
    //**********************************************************

    /// <summary>First line is a name, every other line names a <c>.dep</c> file it depends on.</summary>
    private sealed class Tracked : IDisposable
    {
        public string Name { get; init; }
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class TrackedLoader : IAssetLoader<Tracked>
    {
        public IReadOnlyList<string> Extensions { get; } = ["dep"];

        public Tracked Load(Stream stream, AssetLoadContext context)
        {
            using var reader = new StreamReader(stream);
            var tracked = new Tracked { Name = reader.ReadLine() };
            while (reader.ReadLine() is { } dependency)
                context.Load<Tracked>(dependency);

            return tracked;
        }
    }

    /// <summary>A component that holds a handle: must be <c>unmanaged</c> to live in the ECS.</summary>
    private struct SpriteAsset(AssetHandle<Sprite> handle)
    {
        public AssetHandle<Sprite> Handle = handle;
    }

    private MemoryAssetSource _source;
    private AssetManager _assets;

    [SetUp]
    public void SetUp()
    {
        _source = new MemoryAssetSource()
            .Add("a.txt", "A")
            .Add("b.txt", "B")
            .Add("rock.sprite", "#")
            .Add("map.dep", "Map\ntiles.dep")
            .Add("tiles.dep", "Tiles");
        _assets = new AssetManager(_source);
        _assets.RegisterLoader(new TrackedLoader());
    }

    //**********************************************************
    //** scopes
    //**********************************************************

    [Test]
    public void Dispose_OnlyOwner_RemovesAssetFromCache()
    {
        var scope = _assets.CreateScope();
        scope.Load<string>("a.txt");

        scope.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(_assets.IsLoaded<string>("a.txt"), Is.False);
            Assert.That(_assets.Count, Is.Zero);
        });
    }

    [Test]
    public void Dispose_AssetAlsoOwnedByAnotherScope_StaysCachedAsSameInstance()
    {
        var level = _assets.CreateScope();
        var menu = _assets.CreateScope();
        var first = level.Load<string>("a.txt");
        menu.Load<string>("a.txt");

        level.Dispose();

        Assert.That(menu.Load<string>("a.txt"), Is.SameAs(first));
    }

    [Test]
    public void Dispose_AssetAlsoLoadedThroughManager_StaysCached()
    {
        _assets.Load<string>("a.txt");
        var scope = _assets.CreateScope();
        scope.Load<string>("a.txt");

        scope.Dispose();

        Assert.That(_assets.IsLoaded<string>("a.txt"), Is.True);
    }

    [Test]
    public void Load_SameAssetTwiceInOneScope_OwnsItOnce()
    {
        var scope = _assets.CreateScope();
        scope.Load<string>("a.txt");
        scope.Load<string>("a.txt");

        scope.Dispose();

        Assert.That(_assets.IsLoaded<string>("a.txt"), Is.False);
    }

    [Test]
    public void Dispose_IDisposableAsset_IsDisposedWhenReleased()
    {
        var scope = _assets.CreateScope();
        var tiles = scope.Load<Tracked>("tiles.dep");

        scope.Dispose();

        Assert.That(tiles.IsDisposed, Is.True);
    }

    [Test]
    public void Dispose_AssetWithDependency_ReleasesDependencyToo()
    {
        var scope = _assets.CreateScope();
        scope.Load<Tracked>("map.dep");
        var tiles = _assets.CreateScope().Load<Tracked>("tiles.dep");

        Assert.That(_assets.Count, Is.EqualTo(2));
        scope.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(_assets.IsLoaded<Tracked>("map.dep"), Is.False);
            Assert.That(_assets.IsLoaded<Tracked>("tiles.dep"), Is.True, "still owned by the second scope");
            Assert.That(tiles.IsDisposed, Is.False);
        });
    }

    [Test]
    public void Dispose_LastOwnerOfDependency_RemovesBoth()
    {
        var scope = _assets.CreateScope();
        scope.Load<Tracked>("map.dep");

        scope.Dispose();

        Assert.That(_assets.Count, Is.Zero);
    }

    [Test]
    public void Load_ParentFailsAfterLoadingDependency_DependencyIsReleased()
    {
        _source.Add("broken.dep", "Broken\ntiles.dep\nmissing.dep");
        var scope = _assets.CreateScope();

        Assert.Throws<AssetLoadException>(() => scope.Load<Tracked>("broken.dep"));

        Assert.Multiple(() =>
        {
            Assert.That(_assets.Count, Is.Zero);
            Assert.That(scope.Count, Is.Zero);
        });
    }

    [Test]
    public void Load_AfterDispose_ThrowsObjectDisposed()
    {
        var scope = _assets.CreateScope();
        scope.Dispose();

        Assert.That(() => scope.Load<string>("a.txt"), Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void Dispose_Twice_ReleasesOnce()
    {
        _assets.Load<string>("a.txt");
        var scope = _assets.CreateScope();
        scope.Load<string>("a.txt");

        scope.Dispose();
        scope.Dispose();

        Assert.That(_assets.IsLoaded<string>("a.txt"), Is.True);
    }

    [Test]
    public void TryLoad_Missing_ReturnsFalseAndOwnsNothing()
    {
        var scope = _assets.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(scope.TryLoad<string>("missing.txt", out _), Is.False);
            Assert.That(scope.Count, Is.Zero);
        });
    }

    [Test]
    public void Clear_ThenDisposeScope_DoesNotThrowAndScopeReloads()
    {
        var scope = _assets.CreateScope();
        var old = scope.Load<string>("a.txt");
        _assets.Clear();
        _source.Add("a.txt", "A2");

        Assert.Multiple(() =>
        {
            Assert.That(scope.Load<string>("a.txt"), Is.EqualTo("A2").And.Not.SameAs(old));
            Assert.That(() => scope.Dispose(), Throws.Nothing);
            Assert.That(_assets.Count, Is.Zero);
        });
    }

    //**********************************************************
    //** handles
    //**********************************************************

    [Test]
    public void Get_HandleFromLoadHandle_ReturnsTheLoadedAsset()
    {
        var scope = _assets.CreateScope();

        var handle = scope.LoadHandle<string>("a.txt");

        Assert.Multiple(() =>
        {
            Assert.That(scope.Get(handle), Is.SameAs(scope.Load<string>("a.txt")));
            Assert.That(_assets.Get(handle), Is.EqualTo("A"));
            Assert.That(handle.IsNull, Is.False);
        });
    }

    [Test]
    public void LoadHandle_SameAssetTwice_GivesEqualHandles()
    {
        var scope = _assets.CreateScope();

        Assert.That(scope.LoadHandle<string>("a.txt"), Is.EqualTo(_assets.LoadHandle<string>("a.txt")));
    }

    [Test]
    public void Get_AfterScopeDisposed_ThrowsWithHandle()
    {
        var scope = _assets.CreateScope();
        var handle = scope.LoadHandle<string>("a.txt");
        scope.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(() => _assets.Get(handle), Throws.InvalidOperationException.With.Message.EqualTo(
                $"AssetHandle<String>({handle.Index}v{handle.Generation}) is not loaded: the scope that loaded it " +
                "was disposed, or the AssetManager was cleared."));
            Assert.That(_assets.IsLoaded(handle), Is.False);
        });
    }

    [Test]
    public void Get_StaleHandleAfterSlotReused_ThrowsInsteadOfReturningOtherAsset()
    {
        var scope = _assets.CreateScope();
        var stale = scope.LoadHandle<string>("a.txt");
        scope.Dispose();

        var fresh = _assets.LoadHandle<string>("b.txt");

        Assert.Multiple(() =>
        {
            Assert.That(fresh.Index, Is.EqualTo(stale.Index), "slot reused");
            Assert.That(() => _assets.Get(stale), Throws.InvalidOperationException);
            Assert.That(_assets.Get(fresh), Is.EqualTo("B"));
        });
    }

    [Test]
    public void Get_NullHandleAndUnusedType_Throw()
    {
        _assets.Load<string>("a.txt");

        Assert.Multiple(() =>
        {
            Assert.That(() => _assets.Get(AssetHandle<string>.Null), Throws.InvalidOperationException);
            Assert.That(() => _assets.Get(AssetHandle<Uri>.Null), Throws.InvalidOperationException);
            Assert.That(_assets.IsLoaded(AssetHandle<string>.Null), Is.False);
        });
    }

    [Test]
    public void GetStore_KeptBeforeLoading_SeesLaterLoadsAndReleases()
    {
        var store = _assets.GetStore<string>();
        var scope = _assets.CreateScope();
        var handle = scope.LoadHandle<string>("a.txt");

        Assert.That(store.Get(handle), Is.EqualTo("A"));
        scope.Dispose();
        Assert.Multiple(() =>
        {
            Assert.That(store.IsLoaded(handle), Is.False);
            Assert.That(_assets.GetStore<string>(), Is.SameAs(store));
        });
    }

    [Test]
    public void GetStore_Get_DoesNotAllocate()
    {
        var handle = _assets.LoadHandle<Sprite>("rock.sprite");
        var store = _assets.GetStore<Sprite>();
        store.Get(handle);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            store.Get(handle);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void Get_LoadedHandle_DoesNotAllocate()
    {
        var handle = _assets.LoadHandle<Sprite>("rock.sprite");
        _assets.Get(handle);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            _assets.Get(handle);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void Handle_InEcsComponent_StoresAndReadsBack()
    {
        var handle = _assets.LoadHandle<Sprite>("rock.sprite");
        using var world = new World();

        var entity = world.Create(new SpriteAsset(handle));

        Assert.That(_assets.Get(world.Get<SpriteAsset>(entity).Handle).Data[0, 0].Char, Is.EqualTo('#'));
    }
}
