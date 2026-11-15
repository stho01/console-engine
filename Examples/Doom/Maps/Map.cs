using System;
using System.Collections.Generic;

namespace Doom.Maps;

/// <summary>A map point in world units (x east, y north).</summary>
public readonly record struct Vertex(float X, float Y);

/// <summary>
/// An area with one floor and one ceiling height. Heights, light and flats change at run time (doors, lifts, light
/// effects); the <c>Base*</c> values keep what the map started with.
/// </summary>
public sealed class Sector
{
    //**********************************************************
    //** props
    //**********************************************************

    public int Index { get; init; }
    public float FloorHeight { get; set; }
    public float CeilingHeight { get; set; }
    public int FloorFlat { get; set; }
    public int CeilingFlat { get; set; }
    public int LightLevel { get; set; }
    public int BaseLightLevel { get; init; }
    public int Special { get; set; }
    public int Tag { get; init; }

    /// <summary>The lines with this sector on either side.</summary>
    public List<Line> Lines { get; } = [];

    /// <summary>A floor or ceiling mover is running on this sector; a second one is not started.</summary>
    public bool FloorBusy { get; set; }
    public bool CeilingBusy { get; set; }

    /// <summary>The floor or ceiling moved this tic, so things in it re-check their height.</summary>
    public bool Moved { get; set; }
}

/// <summary>One side of a line: its sector, its three textures and their offsets.</summary>
public sealed class Side
{
    //**********************************************************
    //** props
    //**********************************************************

    public float XOffset { get; set; }
    public float YOffset { get; set; }
    public int Upper { get; set; }
    public int Lower { get; set; }
    public int Middle { get; set; }
    public Sector Sector { get; init; }
}

/// <summary>Line flags, as stored in <c>LINEDEFS</c>.</summary>
[Flags]
public enum LineFlags
{
    None = 0,
    Blocking = 1,
    BlockMonsters = 2,
    TwoSided = 4,
    UpperUnpegged = 8,
    LowerUnpegged = 16,
    Secret = 32,
    BlockSound = 64,
    NeverOnMap = 128,
    AlwaysOnMap = 256
}

/// <summary>A wall or a boundary between two sectors. The front side is on the right going from V1 to V2.</summary>
public sealed class Line
{
    //**********************************************************
    //** props
    //**********************************************************

    public int Index { get; init; }
    public Vertex V1 { get; init; }
    public Vertex V2 { get; init; }
    public LineFlags Flags { get; init; }
    public int Special { get; set; }
    public int Tag { get; init; }
    public Side Front { get; init; }

    /// <summary>Null for a one-sided line (a solid wall).</summary>
    public Side Back { get; init; }

    public Sector FrontSector => Front?.Sector;
    public Sector BackSector => Back?.Sector;
    public float Dx => V2.X - V1.X;
    public float Dy => V2.Y - V1.Y;
    public float MinX => MathF.Min(V1.X, V2.X);
    public float MaxX => MathF.Max(V1.X, V2.X);
    public float MinY => MathF.Min(V1.Y, V2.Y);
    public float MaxY => MathF.Max(V1.Y, V2.Y);

    /// <summary>Seen at least once, so the automap shows it.</summary>
    public bool Mapped { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Which side of the line (extended infinitely) a point is on: positive on the front (right) side, negative on
    /// the back side, 0 on the line.
    /// </summary>
    public float SideOf(float x, float y) => (x - V1.X) * Dy - (y - V1.Y) * Dx;
}

/// <summary>
/// A piece of a line bounding one subsector, as cut by the node builder. <see cref="Offset"/> is the distance from
/// the line's start (on its own side) to the seg's start, for texture alignment.
/// </summary>
public sealed class Seg
{
    //**********************************************************
    //** props
    //**********************************************************

    public Vertex V1 { get; init; }
    public Vertex V2 { get; init; }
    public Line Line { get; init; }
    public Side Side { get; init; }
    public Sector FrontSector { get; init; }

    /// <summary>Null for a one-sided line.</summary>
    public Sector BackSector { get; init; }

    public float Offset { get; init; }
    public float Length { get; init; }
}

/// <summary>A convex area: a run of consecutive segs, all in one sector.</summary>
public sealed class Subsector
{
    public int FirstSeg { get; init; }
    public int SegCount { get; init; }
    public Sector Sector { get; init; }
}

/// <summary>
/// A BSP node: the partition line (a point and a direction) and its two children. A child with
/// <see cref="Map.SubsectorFlag"/> set is a subsector, otherwise a node. <see cref="Front"/> is the right side.
/// </summary>
public readonly record struct Node(float X, float Y, float Dx, float Dy, int Front, int Back)
{
    /// <summary>True when the point is on the partition's back (left) side, or on the line.</summary>
    public bool IsOnBackSide(float x, float y) => (x - X) * Dy - (y - Y) * Dx <= 0;
}

/// <summary>A thing placed in the map: start spot, monster, item or decoration.</summary>
public readonly record struct MapThing(float X, float Y, float Angle, int Type, int Flags);

/// <summary>A loaded level: geometry, the BSP tree that orders it, and the things to spawn.</summary>
public sealed class Map
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Set on a node child that is a subsector.</summary>
    public const int SubsectorFlag = 0x8000;

    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; init; }
    public Sector[] Sectors { get; init; }
    public Side[] Sides { get; init; }
    public Line[] Lines { get; init; }
    public Seg[] Segs { get; init; }
    public Subsector[] Subsectors { get; init; }
    public Node[] Nodes { get; init; }
    public MapThing[] Things { get; init; }

    /// <summary>The tree's root: the last node, or subsector 0 when the map has no nodes.</summary>
    public int Root => Nodes.Length == 0 ? SubsectorFlag : Nodes.Length - 1;

    //**********************************************************
    //** public methods
    //**********************************************************

    public Subsector SubsectorAt(float x, float y)
    {
        var child = Root;
        while ((child & SubsectorFlag) == 0)
        {
            var node = Nodes[child];
            child = node.IsOnBackSide(x, y) ? node.Back : node.Front;
        }

        return Subsectors[child & ~SubsectorFlag];
    }

    public Sector SectorAt(float x, float y) => SubsectorAt(x, y).Sector;

    /// <summary>
    /// The sectors that share a two-sided line with <paramref name="sector"/>. Allocates: only for starting
    /// movers, not per frame.
    /// </summary>
    public static List<Sector> Neighbours(Sector sector)
    {
        var neighbours = new List<Sector>();
        foreach (var line in sector.Lines)
        {
            if (line.Back == null)
                continue;

            var other = line.FrontSector == sector ? line.BackSector : line.FrontSector;
            if (other != sector && !neighbours.Contains(other))
                neighbours.Add(other);
        }

        return neighbours;
    }
}
