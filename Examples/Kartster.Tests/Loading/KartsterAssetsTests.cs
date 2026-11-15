using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Assets;
using Kartster.Loading;
using NUnit.Framework;

namespace Kartster.Tests.Loading;

public class KartsterAssetsTests
{
    private string _root;

    [SetUp]
    public void CreateFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "kartster-assets-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "MyKarts", "box"));
        File.WriteAllText(Path.Combine(_root, "MyKarts", "box", "body.obj"), "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
    }

    [TearDown]
    public void DeleteFolder() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Load_ObjFile_GivesGeometryThatAppendsPastWhatIsThere()
    {
        var assets = KartsterAssets.Register(new AssetManager(new MemoryAssetSource()
            .Add("Karts/box/body.obj", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n")));
        var positions = new List<Vector3> { Vector3.One };
        var indices = new List<int>();

        var geometry = assets.Load<ObjGeometry>("Karts/box/body.obj");
        geometry.AppendTo(positions, indices);

        Assert.Multiple(() =>
        {
            Assert.That(geometry.PositionCount, Is.EqualTo(3));
            Assert.That(geometry.TriangleCount, Is.EqualTo(1));
            Assert.That(positions, Has.Count.EqualTo(4));
            Assert.That(indices, Is.EqualTo(new[] { 1, 2, 3 }));
        });
    }

    [Test]
    public void Load_BrokenObjFile_NamesTheAssetAndTheLine()
    {
        var assets = KartsterAssets.Register(new AssetManager(new MemoryAssetSource()
            .Add("Karts/box/body.obj", "v 0 0 0\nf 1 1\n")));

        var exception = Assert.Throws<AssetLoadException>(() => assets.Load<ObjGeometry>("Karts/box/body.obj"));

        Assert.That(exception.InnerException, Is.InstanceOf<InvalidDataException>());
        Assert.That(exception.InnerException.Message, Does.StartWith("Karts/box/body.obj:2: "));
    }

    [Test]
    public void OpenFolder_FolderOutsideAssets_IsReadThroughItsParent()
    {
        var (assets, folder) = KartsterAssets.OpenFolder(Path.Combine(_root, "MyKarts") + Path.DirectorySeparatorChar);

        Assert.Multiple(() =>
        {
            Assert.That(folder, Is.EqualTo((AssetPath)"MyKarts"));
            Assert.That(assets.EnumerateDirectories(folder), Is.EqualTo(new AssetPath[] { "MyKarts/box" }));
            Assert.That(KartModel.Load(assets, "MyKarts/box", Vector3.One).TriangleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Load_KartModel_LeavesNothingInTheCache()
    {
        var (assets, _) = KartsterAssets.OpenFolder(Path.Combine(_root, "MyKarts"));

        KartModel.Load(assets, "MyKarts/box", Vector3.One);

        Assert.That(assets.Count, Is.Zero);
    }
}
