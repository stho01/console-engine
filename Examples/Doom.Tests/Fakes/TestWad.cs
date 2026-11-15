using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Doom.Graphics;
using Doom.Maps;
using Doom.Play;
using Doom.Wad;

namespace Doom.Tests.Fakes;

/// <summary>
/// Builds a tiny WAD in memory, so the tests need no real game data. Palette: colour i is grey (i, i, i); every light
/// table maps a colour to itself. One 8 × 8 patch of colour <see cref="WallColor"/> makes the textures
/// <c>STARTAN</c>, <c>SW1TEST</c> and <c>SW2TEST</c>; flats <c>FLOOR</c> (<see cref="FloorColor"/>), <c>CEIL</c>
/// (<see cref="CeilingColor"/>) and <c>F_SKY1</c>. Map <c>E1M1</c>:
/// <code>
///   room A (0..256 × 0..256)  | door (256..272 × 96..160, closed) |  room B (272..400 × 0..256, open sky)
/// </code>
/// The player starts in room A at (64, 128) facing east; a stimpack lies at (96, 128). Both door lines have special
/// 1 (use to open); room B's east wall (line 13) has special 11 (exit switch).
/// </summary>
public static class TestWad
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const byte WallColor = 100;
    public const byte FloorColor = 50;
    public const byte CeilingColor = 60;
    public const int ExitLine = 13;
    public const int DoorSector = 1;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static byte[] Build()
    {
        var lumps = new List<(string, byte[])>
        {
            ("PLAYPAL", Playpal()),
            ("COLORMAP", ColorMap()),
            ("PNAMES", PatchNames("WALL")),
            ("WALL", SolidPicture(8, 8, WallColor)),
            ("TEXTURE1", Textures(("STARTAN", 8, 8), ("SW1TEST", 8, 8), ("SW2TEST", 8, 8))),
            ("F_START", Array.Empty<byte>()),
            ("FLOOR", Filled(4096, FloorColor)),
            ("CEIL", Filled(4096, CeilingColor)),
            ("F_SKY1", Filled(4096, 0)),
            ("F_END", Array.Empty<byte>()),
            ("S_START", Array.Empty<byte>()),
            ("STIMA0", SolidPicture(4, 4, 200)),
            ("S_END", Array.Empty<byte>())
        };
        lumps.AddRange(BuildMap());
        return Wad(lumps);
    }

    /// <summary>Loads the test WAD into the game's libraries.</summary>
    public static (WadFile Wad, Palette Palette, TextureLibrary Textures, SpriteLibrary Sprites) Load()
    {
        var wad = WadFile.Parse(Build());
        return (wad, Palette.Load(wad), TextureLibrary.Load(wad), SpriteLibrary.Load(wad));
    }

    /// <summary>A running level on the test map.</summary>
    public static Level CreateLevel(PlayerLoadout loadout = null)
    {
        var (wad, _, textures, sprites) = Load();
        var map = MapLoader.Load(wad, "E1M1", textures);
        return Level.Create(map, textures, sprites, loadout ?? new PlayerLoadout());
    }

    /// <summary>Writes a WAD: header, lump data, directory.</summary>
    public static byte[] Wad(IReadOnlyList<(string Name, byte[] Data)> lumps)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataSize = 0;
        foreach (var (_, data) in lumps)
            dataSize += data.Length;

        writer.Write(Encoding.ASCII.GetBytes("IWAD"));
        writer.Write(lumps.Count);
        writer.Write(12 + dataSize);
        foreach (var (_, data) in lumps)
            writer.Write(data);

        var offset = 12;
        foreach (var (name, data) in lumps)
        {
            writer.Write(offset);
            writer.Write(data.Length);
            writer.Write(Name(name));
            offset += data.Length;
        }

        return stream.ToArray();
    }

    /// <summary>A picture lump of one colour, every column one post.</summary>
    public static byte[] SolidPicture(int width, int height, byte color)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)width);
        writer.Write((short)height);
        writer.Write((short)(width / 2));
        writer.Write((short)height);
        var columnSize = 3 + height + 2;
        for (var x = 0; x < width; x++)
            writer.Write(8 + width * 4 + x * columnSize);
        for (var x = 0; x < width; x++)
        {
            writer.Write((byte)0);
            writer.Write((byte)height);
            writer.Write((byte)0);
            for (var y = 0; y < height; y++)
                writer.Write(color);
            writer.Write((byte)0);
            writer.Write((byte)0xFF);
        }

        return stream.ToArray();
    }

    public static byte[] Name(string name)
    {
        var bytes = new byte[8];
        Encoding.ASCII.GetBytes(name, 0, Math.Min(8, name.Length), bytes, 0);
        return bytes;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static byte[] Filled(int size, byte value)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static byte[] Playpal()
    {
        var bytes = new byte[768 * 14];
        for (var p = 0; p < 14; p++)
        {
            for (var i = 0; i < 256; i++)
            {
                bytes[p * 768 + i * 3] = (byte)i;
                bytes[p * 768 + i * 3 + 1] = (byte)i;
                bytes[p * 768 + i * 3 + 2] = (byte)i;
            }
        }

        return bytes;
    }

    private static byte[] ColorMap()
    {
        var bytes = new byte[34 * 256];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 256);
        return bytes;
    }

    private static byte[] PatchNames(params string[] names)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(names.Length);
        foreach (var name in names)
            writer.Write(Name(name));
        return stream.ToArray();
    }

    /// <summary>A <c>TEXTURE1</c> lump: each texture is patch 0 at (0, 0).</summary>
    private static byte[] Textures(params (string Name, short Width, short Height)[] textures)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(textures.Length);
        const int entrySize = 22 + 10;
        for (var i = 0; i < textures.Length; i++)
            writer.Write(4 + textures.Length * 4 + i * entrySize);
        foreach (var (name, width, height) in textures)
        {
            writer.Write(Name(name));
            writer.Write(0);
            writer.Write(width);
            writer.Write(height);
            writer.Write(0);
            writer.Write((short)1);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)0);
        }

        return stream.ToArray();
    }

    private static IEnumerable<(string, byte[])> BuildMap()
    {
        (short X, short Y)[] vertices =
        [
            (0, 0), (256, 0), (0, 256), (256, 256), (256, 96), (256, 160), // 0-5 room A
            (272, 96), (272, 160), (272, 0), (272, 256), (400, 0), (400, 256) // 6-11 door, room B
        ];

        // v1, v2, special, front side, back side (-1 none)
        (int V1, int V2, int Special, int Front, int Back)[] lines =
        [
            (1, 0, 0, 0, -1), (0, 2, 0, 1, -1), (2, 3, 0, 2, -1), (3, 5, 0, 3, -1), // 0-3 room A
            (5, 4, 1, 4, 5), (4, 1, 0, 6, -1), // 4 door A side, 5 room A
            (6, 4, 0, 7, -1), (5, 7, 0, 8, -1), // 6-7 door tracks
            (6, 7, 1, 9, 10), // 8 door B side
            (10, 8, 0, 11, -1), (8, 6, 0, 12, -1), (7, 9, 0, 13, -1), (9, 11, 0, 14, -1), // 9-12 room B
            (11, 10, 11, 15, -1) // 13 exit switch
        ];

        // sector of each side; upper/middle textures
        (int Sector, string Upper, string Middle)[] sides =
        [
            (0, "-", "STARTAN"), (0, "-", "STARTAN"), (0, "-", "STARTAN"), (0, "-", "STARTAN"),
            (0, "STARTAN", "-"), (1, "STARTAN", "-"), (0, "-", "STARTAN"),
            (1, "-", "STARTAN"), (1, "-", "STARTAN"),
            (2, "STARTAN", "-"), (1, "STARTAN", "-"),
            (2, "-", "STARTAN"), (2, "-", "STARTAN"), (2, "-", "STARTAN"), (2, "-", "STARTAN"),
            (2, "-", "SW1TEST")
        ];

        // line, side (0 front, 1 back) for each seg, grouped by subsector
        (int Line, int Side)[] segs =
        [
            (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), // subsector 0: room A
            (4, 1), (6, 0), (8, 1), (7, 0), // subsector 1: door
            (9, 0), (10, 0), (8, 0), (11, 0), (12, 0), (13, 0) // subsector 2: room B
        ];

        var thingsLump = Lump(writer =>
        {
            Thing(writer, 64, 128, 0, ThingTypes.PlayerStart);
            Thing(writer, 96, 128, 0, 2011);
        });
        var linesLump = Lump(writer =>
        {
            foreach (var (v1, v2, special, front, back) in lines)
            {
                writer.Write((short)v1);
                writer.Write((short)v2);
                writer.Write((short)(back >= 0 ? (int)LineFlags.TwoSided : (int)LineFlags.Blocking));
                writer.Write((short)special);
                writer.Write((short)0);
                writer.Write((ushort)front);
                writer.Write(back >= 0 ? (ushort)back : (ushort)0xFFFF);
            }
        });
        var sidesLump = Lump(writer =>
        {
            foreach (var (sector, upper, middle) in sides)
            {
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write(Name(upper));
                writer.Write(Name("-"));
                writer.Write(Name(middle));
                writer.Write((short)sector);
            }
        });
        var verticesLump = Lump(writer =>
        {
            foreach (var (x, y) in vertices)
            {
                writer.Write(x);
                writer.Write(y);
            }
        });
        var segsLump = Lump(writer =>
        {
            foreach (var (line, side) in segs)
            {
                var (v1, v2, _, _, _) = lines[line];
                writer.Write((short)(side == 0 ? v1 : v2));
                writer.Write((short)(side == 0 ? v2 : v1));
                writer.Write((short)0);
                writer.Write((short)line);
                writer.Write((short)side);
                writer.Write((short)0);
            }
        });
        var subsectorsLump = Lump(writer =>
        {
            writer.Write((short)6);
            writer.Write((short)0);
            writer.Write((short)4);
            writer.Write((short)6);
            writer.Write((short)6);
            writer.Write((short)10);
        });
        var nodesLump = Lump(writer =>
        {
            Node(writer, 256, 0, 0, 256, Map.SubsectorFlag | 1, Map.SubsectorFlag | 0);
            Node(writer, 272, 0, 0, 256, Map.SubsectorFlag | 2, 0);
        });
        var sectorsLump = Lump(writer =>
        {
            Sector(writer, 0, 128, "FLOOR", "CEIL", 160);
            Sector(writer, 0, 0, "FLOOR", "CEIL", 160);
            Sector(writer, 0, 128, "FLOOR", "F_SKY1", 200);
        });

        return
        [
            ("E1M1", Array.Empty<byte>()),
            ("THINGS", thingsLump),
            ("LINEDEFS", linesLump),
            ("SIDEDEFS", sidesLump),
            ("VERTEXES", verticesLump),
            ("SEGS", segsLump),
            ("SSECTORS", subsectorsLump),
            ("NODES", nodesLump),
            ("SECTORS", sectorsLump)
        ];
    }

    private static byte[] Lump(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static void Thing(BinaryWriter writer, short x, short y, short angle, int type)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(angle);
        writer.Write((short)type);
        writer.Write((short)7);
    }

    private static void Node(BinaryWriter writer, short x, short y, short dx, short dy, int front, int back)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(dx);
        writer.Write(dy);
        for (var i = 0; i < 8; i++)
            writer.Write((short)0);
        writer.Write((ushort)front);
        writer.Write((ushort)back);
    }

    private static void Sector(BinaryWriter writer, short floor, short ceiling, string floorFlat, string ceilingFlat,
        short light)
    {
        writer.Write(floor);
        writer.Write(ceiling);
        writer.Write(Name(floorFlat));
        writer.Write(Name(ceilingFlat));
        writer.Write(light);
        writer.Write((short)0);
        writer.Write((short)0);
    }
}
