using System;
using System.IO;
using ConsoleEngine.Assets;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public class DirectoryAssetSourceTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "console-engine-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Maps"));
        File.WriteAllText(Path.Combine(_root, "Maps", "map1.txt"), "!name: One");
        File.WriteAllText(Path.Combine(_root, "Maps", "map2.txt"), "!name: Two");
        File.WriteAllText(Path.Combine(_root, "Maps", "map3.txt2"), "not a map");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Open_ExistingFile_ReadsIt()
    {
        var source = new DirectoryAssetSource(_root);

        using var reader = new StreamReader(source.Open("Maps/map1.txt"));

        Assert.That(reader.ReadToEnd(), Is.EqualTo("!name: One"));
    }

    [Test]
    public void Open_MissingFile_ThrowsNotFoundWithFullPath()
    {
        var source = new DirectoryAssetSource(_root);

        var e = Assert.Throws<AssetNotFoundException>(() => source.Open("Maps/map9.txt"));

        Assert.That(e.Location, Is.EqualTo(Path.Combine(_root, "Maps", "map9.txt")));
    }

    [Test]
    public void Open_MissingFolder_ThrowsNotFound()
    {
        var source = new DirectoryAssetSource(_root);

        Assert.That(() => source.Open("Sprites/rover.sprite"), Throws.TypeOf<AssetNotFoundException>());
    }

    [Test]
    public void Exists_FileAndMissingFile_ReportsEach()
    {
        var source = new DirectoryAssetSource(_root);

        Assert.Multiple(() =>
        {
            Assert.That(source.Exists("Maps/map1.txt"), Is.True);
            Assert.That(source.Exists("Maps/map9.txt"), Is.False);
            Assert.That(source.Exists(AssetPath.Root), Is.False);
        });
    }

    [Test]
    public void Enumerate_Folder_ReturnsOnlyExactExtensionSorted()
    {
        var source = new DirectoryAssetSource(_root);

        Assert.That(source.Enumerate("Maps", "txt"), Is.EqualTo(new AssetPath[] { "Maps/map1.txt", "Maps/map2.txt" }));
    }

    [Test]
    public void Enumerate_MissingFolder_IsEmpty()
    {
        var source = new DirectoryAssetSource(_root);

        Assert.That(source.Enumerate("Sprites", "sprite"), Is.Empty);
    }

    [Test]
    public void AssetManager_OverDirectory_LoadsLines()
    {
        var assets = new AssetManager(new DirectoryAssetSource(_root));

        Assert.That(assets.Load<string[]>("Maps/map2.txt"), Is.EqualTo(new[] { "!name: Two" }));
    }

    [Test]
    public void CreateDefault_RootIsAssetsNextToExe()
    {
        var source = DirectoryAssetSource.CreateDefault();

        Assert.That(source.RootDirectory, Is.EqualTo(Path.Combine(AppContext.BaseDirectory, "Assets")));
    }
}
