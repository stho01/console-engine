using System;
using System.Buffers.Binary;
using System.IO;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.Maps;

/// <summary>
/// Reads a map from the lumps after its marker (<c>E1M1</c>, <c>MAP01</c>…): <c>THINGS</c>, <c>LINEDEFS</c>,
/// <c>SIDEDEFS</c>, <c>VERTEXES</c>, <c>SEGS</c>, <c>SSECTORS</c>, <c>NODES</c>, <c>SECTORS</c>. All values are
/// little-endian; texture and flat names are resolved through a <see cref="TextureLibrary"/>.
/// </summary>
public static class MapLoader
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Map lumps follow the marker within this many entries.</summary>
    private const int MapLumpCount = 11;

    private const int ThingSize = 10;
    private const int LineSize = 14;
    private const int SideSize = 30;
    private const int VertexSize = 4;
    private const int SegSize = 12;
    private const int SubsectorSize = 4;
    private const int NodeSize = 28;
    private const int SectorSize = 26;
    private const int NoSide = 0xFFFF;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <exception cref="InvalidDataException">The map or one of its lumps is missing.</exception>
    public static Map Load(WadFile wad, string name, TextureLibrary textures)
    {
        var marker = wad.IndexOf(name);
        if (marker < 0)
            throw new InvalidDataException(string.Format(DoomTexts.MapMissing, name));

        var vertices = ReadVertices(Lump(wad, marker, name, "VERTEXES"));
        var sectors = ReadSectors(Lump(wad, marker, name, "SECTORS"), textures);
        var sides = ReadSides(Lump(wad, marker, name, "SIDEDEFS"), sectors, textures);
        var lines = ReadLines(Lump(wad, marker, name, "LINEDEFS"), vertices, sides);
        var segs = ReadSegs(Lump(wad, marker, name, "SEGS"), vertices, lines);
        var subsectors = ReadSubsectors(Lump(wad, marker, name, "SSECTORS"), segs);
        var nodes = ReadNodes(Lump(wad, marker, name, "NODES"));
        var things = ReadThings(Lump(wad, marker, name, "THINGS"));

        foreach (var line in lines)
        {
            line.FrontSector?.Lines.Add(line);
            if (line.BackSector != null && line.BackSector != line.FrontSector)
                line.BackSector.Lines.Add(line);
        }

        return new Map
        {
            Name = name,
            Sectors = sectors,
            Sides = sides,
            Lines = lines,
            Segs = segs,
            Subsectors = subsectors,
            Nodes = nodes,
            Things = things
        };
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static ReadOnlySpan<byte> Lump(WadFile wad, int marker, string map, string lump)
    {
        var index = wad.IndexOf(lump, marker + 1, marker + MapLumpCount);
        if (index < 0)
            throw new InvalidDataException(string.Format(DoomTexts.MapLumpMissing, map, lump));

        return wad.Read(index);
    }

    private static short Int16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);

    private static ushort UInt16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    private static Vertex[] ReadVertices(ReadOnlySpan<byte> data)
    {
        var vertices = new Vertex[data.Length / VertexSize];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = new Vertex(Int16(data, i * VertexSize), Int16(data, i * VertexSize + 2));

        return vertices;
    }

    private static Sector[] ReadSectors(ReadOnlySpan<byte> data, TextureLibrary textures)
    {
        var sectors = new Sector[data.Length / SectorSize];
        for (var i = 0; i < sectors.Length; i++)
        {
            var entry = data.Slice(i * SectorSize, SectorSize);
            var light = Int16(entry, 20);
            sectors[i] = new Sector
            {
                Index = i,
                FloorHeight = Int16(entry, 0),
                CeilingHeight = Int16(entry, 2),
                FloorFlat = textures.FindFlat(WadFile.ReadName(entry.Slice(4, 8))),
                CeilingFlat = textures.FindFlat(WadFile.ReadName(entry.Slice(12, 8))),
                LightLevel = light,
                BaseLightLevel = light,
                Special = Int16(entry, 22),
                Tag = Int16(entry, 24)
            };
        }

        return sectors;
    }

    private static Side[] ReadSides(ReadOnlySpan<byte> data, Sector[] sectors, TextureLibrary textures)
    {
        var sides = new Side[data.Length / SideSize];
        for (var i = 0; i < sides.Length; i++)
        {
            var entry = data.Slice(i * SideSize, SideSize);
            var sector = Int16(entry, 28);
            sides[i] = new Side
            {
                XOffset = Int16(entry, 0),
                YOffset = Int16(entry, 2),
                Upper = textures.FindTexture(WadFile.ReadName(entry.Slice(4, 8))),
                Lower = textures.FindTexture(WadFile.ReadName(entry.Slice(12, 8))),
                Middle = textures.FindTexture(WadFile.ReadName(entry.Slice(20, 8))),
                Sector = sector >= 0 && sector < sectors.Length ? sectors[sector] : sectors[0]
            };
        }

        return sides;
    }

    private static Line[] ReadLines(ReadOnlySpan<byte> data, Vertex[] vertices, Side[] sides)
    {
        var lines = new Line[data.Length / LineSize];
        for (var i = 0; i < lines.Length; i++)
        {
            var entry = data.Slice(i * LineSize, LineSize);
            var front = UInt16(entry, 10);
            var back = UInt16(entry, 12);
            lines[i] = new Line
            {
                Index = i,
                V1 = vertices[UInt16(entry, 0)],
                V2 = vertices[UInt16(entry, 2)],
                Flags = (LineFlags)UInt16(entry, 4),
                Special = UInt16(entry, 6),
                Tag = Int16(entry, 8),
                Front = front != NoSide && front < sides.Length ? sides[front] : null,
                Back = back != NoSide && back < sides.Length ? sides[back] : null
            };
        }

        return lines;
    }

    private static Seg[] ReadSegs(ReadOnlySpan<byte> data, Vertex[] vertices, Line[] lines)
    {
        var segs = new Seg[data.Length / SegSize];
        for (var i = 0; i < segs.Length; i++)
        {
            var entry = data.Slice(i * SegSize, SegSize);
            var v1 = vertices[UInt16(entry, 0)];
            var v2 = vertices[UInt16(entry, 2)];
            var line = lines[UInt16(entry, 6)];
            var onBack = Int16(entry, 8) != 0;
            var side = onBack ? line.Back : line.Front;
            var other = onBack ? line.Front : line.Back;
            segs[i] = new Seg
            {
                V1 = v1,
                V2 = v2,
                Line = line,
                Side = side,
                FrontSector = side?.Sector,
                BackSector = line.Back != null ? other?.Sector : null,
                Offset = Int16(entry, 10),
                Length = MathF.Sqrt((v2.X - v1.X) * (v2.X - v1.X) + (v2.Y - v1.Y) * (v2.Y - v1.Y))
            };
        }

        return segs;
    }

    private static Subsector[] ReadSubsectors(ReadOnlySpan<byte> data, Seg[] segs)
    {
        var subsectors = new Subsector[data.Length / SubsectorSize];
        for (var i = 0; i < subsectors.Length; i++)
        {
            var count = UInt16(data, i * SubsectorSize);
            var first = UInt16(data, i * SubsectorSize + 2);
            subsectors[i] = new Subsector
            {
                FirstSeg = first,
                SegCount = count,
                Sector = first < segs.Length ? segs[first].FrontSector : null
            };
        }

        return subsectors;
    }

    private static Node[] ReadNodes(ReadOnlySpan<byte> data)
    {
        var nodes = new Node[data.Length / NodeSize];
        for (var i = 0; i < nodes.Length; i++)
        {
            var entry = data.Slice(i * NodeSize, NodeSize);
            nodes[i] = new Node(Int16(entry, 0), Int16(entry, 2), Int16(entry, 4), Int16(entry, 6),
                UInt16(entry, 24), UInt16(entry, 26));
        }

        return nodes;
    }

    private static MapThing[] ReadThings(ReadOnlySpan<byte> data)
    {
        var things = new MapThing[data.Length / ThingSize];
        for (var i = 0; i < things.Length; i++)
        {
            var entry = data.Slice(i * ThingSize, ThingSize);
            things[i] = new MapThing(Int16(entry, 0), Int16(entry, 2), Int16(entry, 4) * MathF.PI / 180f,
                Int16(entry, 6), Int16(entry, 8));
        }

        return things;
    }
}
