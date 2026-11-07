using System;
using System.Collections.Generic;
using System.IO;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Saving;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class SaveFileTests
{
    private const int Seed = 42;

    private string _directory;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "EternalDiggerTests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "save.dat");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void Read_WrittenSave_ReturnsTheSameData()
    {
        var saved = PlayedSave(out _);

        var status = SaveFile.Read(Written(saved), out var loaded);

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        AssertSame(loaded, saved);
    }

    [Test]
    public void Load_SavedMine_RestoresTheMineAndMiner()
    {
        var saved = PlayedSave(out var played);
        SaveFile.Save(_path, saved);

        var status = SaveFile.Load(_path, out var loaded);
        var restored = new Mine(loaded.Seed);
        loaded.ApplyTo(restored);
        var miner = loaded.ToMiner();

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        AssertSameMine(restored, played);
        Assert.That(miner.Column, Is.EqualTo(saved.Column));
        Assert.That(miner.Row, Is.EqualTo(saved.Row));
        Assert.That(miner.FacingX, Is.EqualTo(-1));
        Assert.That(miner.Gold, Is.EqualTo(saved.Gold));
        Assert.That(miner.Ladders, Is.EqualTo(saved.Ladders));
        Assert.That(miner.GoldOre, Is.EqualTo(saved.GoldOre));
        Assert.That(miner.IsMoving, Is.False);
    }

    [Test]
    public void Save_OverAnOldSave_ReplacesItAndLeavesNoTempFile()
    {
        SaveFile.Save(_path, SaveData.NewMine(1));
        SaveFile.Save(_path, SaveData.NewMine(2));

        SaveFile.Load(_path, out var loaded);

        Assert.That(loaded.Seed, Is.EqualTo(2));
        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
    }

    [Test]
    public void Load_NoFile_ReturnsMissing()
    {
        Assert.That(SaveFile.Load(_path, out var data), Is.EqualTo(SaveStatus.Missing));
        Assert.That(data, Is.Null);
    }

    [Test]
    public void Load_GarbageFile_ReturnsCorruptAndMovesItAside()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "not a save");

        var status = SaveFile.Load(_path, out var data);

        Assert.That(status, Is.EqualTo(SaveStatus.Corrupt));
        Assert.That(data, Is.Null);
        Assert.That(File.Exists(_path), Is.False);
        Assert.That(File.ReadAllText(SaveFile.BadPath(_path)), Is.EqualTo("not a save"));
    }

    [Test]
    public void Read_EveryTruncation_ReturnsCorrupt()
    {
        var bytes = Written(PlayedSave(out _)).ToArray();

        for (var length = 0; length < bytes.Length; length++)
        {
            var status = SaveFile.Read(new MemoryStream(bytes, 0, length), out var data);
            Assert.That(status, Is.EqualTo(SaveStatus.Corrupt), $"length {length}");
            Assert.That(data, Is.Null);
        }
    }

    [Test]
    public void Read_TrailingBytes_ReturnsCorrupt()
    {
        var stream = Written(SaveData.NewMine(Seed));
        stream.Position = stream.Length;
        stream.WriteByte(0);
        stream.Position = 0;

        Assert.That(SaveFile.Read(stream, out _), Is.EqualTo(SaveStatus.Corrupt));
    }

    [TestCase(0)]
    [TestCase(SaveFile.Version + 1)]
    public void Read_OtherVersion_ReturnsUnsupported(int version)
    {
        var bytes = Written(SaveData.NewMine(Seed)).ToArray();
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), version);

        Assert.That(SaveFile.Read(new MemoryStream(bytes), out var data), Is.EqualTo(SaveStatus.Unsupported));
        Assert.That(data, Is.Null);
    }

    [Test]
    public void Load_OtherVersion_MovesItAside()
    {
        var bytes = Written(SaveData.NewMine(Seed)).ToArray();
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), SaveFile.Version + 1);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(_path, bytes);

        Assert.That(SaveFile.Load(_path, out _), Is.EqualTo(SaveStatus.Unsupported));
        Assert.That(File.Exists(SaveFile.BadPath(_path)), Is.True);
    }

    [Test]
    public void Read_OutOfRangeValues_ReturnsCorrupt()
    {
        var badColumn = new SaveData { Seed = Seed, Column = Mine.Columns, Row = Layout.GroundRow };
        var negativeGold = new SaveData { Seed = Seed, Column = 1, Row = Layout.GroundRow, Gold = -1 };
        var badTile = SaveData.NewMine(Seed);
        badTile.Changes.Add(new TileChange(3, 20, new TileCell { Type = (TileType)200 }));

        Assert.That(SaveFile.Read(Written(badColumn), out _), Is.EqualTo(SaveStatus.Corrupt));
        Assert.That(SaveFile.Read(Written(negativeGold), out _), Is.EqualTo(SaveStatus.Corrupt));
        Assert.That(SaveFile.Read(Written(badTile), out _), Is.EqualTo(SaveStatus.Corrupt));
    }

    [Test]
    public void NewMine_StartState_MatchesTheShopAndLayout()
    {
        var miner = SaveData.NewMine(Seed).ToMiner();

        Assert.That(miner.Column, Is.EqualTo(Layout.SpawnColumn));
        Assert.That(miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(miner.Gold, Is.EqualTo(Shop.StartGold));
        Assert.That(miner.Ladders, Is.EqualTo(Shop.StartLadders));
        Assert.That(miner.OreCount, Is.Zero);
    }

    [Test]
    public void Save_GameSaver_WritesTheMinerAndCountsTheSave()
    {
        var mine = new Mine(Seed);
        mine.Hit(Mine.ShaftColumn, Mine.SurfaceRow + 2);
        var saver = new GameSaver(_path, Seed, mine);
        var miner = new Miner { Column = Mine.ShaftColumn, Row = Mine.SurfaceRow + 1, FacingX = 1, Gold = 70 };

        var written = saver.Save(miner);
        SaveFile.Load(_path, out var loaded);

        Assert.That(written, Is.True);
        Assert.That(saver.Saves, Is.EqualTo(1));
        Assert.That(saver.LastSaveFailed, Is.False);
        Assert.That(loaded.Gold, Is.EqualTo(70));
        Assert.That(loaded.Changes, Has.Some.Matches<TileChange>(c => c.Cell.Damage == 1));
    }

    [Test]
    public void Save_GameSaverCannotWrite_ReportsFailureWithoutThrowing()
    {
        // A directory where the file should be: the final move fails.
        Directory.CreateDirectory(_path);
        var saver = new GameSaver(_path, Seed, new Mine(Seed));

        var written = saver.Save(new Miner { FacingX = 1 });

        Assert.That(written, Is.False);
        Assert.That(saver.LastSaveFailed, Is.True);
        Assert.That(saver.Saves, Is.EqualTo(1));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>A mine after some play: a dug tile, a half-dug tile, a ladder, seen tiles, and a miner with loot.</summary>
    private static SaveData PlayedSave(out Mine mine)
    {
        mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 40;
        mine.At(5, row) = new TileCell { Type = TileType.Air, Seen = true };
        mine.TryPlaceLadder(5, row);
        mine.At(6, row) = new TileCell { Type = TileType.Gold, Damage = 3, Seen = true };
        mine.At(7, row + 70).Seen = true;
        var miner = new Miner
        {
            Column = 5,
            Row = row,
            FacingX = -1,
            Gold = 1234,
            Ladders = 3,
            CopperOre = 4,
            SilverOre = 5,
            GoldOre = 6
        };
        return SaveData.Capture(Seed, miner, mine, new List<TileChange>());
    }

    private static MemoryStream Written(SaveData data)
    {
        var stream = new MemoryStream();
        SaveFile.Write(stream, data);
        stream.Position = 0;
        return stream;
    }

    private static void AssertSame(SaveData actual, SaveData expected)
    {
        Assert.That(actual.Seed, Is.EqualTo(expected.Seed));
        Assert.That(actual.Column, Is.EqualTo(expected.Column));
        Assert.That(actual.Row, Is.EqualTo(expected.Row));
        Assert.That(actual.FacingX, Is.EqualTo(expected.FacingX));
        Assert.That(actual.Gold, Is.EqualTo(expected.Gold));
        Assert.That(actual.Ladders, Is.EqualTo(expected.Ladders));
        Assert.That(actual.CopperOre, Is.EqualTo(expected.CopperOre));
        Assert.That(actual.SilverOre, Is.EqualTo(expected.SilverOre));
        Assert.That(actual.GoldOre, Is.EqualTo(expected.GoldOre));
        Assert.That(actual.Changes, Is.EqualTo(expected.Changes));
    }

    private static void AssertSameMine(Mine actual, Mine expected)
    {
        Assert.That(actual.GeneratedRows, Is.EqualTo(expected.GeneratedRows));
        for (var row = 0; row < expected.GeneratedRows; row++)
        for (var column = 0; column < Mine.Columns; column++)
            Assert.That(actual.At(column, row), Is.EqualTo(expected.At(column, row)), $"tile {column}, {row}");
    }
}
