using System;
using System.Collections.Generic;
using System.IO;
using ConsoleEngine.Assets;
using ConsoleEngine.Assets.Loaders;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public class AssetManagerTests
{
    //**********************************************************
    //** test loaders
    //**********************************************************

    /// <summary>A test format: the first line is a name, every other line names another <c>.node</c> to load.</summary>
    private sealed class Node
    {
        public string Name { get; init; }
        public List<Node> Children { get; } = new();
    }

    private sealed class NodeLoader : IAssetLoader<Node>
    {
        public int Loads { get; private set; }
        public IReadOnlyList<string> Extensions { get; } = [".NODE"];

        public Node Load(Stream stream, AssetLoadContext context)
        {
            Loads++;
            using var reader = new StreamReader(stream);
            var node = new Node { Name = reader.ReadLine() };
            while (reader.ReadLine() is { } child)
                node.Children.Add(context.Load<Node>(child));

            return node;
        }
    }

    private sealed class FailingLoader : IAssetLoader<Node>
    {
        public IReadOnlyList<string> Extensions { get; } = ["bad"];
        public Node Load(Stream stream, AssetLoadContext context) => throw new FormatException("Line 3 is broken.");
    }

    private sealed class NullLoader : IAssetLoader<Node>
    {
        public IReadOnlyList<string> Extensions { get; } = ["null"];
        public Node Load(Stream stream, AssetLoadContext context) => null;
    }

    private sealed class NoExtensionsLoader : IAssetLoader<Node>
    {
        public IReadOnlyList<string> Extensions { get; } = [];
        public Node Load(Stream stream, AssetLoadContext context) => new();
    }

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Load_TextFile_ReturnsContent()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("story.txt", "Once upon a time"));

        Assert.That(assets.Load<string>("story.txt"), Is.EqualTo("Once upon a time"));
    }

    [Test]
    public void Load_LinesFromTextFile_SplitsOnEveryLineEnding()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("Maps/map1.txt", "a\r\nb\nc\n"));

        Assert.That(assets.Load<string[]>("Maps/map1.txt"), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Load_SamePathAndTypeTwice_ReturnsCachedInstance()
    {
        var loader = new NodeLoader();
        var assets = new AssetManager(new MemoryAssetSource().Add("a.node", "A"));
        assets.RegisterLoader(loader);

        var first = assets.Load<Node>("a.node");
        var second = assets.Load<Node>(@".\a.node");

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(loader.Loads, Is.EqualTo(1));
            Assert.That(assets.IsLoaded<Node>("a.node"), Is.True);
        });
    }

    [Test]
    public void Load_SameFileAsTwoTypes_CachesEachType()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.txt", "x\ny"));

        Assert.Multiple(() =>
        {
            Assert.That(assets.Load<string>("a.txt"), Is.EqualTo("x\ny"));
            Assert.That(assets.Load<string[]>("a.txt"), Is.EqualTo(new[] { "x", "y" }));
            Assert.That(assets.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void Load_CustomLoader_LoadsDependenciesRelativeToAsset()
    {
        var source = new MemoryAssetSource()
            .Add("Maps/map1.node", "Map\n../Tiles/rock.node")
            .Add("Tiles/rock.node", "Rock");
        var assets = new AssetManager(source);
        assets.RegisterLoader(new NodeLoader());

        var map = assets.Load<Node>("Maps/map1.node");

        Assert.Multiple(() =>
        {
            Assert.That(map.Children[0].Name, Is.EqualTo("Rock"));
            Assert.That(assets.Load<Node>("Tiles/rock.node"), Is.SameAs(map.Children[0]));
        });
    }

    [Test]
    public void Load_ExtensionInOtherCase_FindsLoader()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("A.Node", "A"));
        assets.RegisterLoader(new NodeLoader());

        Assert.That(assets.Load<Node>("A.Node").Name, Is.EqualTo("A"));
    }

    [Test]
    public void Load_MissingAsset_ThrowsNotFoundWithLocation()
    {
        var assets = new AssetManager(new MemoryAssetSource());

        var e = Assert.Throws<AssetNotFoundException>(() => assets.Load<string>("Maps/map9.txt"));

        Assert.Multiple(() =>
        {
            Assert.That(e.Path.Value, Is.EqualTo("Maps/map9.txt"));
            Assert.That(e.Location, Is.EqualTo("memory:Maps/map9.txt"));
            Assert.That(e.Message, Is.EqualTo("Asset 'Maps/map9.txt' not found at 'memory:Maps/map9.txt'."));
        });
    }

    [Test]
    public void Load_NoLoaderForTypeAndExtension_ThrowsAndListsRegisteredExtensions()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.json", "{}"));

        var e = Assert.Throws<AssetLoaderNotFoundException>(() => assets.Load<string>("a.json"));

        Assert.Multiple(() =>
        {
            Assert.That(e.AssetType, Is.EqualTo(typeof(string)));
            Assert.That(e.Message, Is.EqualTo(
                "No loader for String assets with extension '.json' (asset 'a.json'). " +
                "Registered extensions for String: .txt. Register one with AssetManager.RegisterLoader."));
        });
    }

    [Test]
    public void Load_LoaderThrows_WrapsWithPathAndLoaderAndIsNotCached()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.bad", "x"));
        assets.RegisterLoader(new FailingLoader());

        var e = Assert.Throws<AssetLoadException>(() => assets.Load<Node>("a.bad"));

        Assert.Multiple(() =>
        {
            Assert.That(e.Message, Is.EqualTo(
                "Failed to load asset 'a.bad' as Node with FailingLoader: Line 3 is broken."));
            Assert.That(e.InnerException, Is.TypeOf<FormatException>());
            Assert.That(assets.IsLoaded<Node>("a.bad"), Is.False);
        });
    }

    [Test]
    public void Load_LoaderReturnsNull_Throws()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.null", "x"));
        assets.RegisterLoader(new NullLoader());

        Assert.That(() => assets.Load<Node>("a.null"),
            Throws.TypeOf<AssetLoadException>().With.Message.EqualTo("NullLoader returned null for asset 'a.null'."));
    }

    [Test]
    public void Load_MissingDependency_ThrowsLoadExceptionWithNotFoundInside()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.node", "A\nb.node"));
        assets.RegisterLoader(new NodeLoader());

        var e = Assert.Throws<AssetLoadException>(() => assets.Load<Node>("a.node"));

        Assert.That(e.InnerException, Is.TypeOf<AssetNotFoundException>());
    }

    [Test]
    public void Load_CircularDependency_ThrowsWithChain()
    {
        var source = new MemoryAssetSource()
            .Add("a.node", "A\nb.node")
            .Add("b.node", "B\na.node");
        var assets = new AssetManager(source);
        assets.RegisterLoader(new NodeLoader());

        var e = Assert.Throws<AssetLoadException>(() => assets.Load<Node>("a.node"));
        var circular = e.InnerException?.InnerException;

        Assert.Multiple(() =>
        {
            Assert.That(circular?.Message,
                Is.EqualTo("Circular asset dependency: a.node (Node) -> b.node (Node) -> a.node (Node)."));
            Assert.That(assets.Count, Is.Zero);
        });
    }

    [Test]
    public void Load_AfterFailedLoad_CanLoadAgain()
    {
        var source = new MemoryAssetSource().Add("a.node", "A\nb.node");
        var assets = new AssetManager(source);
        assets.RegisterLoader(new NodeLoader());
        Assert.Throws<AssetLoadException>(() => assets.Load<Node>("a.node"));

        source.Add("b.node", "B");

        Assert.That(assets.Load<Node>("a.node").Children[0].Name, Is.EqualTo("B"));
    }

    [Test]
    public void TryLoad_MissingAsset_ReturnsFalse()
    {
        var assets = new AssetManager(new MemoryAssetSource());

        var found = assets.TryLoad<string>("missing.txt", out var text);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(text, Is.Null);
        });
    }

    [Test]
    public void TryLoad_ExistingAsset_ReturnsTrueAndCaches()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.txt", "x"));

        var found = assets.TryLoad<string>("a.txt", out var text);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(text, Is.EqualTo("x"));
            Assert.That(assets.IsLoaded<string>("a.txt"), Is.True);
        });
    }

    [Test]
    public void TryLoad_NoLoader_StillThrows()
    {
        var assets = new AssetManager(new MemoryAssetSource());

        Assert.That(() => assets.TryLoad<Node>("missing.node", out _), Throws.TypeOf<AssetLoaderNotFoundException>());
    }

    [Test]
    public void TryLoad_LoaderFails_StillThrows()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.bad", "x"));
        assets.RegisterLoader(new FailingLoader());

        Assert.That(() => assets.TryLoad<Node>("a.bad", out _), Throws.TypeOf<AssetLoadException>());
    }

    [Test]
    public void RegisterLoader_SameTypeAndExtensionTwice_ThrowsAndKeepsFirst()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("a.txt", "x"));

        Assert.Multiple(() =>
        {
            Assert.That(() => assets.RegisterLoader(new TextLoader("md", "TXT")),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "A loader for String assets with extension '.txt' is already registered (TextLoader)."));
            Assert.That(() => assets.Load<string>("a.md"), Throws.TypeOf<AssetLoaderNotFoundException>());
        });
    }

    [Test]
    public void RegisterLoader_ExtraExtension_LoadsThatExtension()
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("Story/intro.story", "Hello"));
        assets.RegisterLoader(new TextLoader("story"));

        Assert.That(assets.Load<string>("Story/intro.story"), Is.EqualTo("Hello"));
    }

    [Test]
    public void RegisterLoader_NoExtensions_Throws()
    {
        var assets = new AssetManager(new MemoryAssetSource());

        Assert.That(() => assets.RegisterLoader(new NoExtensionsLoader()), Throws.ArgumentException);
    }

    [Test]
    public void Clear_AfterLoad_ReadsSourceAgain()
    {
        var source = new MemoryAssetSource().Add("a.txt", "old");
        var assets = new AssetManager(source);
        assets.Load<string>("a.txt");
        source.Add("a.txt", "new");

        assets.Clear();

        Assert.That(assets.Load<string>("a.txt"), Is.EqualTo("new"));
    }

    [Test]
    public void Enumerate_Directory_ReturnsMatchingFilesSorted()
    {
        var source = new MemoryAssetSource()
            .Add("Maps/map2.txt", "")
            .Add("Maps/map1.txt", "")
            .Add("Maps/notes.md", "")
            .Add("Maps/Old/map0.txt", "");
        var assets = new AssetManager(source);

        var maps = assets.Enumerate("Maps", ".TXT");

        Assert.That(maps, Is.EqualTo(new AssetPath[] { "Maps/map1.txt", "Maps/map2.txt" }));
    }
}
