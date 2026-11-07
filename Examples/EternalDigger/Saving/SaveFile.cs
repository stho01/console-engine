using System;
using System.Collections.Generic;
using System.IO;
using EternalDigger.Mining;

namespace EternalDigger.Saving;

/// <summary>What <see cref="SaveFile.Load"/> found.</summary>
public enum SaveStatus : byte
{
    /// <summary>No save file: start a new mine.</summary>
    Missing,
    Loaded,
    /// <summary>The file is damaged or isn't a save. It is moved aside to <see cref="SaveFile.BadPath"/>.</summary>
    Corrupt,
    /// <summary>A save-format version this build can't read. Also moved aside to <see cref="SaveFile.BadPath"/>.</summary>
    Unsupported
}

/// <summary>
/// The save file: one slot, binary, versioned. Layout (little-endian): magic <c>EDSV</c>, format version, seed,
/// miner column, row and facing, gold, ladders, copper, silver and gold ore, then the changed tiles (count, then row,
/// column, type, damage, flags each). Writing goes to a temp file first, so a crash never leaves half a save.
/// </summary>
public static class SaveFile
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The save-format version written. Bump it whenever the layout changes, and keep reading older ones.</summary>
    public const int Version = 1;

    /// <summary>"EDSV" as a little-endian int.</summary>
    private const int Magic = 0x56534445;

    /// <summary>Deepest row a save may hold. Guards against a damaged file growing the mine to gigabytes.</summary>
    private const int MaxRow = 100_000;

    private const byte LadderFlag = 1;
    private const byte SeenFlag = 2;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>
    /// <c>EternalDigger/save.dat</c> in the user's local app-data folder (<c>%LOCALAPPDATA%</c> on Windows,
    /// <c>~/Library/Application Support</c> on macOS, <c>~/.local/share</c> on Linux).
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EternalDigger", "save.dat");

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Where an unreadable save is moved, so the next save doesn't destroy it.</summary>
    public static string BadPath(string path) => path + ".bad";

    /// <summary>Writes the save to <paramref name="path"/> through a temp file. Throws on I/O errors.</summary>
    public static void Save(string path, SaveData data)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        using (var stream = File.Create(temp))
            Write(stream, data);

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Reads the save at <paramref name="path"/>. Never throws: a missing file is <see cref="SaveStatus.Missing"/>; a
    /// damaged or unknown-version file is moved to <see cref="BadPath"/> and reported.
    /// </summary>
    public static SaveStatus Load(string path, out SaveData data)
    {
        data = null;
        SaveStatus status;
        try
        {
            if (!File.Exists(path))
                return SaveStatus.Missing;

            using (var stream = File.OpenRead(path))
                status = Read(stream, out data);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status = SaveStatus.Corrupt;
        }

        if (status != SaveStatus.Loaded)
            MoveAside(path);

        return status;
    }

    public static void Write(Stream stream, SaveData data)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(data.Seed);
        writer.Write(data.Column);
        writer.Write(data.Row);
        writer.Write((sbyte)data.FacingX);
        writer.Write(data.Gold);
        writer.Write(data.Ladders);
        writer.Write(data.CopperOre);
        writer.Write(data.SilverOre);
        writer.Write(data.GoldOre);

        writer.Write(data.Changes.Count);
        for (var i = 0; i < data.Changes.Count; i++)
        {
            var change = data.Changes[i];
            writer.Write(change.Row);
            writer.Write((byte)change.Column);
            writer.Write((byte)change.Cell.Type);
            writer.Write(change.Cell.Damage);
            writer.Write((byte)((change.Cell.HasLadder ? LadderFlag : 0) | (change.Cell.Seen ? SeenFlag : 0)));
        }
    }

    /// <summary>Reads a save from a stream. Never throws on bad data; <paramref name="data"/> is null unless loaded.
    /// </summary>
    public static SaveStatus Read(Stream stream, out SaveData data)
    {
        data = null;
        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != Magic)
                return SaveStatus.Corrupt;

            var version = reader.ReadInt32();
            if (version != Version)
                return SaveStatus.Unsupported;

            return ReadVersion1(reader, out data);
        }
        catch (EndOfStreamException)
        {
            data = null;
            return SaveStatus.Corrupt;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static SaveStatus ReadVersion1(BinaryReader reader, out SaveData data)
    {
        data = null;
        var seed = reader.ReadInt32();
        var column = reader.ReadInt32();
        var row = reader.ReadInt32();
        var facing = reader.ReadSByte();
        var gold = reader.ReadInt32();
        var ladders = reader.ReadInt32();
        var copper = reader.ReadInt32();
        var silver = reader.ReadInt32();
        var goldOre = reader.ReadInt32();
        if ((uint)column >= Mine.Columns || (uint)row > MaxRow || facing is not (-1 or 1) ||
            (gold | ladders | copper | silver | goldOre) < 0)
            return SaveStatus.Corrupt;

        var count = reader.ReadInt32();
        if ((uint)count > (MaxRow + 1L) * Mine.Columns)
            return SaveStatus.Corrupt;

        var changes = new List<TileChange>(Math.Min(count, 1 << 16));
        for (var i = 0; i < count; i++)
        {
            var changeRow = reader.ReadInt32();
            var changeColumn = reader.ReadByte();
            var type = (TileType)reader.ReadByte();
            var damage = reader.ReadByte();
            var flags = reader.ReadByte();
            if ((uint)changeRow > MaxRow || changeColumn >= Mine.Columns || !Enum.IsDefined(type) ||
                (flags & ~(LadderFlag | SeenFlag)) != 0)
                return SaveStatus.Corrupt;

            changes.Add(new TileChange(changeColumn, changeRow, new TileCell
            {
                Type = type,
                Damage = damage,
                HasLadder = (flags & LadderFlag) != 0,
                Seen = (flags & SeenFlag) != 0
            }));
        }

        if (reader.BaseStream.CanSeek && reader.BaseStream.Position != reader.BaseStream.Length)
            return SaveStatus.Corrupt;

        data = new SaveData
        {
            Seed = seed,
            Column = column,
            Row = row,
            FacingX = facing,
            Gold = gold,
            Ladders = ladders,
            CopperOre = copper,
            SilverOre = silver,
            GoldOre = goldOre,
            Changes = changes
        };
        return SaveStatus.Loaded;
    }

    private static void MoveAside(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, BadPath(path), overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Can't move it: the next save overwrites it instead.
        }
    }
}
