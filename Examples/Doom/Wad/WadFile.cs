using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Wad;

/// <summary>A lump directory entry: its name (upper case, at most 8 characters) and where its bytes are.</summary>
public readonly record struct WadLump(string Name, int Offset, int Size);

/// <summary>
/// A WAD file held in memory: a 12 byte header (<c>IWAD</c> or <c>PWAD</c>, lump count, directory offset) and a
/// directory of 16 byte entries (offset, size, 8 character name). Lumps are read as spans into the file's bytes.
/// When names repeat, <see cref="IndexOf(string)"/> finds the last one, like a PWAD overriding an IWAD.
/// </summary>
public sealed class WadFile
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int HeaderSize = 12;
    private const int DirectoryEntrySize = 16;
    private const int NameLength = 8;

    private readonly byte[] _data;
    private readonly WadLump[] _lumps;
    private readonly Dictionary<string, int> _lastIndexByName = new(StringComparer.Ordinal);

    //**********************************************************
    //** ctor
    //**********************************************************

    private WadFile(byte[] data, WadLump[] lumps)
    {
        _data = data;
        _lumps = lumps;
        for (var i = 0; i < lumps.Length; i++)
            _lastIndexByName[lumps[i].Name] = i;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public IReadOnlyList<WadLump> Lumps => _lumps;
    public int Count => _lumps.Length;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static WadFile Load(string path) => Parse(File.ReadAllBytes(path));

    /// <exception cref="InvalidDataException">The bytes are not a WAD file, or its directory is broken.</exception>
    public static WadFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < HeaderSize)
            throw new InvalidDataException(DoomTexts.WadTooShort);

        var id = Encoding.ASCII.GetString(data, 0, 4);
        if (id != "IWAD" && id != "PWAD")
            throw new InvalidDataException(string.Format(DoomTexts.WadBadId, id));

        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
        var directory = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8));
        if (count < 0 || directory < 0 || directory + (long)count * DirectoryEntrySize > data.Length)
            throw new InvalidDataException(DoomTexts.WadBadDirectory);

        var lumps = new WadLump[count];
        for (var i = 0; i < count; i++)
        {
            var entry = data.AsSpan(directory + i * DirectoryEntrySize, DirectoryEntrySize);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(entry);
            var size = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
            var name = ReadName(entry.Slice(8, NameLength));
            if (offset < 0 || size < 0 || (size > 0 && offset + (long)size > data.Length))
                throw new InvalidDataException(string.Format(DoomTexts.WadBadLump, name));

            lumps[i] = new WadLump(name, offset, size);
        }

        return new WadFile(data, lumps);
    }

    /// <summary>The index of the last lump called <paramref name="name"/>, or -1.</summary>
    public int IndexOf(string name) => _lastIndexByName.TryGetValue(name, out var index) ? index : -1;

    /// <summary>
    /// The index of the first lump called <paramref name="name"/> in [<paramref name="start"/>,
    /// <paramref name="end"/>), or -1.
    /// </summary>
    public int IndexOf(string name, int start, int end)
    {
        end = Math.Min(end, _lumps.Length);
        for (var i = Math.Max(0, start); i < end; i++)
        {
            if (_lumps[i].Name == name)
                return i;
        }

        return -1;
    }

    public bool Contains(string name) => _lastIndexByName.ContainsKey(name);

    public ReadOnlySpan<byte> Read(int index)
    {
        var lump = _lumps[index];
        return lump.Size == 0 ? ReadOnlySpan<byte>.Empty : _data.AsSpan(lump.Offset, lump.Size);
    }

    /// <exception cref="InvalidDataException">There is no lump called <paramref name="name"/>.</exception>
    public ReadOnlySpan<byte> Read(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
            throw new InvalidDataException(string.Format(DoomTexts.LumpMissing, name));

        return Read(index);
    }

    /// <summary>An 8 byte, zero padded lump or texture name as an upper case string.</summary>
    public static string ReadName(ReadOnlySpan<byte> bytes)
    {
        var length = 0;
        while (length < bytes.Length && length < NameLength && bytes[length] != 0)
            length++;

        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = char.ToUpperInvariant((char)bytes[i]);

        return new string(chars);
    }
}
