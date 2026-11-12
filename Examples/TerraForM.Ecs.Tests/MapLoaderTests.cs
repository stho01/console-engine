using System;
using ConsoleEngine.Assets;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Tests;

public class MapLoaderTests
{
    //**********************************************************
    //** helpers
    //**********************************************************

    private static AssetManager Assets(string mapText)
    {
        var assets = new AssetManager(new MemoryAssetSource().Add("Maps/test.txt", mapText));
        assets.RegisterLoader(new MapLoader());
        return assets;
    }

    private static Map Load(string mapText) => Assets(mapText).Load<Map>("Maps/test.txt");

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Load_Header_ReadsNamePowerAndSequences()
    {
        var map = Load("!name: Test Map\n!power: 3500\n!sequences:30\n!width: 2\n!height: 1\n..");

        Assert.Multiple(() =>
        {
            Assert.That(map.Name, Is.EqualTo("Test Map"));
            Assert.That(map.MaxPower, Is.EqualTo(3500));
            Assert.That(map.Sequences, Is.EqualTo(30));
        });
    }

    [Test]
    public void Load_Grid_MapsEveryCharacterToTileKind()
    {
        var map = Load("!width: 4\n!height: 2\nSFHP\nCB.x\n");

        Assert.Multiple(() =>
        {
            Assert.That(map.Tiles.GetLength(0), Is.EqualTo(4));
            Assert.That(map.Tiles.GetLength(1), Is.EqualTo(2));
            Assert.That(map.Tiles[0, 0], Is.EqualTo(TileKind.StartingPoint));
            Assert.That(map.Tiles[1, 0], Is.EqualTo(TileKind.FinishPoint));
            Assert.That(map.Tiles[2, 0], Is.EqualTo(TileKind.Rock));
            Assert.That(map.Tiles[3, 0], Is.EqualTo(TileKind.PlantSpot));
            Assert.That(map.Tiles[0, 1], Is.EqualTo(TileKind.Crater));
            Assert.That(map.Tiles[1, 1], Is.EqualTo(TileKind.BonusPoint));
            Assert.That(map.Tiles[2, 1], Is.EqualTo(TileKind.None));
            Assert.That(map.Tiles[3, 1], Is.EqualTo(TileKind.None));
        });
    }

    [Test]
    public void Load_StartingPoint_IsInWorldUnits()
    {
        var map = Load("!width: 3\n!height: 2\n...\n..S");

        Assert.That(map.StartPosition, Is.EqualTo(new Vector2(2, 1) * MapLoader.TileSize));
    }

    [Test]
    public void Load_NoStartingPoint_StartPositionIsNull()
    {
        Assert.That(Load("!width: 1\n!height: 1\n.").StartPosition, Is.Null);
    }

    [Test]
    public void Load_WindowsLineEndings_ReadLikeUnix()
    {
        var map = Load("!name: CRLF\r\n!width: 2\r\n!height: 1\r\nSF\r\n");

        Assert.Multiple(() =>
        {
            Assert.That(map.Name, Is.EqualTo("CRLF"));
            Assert.That(map.Tiles[1, 0], Is.EqualTo(TileKind.FinishPoint));
        });
    }

    [Test]
    public void Load_RowsBeforeWidthAndHeight_AreDroppedButStillCountAsRows()
    {
        // Quirks kept from the original WorldLoader: rows are only stored once the grid exists, but every row moves
        // the row counter, so the first stored row lands at y = 1.
        var map = Load("H.\n!width: 2\n!height: 2\n.F");

        Assert.Multiple(() =>
        {
            Assert.That(map.Tiles[0, 0], Is.EqualTo(TileKind.None));
            Assert.That(map.Tiles[1, 0], Is.EqualTo(TileKind.None));
            Assert.That(map.Tiles[1, 1], Is.EqualTo(TileKind.FinishPoint));
        });
    }

    [Test]
    public void Load_SameMapTwice_ReturnsCachedMap()
    {
        var assets = Assets("!width: 1\n!height: 1\nS");

        Assert.That(assets.Load<Map>("Maps/test.txt"), Is.SameAs(assets.Load<Map>("Maps/test.txt")));
    }

    [Test]
    public void Load_PowerNotANumber_ThrowsAssetLoadExceptionWithFormatException()
    {
        var e = Assert.Throws<AssetLoadException>(() => Load("!power: lots\n"));

        Assert.Multiple(() =>
        {
            Assert.That(e.Path.Value, Is.EqualTo("Maps/test.txt"));
            Assert.That(e.InnerException, Is.TypeOf<FormatException>());
        });
    }

    [Test]
    public void Load_MissingMap_ThrowsNotFound()
    {
        var assets = Assets("!width: 1\n!height: 1\n.");

        Assert.That(() => assets.Load<Map>("Maps/map9.txt"), Throws.TypeOf<AssetNotFoundException>());
    }

    [Test]
    public void ShippedMaps_FromExeFolder_AllLoadWithNameAndStart()
    {
        // The maps are copied next to this test assembly through the TerraForM.Ecs project reference, so this reads
        // them exactly where the game does: Assets/ next to the exe.
        var assets = new AssetManager(DirectoryAssetSource.CreateDefault());
        assets.RegisterLoader(new MapLoader());

        var paths = assets.Enumerate("Maps", "txt");

        Assert.That(paths, Has.Count.EqualTo(5));
        foreach (var path in paths)
        {
            var map = assets.Load<Map>(path);
            Assert.Multiple(() =>
            {
                Assert.That(map.Name, Is.Not.Empty, path.Value);
                Assert.That(map.StartPosition, Is.Not.Null, path.Value);
                Assert.That(map.Tiles.GetLength(0), Is.EqualTo(95), path.Value);
                Assert.That(map.Tiles.GetLength(1), Is.EqualTo(17), path.Value);
            });
        }
    }
}
