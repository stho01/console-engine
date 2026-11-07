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
        var elevator = new Elevator();
        loaded.ApplyTo(restored, elevator);
        var miner = loaded.ToMiner();

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        AssertSameMine(restored, played);
        Assert.That(miner.Column, Is.EqualTo(saved.Column));
        Assert.That(miner.Row, Is.EqualTo(saved.Row));
        Assert.That(miner.FacingX, Is.EqualTo(-1));
        Assert.That(miner.Gold, Is.EqualTo(saved.Gold));
        Assert.That(miner.Ladders, Is.EqualTo(saved.Ladders));
        Assert.That(miner.GoldOre, Is.EqualTo(saved.GoldOre));
        Assert.That(miner.GemOre, Is.EqualTo(7));
        Assert.That(miner.HasBlastingKit, Is.True);
        Assert.That(miner.Dynamite, Is.EqualTo(6), "lit sticks are put back");
        Assert.That(miner.LitDynamite, Is.Zero);
        Assert.That(miner.LampTier, Is.EqualTo(2));
        Assert.That(miner.BagTier, Is.EqualTo(3));
        Assert.That(miner.IsMoving, Is.False);
        Assert.That(elevator.Segments, Is.EqualTo(2));
        Assert.That(elevator.IsCarAt(elevator.BottomRow), Is.True);
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

    [TestCase(SaveFile.OldestVersion - 1)]
    [TestCase(SaveFile.Version + 1)]
    public void Read_OtherVersion_ReturnsUnsupported(int version)
    {
        var bytes = Written(SaveData.NewMine(Seed)).ToArray();
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), version);

        Assert.That(SaveFile.Read(new MemoryStream(bytes), out var data), Is.EqualTo(SaveStatus.Unsupported));
        Assert.That(data, Is.Null);
    }

    [Test]
    public void Read_Version1Save_LoadsWithoutALiftOrGems()
    {
        var status = SaveFile.Read(OldSave(1), out var loaded);

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        Assert.That(loaded.Gold, Is.EqualTo(55));
        Assert.That(loaded.Changes, Has.Count.EqualTo(1));
        Assert.That(loaded.Changes[0].Cell.HasLadder, Is.True);
        Assert.That(loaded.ElevatorSegments, Is.Zero);
        Assert.That(loaded.ElevatorCarRow, Is.EqualTo(Elevator.TopRow));
        Assert.That(loaded.GemOre, Is.Zero);
    }

    [Test]
    public void Read_Version2Save_LoadsTheLiftWithoutGems()
    {
        var status = SaveFile.Read(OldSave(2), out var loaded);

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        Assert.That(loaded.ElevatorSegments, Is.EqualTo(3));
        Assert.That(loaded.ElevatorCarRow, Is.EqualTo(Mine.SurfaceRow + 30));
        Assert.That(loaded.GemOre, Is.Zero);
        Assert.That(loaded.Changes, Has.Count.EqualTo(1));
    }

    [Test]
    public void Read_Version3Save_LoadsGemsWithoutDynamite()
    {
        var status = SaveFile.Read(OldSave(3), out var loaded);

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        Assert.That(loaded.GemOre, Is.EqualTo(9));
        Assert.That(loaded.HasBlastingKit, Is.False);
        Assert.That(loaded.Dynamite, Is.Zero);
    }

    [Test]
    public void Read_Version4Save_LoadsDynamiteWithoutUpgrades()
    {
        var status = SaveFile.Read(OldSave(4), out var loaded);

        Assert.That(status, Is.EqualTo(SaveStatus.Loaded));
        Assert.That(loaded.HasBlastingKit, Is.True);
        Assert.That(loaded.Dynamite, Is.EqualTo(5));
        Assert.That(loaded.ShovelTier + loaded.LampTier + loaded.BagTier + loaded.DetectorTier, Is.Zero);
        Assert.That(loaded.Oil, Is.EqualTo(LampOil.TankSize), "saves from before lamp oil start full");
    }

    [Test]
    public void Read_OilOverTheTank_ReturnsCorrupt()
    {
        var data = new SaveData { Seed = Seed, Column = 1, Row = 9, Oil = LampOil.TankSize + 1f };

        Assert.That(SaveFile.Read(Written(data), out _), Is.EqualTo(SaveStatus.Corrupt));
    }

    [Test]
    public void Read_UpgradeTierAboveTheTop_ReturnsCorrupt()
    {
        var data = new SaveData { Seed = Seed, Column = 1, Row = 9, LampTier = Upgrades.MaxTier + 1 };

        Assert.That(SaveFile.Read(Written(data), out _), Is.EqualTo(SaveStatus.Corrupt));
    }

    [Test]
    public void Read_CarOffItsLandings_ReturnsCorrupt()
    {
        var data = new SaveData { Seed = Seed, Column = 1, Row = 9, ElevatorSegments = 2, ElevatorCarRow = 15 };

        Assert.That(SaveFile.Read(Written(data), out _), Is.EqualTo(SaveStatus.Corrupt));
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
        var saver = new GameSaver(_path, Seed, mine, new Elevator());
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
        var saver = new GameSaver(_path, Seed, new Mine(Seed), new Elevator());

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
            GoldOre = 6,
            GemOre = 7,
            HasBlastingKit = true,
            Dynamite = 4,
            LitDynamite = 2,
            ShovelTier = 1,
            LampTier = 2,
            BagTier = 3,
            DetectorTier = 1,
            Oil = 42.5f
        };
        var elevator = new Elevator();
        var rich = new Miner { Gold = 10_000 };
        elevator.TryBuild(mine, ref rich);
        elevator.TryBuild(mine, ref rich);
        elevator.Send(elevator.BottomRow);
        elevator.Update(10f);
        return SaveData.Capture(Seed, miner, mine, elevator, new List<TileChange>());
    }

    /// <summary>A hand-written old save: gold 55, one tile; v2 a 30 m lift, v3 9 gems, v4 the kit and 5 sticks.</summary>
    private static MemoryStream OldSave(int version)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x56534445);
            writer.Write(version);
            foreach (var value in new[] { Seed, 7, Layout.GroundRow })
                writer.Write(value);
            writer.Write((sbyte)1);
            foreach (var value in new[] { 55, 4, 1, 2, 3 })
                writer.Write(value);
            if (version >= 2)
            {
                writer.Write(3);
                writer.Write(Mine.SurfaceRow + 30);
            }

            if (version >= 3)
                writer.Write(9);

            if (version >= 4)
            {
                writer.Write((byte)1);
                writer.Write(5);
            }

            writer.Write(1);
            writer.Write(13);
            writer.Write((byte)7);
            writer.Write((byte)TileType.Air);
            writer.Write((byte)0);
            writer.Write((byte)3);
        }

        stream.Position = 0;
        return stream;
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
        Assert.That(actual.GemOre, Is.EqualTo(expected.GemOre));
        Assert.That(actual.HasBlastingKit, Is.EqualTo(expected.HasBlastingKit));
        Assert.That(actual.Dynamite, Is.EqualTo(expected.Dynamite));
        Assert.That(actual.ShovelTier, Is.EqualTo(expected.ShovelTier));
        Assert.That(actual.LampTier, Is.EqualTo(expected.LampTier));
        Assert.That(actual.BagTier, Is.EqualTo(expected.BagTier));
        Assert.That(actual.DetectorTier, Is.EqualTo(expected.DetectorTier));
        Assert.That(actual.Oil, Is.EqualTo(expected.Oil));
        Assert.That(actual.ElevatorSegments, Is.EqualTo(expected.ElevatorSegments));
        Assert.That(actual.ElevatorCarRow, Is.EqualTo(expected.ElevatorCarRow));
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
