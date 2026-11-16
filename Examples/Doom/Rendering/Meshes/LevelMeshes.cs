using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;
using Doom.Maps;

namespace Doom.Rendering.Meshes;

/// <summary>
/// A map as <c>ConsoleEngine.Rendering3D</c> meshes, three per subsector (Doom's convex BSP leaves, so the renderer
/// can skip what it can't see): its floor and ceiling (one textured fan each, the flat on its 64-unit world grid),
/// its sky ceiling (drawn in <see cref="SkyColor"/>, which the renderer swaps for the sky picture) and the walls of
/// its segs (one textured quad per part). Walls follow Doom's rules: upper and lower parts, pegging and offsets, the
/// sky hack, see-through middle textures drawn once inside the opening (their holes are texture holes). Textures are
/// tinted only by Doom's fake contrast; the renderer lights each subsector by its sector's light level.
/// <see cref="Update"/> rebuilds the subsectors of sectors whose heights, textures or offsets changed.
/// </summary>
public sealed class LevelMeshes
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>
    /// What sky ceilings are drawn in: unlit, so it comes out of the rasterizer as exactly <see cref="SkyKey"/>.
    /// </summary>
    public static readonly Vector3 SkyColor = new(1, 0, 1);

    /// <summary><see cref="SkyColor"/> packed as <c>0xRRGGBB</c>.</summary>
    public const uint SkyKey = 0xFF00FF;

    /// <summary>Lines exactly along x or y are drawn a little darker or lighter, so corners read (like Doom).</summary>
    private const float DarkerWall = 0.9f;

    private const float LighterWall = 1.1f;

    /// <summary>Walls thinner or shorter than this (map units) are left out.</summary>
    private const float MinSize = 1 / 64f;

    private static readonly Vector3 White = Vector3.One;

    private readonly Map _map;
    private readonly TextureLibrary _textures;
    private readonly TextureCache _textureCache;
    private readonly List<int>[] _sectorSubsectors;
    private readonly long[] _flatKeys;
    private readonly long[] _wallKeys;
    private readonly MeshBuilder _builder = new();

    //**********************************************************
    //** ctor
    //**********************************************************

    public LevelMeshes(Map map, TextureLibrary textures, TextureCache textureCache)
    {
        _map = map;
        _textures = textures;
        _textureCache = textureCache;
        Polygons = SubsectorPolygons.Build(map);
        _sectorSubsectors = new List<int>[map.Sectors.Length];
        _flatKeys = new long[map.Sectors.Length];
        _wallKeys = new long[map.Sectors.Length];
        Array.Fill(_flatKeys, -1);
        Array.Fill(_wallKeys, -1);
        for (var i = 0; i < _sectorSubsectors.Length; i++)
            _sectorSubsectors[i] = [];

        Subsectors = new SubsectorMeshes[map.Subsectors.Length];
        for (var i = 0; i < Subsectors.Length; i++)
        {
            Subsectors[i] = new SubsectorMeshes();
            var sector = map.Subsectors[i].Sector;
            if (sector != null)
                _sectorSubsectors[sector.Index].Add(i);
        }

        Update();
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>In <see cref="Map.Subsectors"/> order.</summary>
    public SubsectorMeshes[] Subsectors { get; }

    /// <summary>Each subsector's floor shape (<see cref="SubsectorPolygons"/>), in subsector order.</summary>
    public Vector2[][] Polygons { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Rebuilds the meshes of sectors that changed since the last call: heights (doors, lifts), flats and wall
    /// textures (animations, switches) or wall offsets (scrolling). Allocates only while a mesh outgrows its arrays.
    /// </summary>
    public void Update()
    {
        var sectors = _map.Sectors;
        for (var i = 0; i < sectors.Length; i++)
        {
            var sector = sectors[i];
            var flatKey = FlatKey(sector);
            if (flatKey != _flatKeys[i])
            {
                _flatKeys[i] = flatKey;
                foreach (var subsector in _sectorSubsectors[i])
                    BuildFlats(sector, subsector);
            }

            var wallKey = WallKey(sector);
            if (wallKey != _wallKeys[i])
            {
                _wallKeys[i] = wallKey;
                foreach (var subsector in _sectorSubsectors[i])
                    BuildWalls(subsector);
            }
        }
    }

    //**********************************************************
    //** internals: change keys
    //**********************************************************

    private int FlatFrame(int flat) => flat >= 0 && flat < _textures.FlatFrame.Length ? _textures.FlatFrame[flat] : -1;

    private int TextureFrame(int texture) =>
        texture >= 0 && texture < _textures.TextureFrame.Length ? _textures.TextureFrame[texture] : -1;

    private long FlatKey(Sector sector) =>
        HashCode.Combine(sector.FloorHeight, sector.CeilingHeight, FlatFrame(sector.FloorFlat),
            FlatFrame(sector.CeilingFlat)) & 0xFFFFFFFFL;

    private long WallKey(Sector sector)
    {
        var hash = new HashCode();
        foreach (var line in sector.Lines)
        {
            AddSide(ref hash, line.FrontSector == sector ? line.Front : null);
            AddSide(ref hash, line.BackSector == sector ? line.Back : null);
            AddHeights(ref hash, line.FrontSector);
            AddHeights(ref hash, line.BackSector);
        }

        return hash.ToHashCode() & 0xFFFFFFFFL;
    }

    private void AddSide(ref HashCode hash, Side side)
    {
        if (side == null)
            return;

        hash.Add(TextureFrame(side.Upper));
        hash.Add(TextureFrame(side.Middle));
        hash.Add(TextureFrame(side.Lower));
        hash.Add(side.XOffset);
        hash.Add(side.YOffset);
    }

    private static void AddHeights(ref HashCode hash, Sector sector)
    {
        if (sector == null)
            return;

        hash.Add(sector.FloorHeight);
        hash.Add(sector.CeilingHeight);
        hash.Add(sector.CeilingFlat);
    }

    //**********************************************************
    //** internals: floors and ceilings
    //**********************************************************

    private void BuildFlats(Sector sector, int subsector)
    {
        var polygon = Polygons[subsector];
        var meshes = Subsectors[subsector];
        var sky = _textures.SkyFlat;
        var ceilingIsSky = sky >= 0 && sector.CeilingFlat == sky;
        var floorFlat = FlatFrame(sector.FloorFlat);
        var ceilingFlat = FlatFrame(sector.CeilingFlat);

        _builder.Clear();
        if (floorFlat >= 0 && sector.FloorFlat != sky)
            AddFlat(polygon, sector.FloorHeight, floorFlat, ceiling: false);
        if (ceilingFlat >= 0 && !ceilingIsSky)
            AddFlat(polygon, sector.CeilingHeight, ceilingFlat, ceiling: true);

        meshes.Flats = _builder.ToMesh(meshes.Flats);
        meshes.FlatsMin = _builder.Min;
        meshes.FlatsMax = _builder.Max;

        _builder.Clear();
        if (ceilingIsSky)
            AddFan(polygon, sector.CeilingHeight, SkyColor, ceiling: true);

        meshes.Sky = _builder.ToMesh(meshes.Sky, unlit: true);
        meshes.SkyMin = _builder.Min;
        meshes.SkyMax = _builder.Max;
    }

    /// <summary>The polygon with the flat on it: texel (x, -y) of the world, repeating every 64 units.</summary>
    private void AddFlat(Vector2[] polygon, float height, int flat, bool ceiling)
    {
        if (polygon.Length >= 3)
            AddFan(polygon, height, White, ceiling, _textureCache.Flat(flat));
    }

    /// <summary>A convex counter-clockwise polygon at a height, facing up (floor) or down (ceiling).</summary>
    private void AddFan(IReadOnlyList<Vector2> polygon, float height, Vector3 color, bool ceiling,
        MeshTexture texture = null)
    {
        var first = FlatVertex(polygon[0], height, texture);
        var previous = FlatVertex(polygon[1], height, texture);
        for (var i = 2; i < polygon.Count; i++)
        {
            var next = FlatVertex(polygon[i], height, texture);
            if (ceiling)
                _builder.AddTriangle(first, next, previous, color, texture);
            else
                _builder.AddTriangle(first, previous, next, color, texture);
            previous = next;
        }
    }

    /// <summary>A floor or ceiling corner; flats are drawn with row = -y (see WorldRenderer.DrawPlane).</summary>
    private int FlatVertex(Vector2 corner, float height, MeshTexture texture) =>
        _builder.AddVertex(MeshBuilder.ToWorld(corner.X, corner.Y, height),
            texture == null ? default : new Vector2(corner.X, -corner.Y) / Graphics.Flat.Size);

    //**********************************************************
    //** internals: walls
    //**********************************************************

    private void BuildWalls(int subsector)
    {
        var meshes = Subsectors[subsector];
        var info = _map.Subsectors[subsector];
        var end = Math.Min(info.FirstSeg + info.SegCount, _map.Segs.Length);
        _builder.Clear();
        for (var i = info.FirstSeg; i < end; i++)
            AddSeg(_map.Segs[i]);

        meshes.Walls = _builder.ToMesh(meshes.Walls);
        meshes.WallsMin = _builder.Min;
        meshes.WallsMax = _builder.Max;
    }

    /// <summary>A seg's wall parts, seen from its front sector (its start on the left).</summary>
    private void AddSeg(Seg seg)
    {
        var side = seg.Side;
        var front = seg.FrontSector;
        var back = seg.BackSector;
        var line = seg.Line;
        if (side == null || front == null || line == null)
            return;

        var shade = line.V1.Y == line.V2.Y ? DarkerWall : line.V1.X == line.V2.X ? LighterWall : 1f;
        var wall = new Wall(new Vector2(seg.V1.X, seg.V1.Y), new Vector2(seg.V2.X, seg.V2.Y),
            side.XOffset + seg.Offset, shade);
        var lowerUnpegged = (line.Flags & LineFlags.LowerUnpegged) != 0;
        var upperUnpegged = (line.Flags & LineFlags.UpperUnpegged) != 0;

        if (back == null)
        {
            var middle = TextureFrame(side.Middle);
            if (middle < 0)
                return;

            var anchor = (lowerUnpegged ? front.FloorHeight + _textures.Textures[middle].Height
                : front.CeilingHeight) + side.YOffset;
            AddWallPart(wall, front.FloorHeight, front.CeilingHeight, middle, anchor);
            return;
        }

        var sky = _textures.SkyFlat;
        var skyHack = sky >= 0 && front.CeilingFlat == sky && back.CeilingFlat == sky;
        var upper = TextureFrame(side.Upper);
        if (back.CeilingHeight < front.CeilingHeight && !skyHack && upper >= 0)
        {
            var anchor = (upperUnpegged ? front.CeilingHeight
                : back.CeilingHeight + _textures.Textures[upper].Height) + side.YOffset;
            AddWallPart(wall, MathF.Max(back.CeilingHeight, front.FloorHeight), front.CeilingHeight, upper, anchor);
        }

        var lower = TextureFrame(side.Lower);
        if (back.FloorHeight > front.FloorHeight && lower >= 0)
        {
            var anchor = (lowerUnpegged ? front.CeilingHeight : back.FloorHeight) + side.YOffset;
            AddWallPart(wall, front.FloorHeight, MathF.Min(back.FloorHeight, front.CeilingHeight), lower, anchor);
        }

        var masked = TextureFrame(side.Middle);
        if (masked >= 0)
        {
            var openBottom = MathF.Max(front.FloorHeight, back.FloorHeight);
            var openTop = MathF.Min(front.CeilingHeight, back.CeilingHeight);
            var height = _textures.Textures[masked].Height;
            var anchor = (lowerUnpegged ? openBottom + height : openTop) + side.YOffset;
            AddWallPart(wall, MathF.Max(openBottom, anchor - height), MathF.Min(openTop, anchor), masked, anchor);
        }
    }

    /// <summary>A side of a line: from <see cref="Start"/> (left, seen from the front) to <see cref="End"/>.</summary>
    private readonly record struct Wall(Vector2 Start, Vector2 End, float XOffset, float Shade);

    /// <summary>
    /// The wall from <paramref name="bottom"/> to <paramref name="top"/> as one textured quad. Row 0 of the texture
    /// is at height <paramref name="anchor"/>, column 0 at the start of the line (plus the offsets); it repeats.
    /// </summary>
    private void AddWallPart(in Wall wall, float bottom, float top, int texture, float anchor)
    {
        var length = Vector2.Distance(wall.Start, wall.End);
        if (top - bottom < MinSize || length < MinSize)
            return;

        var source = _textures.Textures[texture];
        var u1 = wall.XOffset / source.Width;
        var u2 = (wall.XOffset + length) / source.Width;
        var vTop = (anchor - top) / source.Height;
        var vBottom = (anchor - bottom) / source.Height;
        _builder.AddQuad(
            MeshBuilder.ToWorld(wall.Start.X, wall.Start.Y, bottom), new Vector2(u1, vBottom),
            MeshBuilder.ToWorld(wall.End.X, wall.End.Y, bottom), new Vector2(u2, vBottom),
            MeshBuilder.ToWorld(wall.End.X, wall.End.Y, top), new Vector2(u2, vTop),
            MeshBuilder.ToWorld(wall.Start.X, wall.Start.Y, top), new Vector2(u1, vTop),
            White * wall.Shade, _textureCache.Wall(texture));
    }
}

/// <summary>The meshes of one subsector (any may be null: nothing to draw) and their bounds.</summary>
public sealed class SubsectorMeshes
{
    public Mesh Flats { get; set; }
    public Vector3 FlatsMin { get; set; }
    public Vector3 FlatsMax { get; set; }

    /// <summary>Unlit, in <see cref="LevelMeshes.SkyColor"/>.</summary>
    public Mesh Sky { get; set; }

    public Vector3 SkyMin { get; set; }
    public Vector3 SkyMax { get; set; }
    public Mesh Walls { get; set; }
    public Vector3 WallsMin { get; set; }
    public Vector3 WallsMax { get; set; }
}
