using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;
using Doom.Maps;

namespace Doom.Rendering.Meshes;

/// <summary>
/// A map as <c>ConsoleEngine.Rendering3D</c> meshes, three per sector: its floors and ceilings (cut into
/// <see cref="BlockColors.Size"/>-unit squares on the flat's grid), its sky ceilings (drawn in
/// <see cref="SkyColor"/>, which the renderer swaps for the sky picture) and the walls on its side of its lines
/// (cut into texture blocks). Walls follow Doom's rules: upper and lower parts, pegging, the sky hack, see-through
/// middle textures drawn once inside the opening. The colours are unlit; the renderer lights each sector by its
/// light level. <see cref="Update"/> rebuilds the meshes of sectors whose heights, textures or offsets changed.
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

    /// <summary>A see-through texture block is drawn when at least this share of its texels is.</summary>
    private const float MinCoverage = 0.5f;

    /// <summary>Wall and floor cells thinner than this (map units) are not cut further.</summary>
    private const float MinStep = 1 / 64f;

    private readonly Map _map;
    private readonly TextureLibrary _textures;
    private readonly BlockColors _blocks;
    private readonly List<Vector2[]>[] _sectorPolygons;
    private readonly MeshBuilder _builder = new();
    private readonly List<Vector2> _strip = new(16);
    private readonly List<Vector2> _cell = new(16);
    private readonly List<Vector2> _scratch = new(16);
    private readonly List<Vector2> _polygonCell = new(16);

    //**********************************************************
    //** ctor
    //**********************************************************

    public LevelMeshes(Map map, TextureLibrary textures, BlockColors blocks)
    {
        _map = map;
        _textures = textures;
        _blocks = blocks;
        var subsectorPolygons = SubsectorPolygons.Build(map);
        _sectorPolygons = new List<Vector2[]>[map.Sectors.Length];
        Sectors = new SectorMeshes[map.Sectors.Length];
        for (var i = 0; i < Sectors.Length; i++)
        {
            _sectorPolygons[i] = [];
            Sectors[i] = new SectorMeshes { FlatKey = -1, WallKey = -1 };
        }

        for (var i = 0; i < map.Subsectors.Length; i++)
        {
            var sector = map.Subsectors[i].Sector;
            if (sector != null && subsectorPolygons[i].Length > 0)
                _sectorPolygons[sector.Index].Add(subsectorPolygons[i]);
        }

        Update();
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>In <see cref="Map.Sectors"/> order.</summary>
    public SectorMeshes[] Sectors { get; }

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
            var meshes = Sectors[i];
            var flatKey = FlatKey(sector);
            if (flatKey != meshes.FlatKey)
            {
                meshes.FlatKey = flatKey;
                BuildFlats(sector, meshes);
            }

            var wallKey = WallKey(sector);
            if (wallKey != meshes.WallKey)
            {
                meshes.WallKey = wallKey;
                BuildWalls(sector, meshes);
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

    private void BuildFlats(Sector sector, SectorMeshes meshes)
    {
        var polygons = _sectorPolygons[sector.Index];
        var sky = _textures.SkyFlat;
        var ceilingIsSky = sky >= 0 && sector.CeilingFlat == sky;
        var floorFlat = FlatFrame(sector.FloorFlat);
        var ceilingFlat = FlatFrame(sector.CeilingFlat);

        _builder.Clear();
        foreach (var polygon in polygons)
        {
            if (floorFlat >= 0 && sector.FloorFlat != sky)
                AddFlat(polygon, sector.FloorHeight, floorFlat, ceiling: false);
            if (ceilingFlat >= 0 && !ceilingIsSky)
                AddFlat(polygon, sector.CeilingHeight, ceilingFlat, ceiling: true);
        }

        meshes.Flats = _builder.ToMesh(meshes.Flats);
        meshes.FlatsMin = _builder.Min;
        meshes.FlatsMax = _builder.Max;

        _builder.Clear();
        if (ceilingIsSky)
        {
            foreach (var polygon in polygons)
                AddFan(polygon, sector.CeilingHeight, SkyColor, ceiling: true);
        }

        meshes.Sky = _builder.ToMesh(meshes.Sky, unlit: true);
        meshes.SkyMin = _builder.Min;
        meshes.SkyMax = _builder.Max;
    }

    /// <summary>The polygon cut into the flat's blocks, on a grid of <see cref="BlockColors.Size"/> units.</summary>
    private void AddFlat(Vector2[] polygon, float height, int flat, bool ceiling)
    {
        const int size = BlockColors.Size;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var corner in polygon)
        {
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }

        _scratch.Clear();
        _scratch.AddRange(polygon);
        for (var x0 = MathF.Floor(min.X / size) * size; x0 < max.X; x0 += size)
        {
            ConvexPolygon.Clip(_scratch, _cell, Vector2.UnitX, x0);
            ConvexPolygon.Clip(_cell, _strip, -Vector2.UnitX, -(x0 + size));
            if (_strip.Count < 3)
                continue;

            for (var y0 = MathF.Floor(min.Y / size) * size; y0 < max.Y; y0 += size)
            {
                ConvexPolygon.Clip(_strip, _cell, Vector2.UnitY, y0);
                ConvexPolygon.Clip(_cell, _polygonCell, -Vector2.UnitY, -(y0 + size));
                if (_polygonCell.Count < 3)
                    continue;

                // Flats are drawn with row = floor(-y) & 63 (see WorldRenderer.DrawPlane).
                var color = _blocks.Flat(flat, (int)x0, -(int)y0 - size);
                AddFan(_polygonCell, height, color, ceiling);
            }
        }
    }

    /// <summary>A convex counter-clockwise polygon at a height, facing up (floor) or down (ceiling).</summary>
    private void AddFan(IReadOnlyList<Vector2> polygon, float height, Vector3 color, bool ceiling)
    {
        var first = _builder.VertexCount;
        for (var i = 0; i < polygon.Count; i++)
            _builder.AddVertex(MeshBuilder.ToWorld(polygon[i].X, polygon[i].Y, height));
        for (var i = 1; i + 1 < polygon.Count; i++)
        {
            if (ceiling)
                _builder.AddTriangle(first, first + i + 1, first + i, color);
            else
                _builder.AddTriangle(first, first + i, first + i + 1, color);
        }
    }

    //**********************************************************
    //** internals: walls
    //**********************************************************

    private void BuildWalls(Sector sector, SectorMeshes meshes)
    {
        _builder.Clear();
        foreach (var line in sector.Lines)
        {
            if (line.FrontSector == sector)
                AddSide(line, line.Front, line.FrontSector, line.BackSector, line.V1, line.V2);
            if (line.BackSector == sector)
                AddSide(line, line.Back, line.BackSector, line.FrontSector, line.V2, line.V1);
        }

        meshes.Walls = _builder.ToMesh(meshes.Walls);
        meshes.WallsMin = _builder.Min;
        meshes.WallsMax = _builder.Max;
    }

    /// <summary>One side's wall parts, seen from <paramref name="front"/> (<paramref name="start"/> on the left).</summary>
    private void AddSide(Line line, Side side, Sector front, Sector back, Vertex start, Vertex end)
    {
        if (side == null || front == null)
            return;

        var shade = line.V1.Y == line.V2.Y ? DarkerWall : line.V1.X == line.V2.X ? LighterWall : 1f;
        var wall = new Wall(new Vector2(start.X, start.Y), new Vector2(end.X, end.Y), side.XOffset, shade);
        var lowerUnpegged = (line.Flags & LineFlags.LowerUnpegged) != 0;
        var upperUnpegged = (line.Flags & LineFlags.UpperUnpegged) != 0;

        if (back == null)
        {
            var middle = TextureFrame(side.Middle);
            if (middle < 0)
                return;

            var anchor = (lowerUnpegged ? front.FloorHeight + _textures.Textures[middle].Height
                : front.CeilingHeight) + side.YOffset;
            AddWallPart(wall, front.FloorHeight, front.CeilingHeight, middle, anchor, tiled: true);
            return;
        }

        var sky = _textures.SkyFlat;
        var skyHack = sky >= 0 && front.CeilingFlat == sky && back.CeilingFlat == sky;
        var upper = TextureFrame(side.Upper);
        if (back.CeilingHeight < front.CeilingHeight && !skyHack && upper >= 0)
        {
            var anchor = (upperUnpegged ? front.CeilingHeight
                : back.CeilingHeight + _textures.Textures[upper].Height) + side.YOffset;
            AddWallPart(wall, MathF.Max(back.CeilingHeight, front.FloorHeight), front.CeilingHeight, upper, anchor,
                tiled: true);
        }

        var lower = TextureFrame(side.Lower);
        if (back.FloorHeight > front.FloorHeight && lower >= 0)
        {
            var anchor = (lowerUnpegged ? front.CeilingHeight : back.FloorHeight) + side.YOffset;
            AddWallPart(wall, front.FloorHeight, MathF.Min(back.FloorHeight, front.CeilingHeight), lower, anchor,
                tiled: true);
        }

        var masked = TextureFrame(side.Middle);
        if (masked >= 0)
        {
            var openBottom = MathF.Max(front.FloorHeight, back.FloorHeight);
            var openTop = MathF.Min(front.CeilingHeight, back.CeilingHeight);
            var height = _textures.Textures[masked].Height;
            var anchor = (lowerUnpegged ? openBottom + height : openTop) + side.YOffset;
            AddWallPart(wall, MathF.Max(openBottom, anchor - height), MathF.Min(openTop, anchor), masked, anchor,
                tiled: false);
        }
    }

    /// <summary>A side of a line: from <see cref="Start"/> (left, seen from the front) to <see cref="End"/>.</summary>
    private readonly record struct Wall(Vector2 Start, Vector2 End, float XOffset, float Shade);

    /// <summary>
    /// The wall from <paramref name="bottom"/> to <paramref name="top"/>, cut where texture blocks (or the
    /// texture's edges) begin. Row 0 of the texture is at height <paramref name="anchor"/>. Untiled (see-through
    /// middle) parts drop blocks that are mostly holes.
    /// </summary>
    private void AddWallPart(in Wall wall, float bottom, float top, int texture, float anchor, bool tiled)
    {
        if (top - bottom < MinStep)
            return;

        var source = _textures.Textures[texture];
        var length = Vector2.Distance(wall.Start, wall.End);
        if (length < MinStep)
            return;

        var direction = (wall.End - wall.Start) / length;
        for (var s = 0f; s < length;)
        {
            var e = MathF.Min(length, s + StepToNextBlock(wall.XOffset + s, source.Width));
            var column = (int)MathF.Floor(wall.XOffset + (s + e) / 2);
            var left = wall.Start + direction * s;
            var right = wall.Start + direction * e;
            for (var z = top; z > bottom;)
            {
                var z2 = MathF.Max(bottom, z - StepToNextBlock(anchor - z, source.Height));
                var row = (int)MathF.Floor(anchor - (z + z2) / 2);
                var (color, coverage) = _blocks.Texture(texture, column, row);
                if (tiled || coverage >= MinCoverage)
                    _builder.AddQuad(MeshBuilder.ToWorld(left.X, left.Y, z2), MeshBuilder.ToWorld(right.X, right.Y, z2),
                        MeshBuilder.ToWorld(right.X, right.Y, z), MeshBuilder.ToWorld(left.X, left.Y, z),
                        color * wall.Shade);
                z = z2;
            }

            s = e;
        }
    }

    /// <summary>
    /// Texels from <paramref name="position"/> to the next block start or texture edge (at least <see cref="MinStep"/>).
    /// </summary>
    private static float StepToNextBlock(float position, int textureSize)
    {
        var local = position - MathF.Floor(position / textureSize) * textureSize;
        var next = MathF.Min((MathF.Floor(local / BlockColors.Size) + 1) * BlockColors.Size, textureSize);
        var step = next - local;
        return step < MinStep ? step + MathF.Min(BlockColors.Size, textureSize) : step;
    }
}

/// <summary>The meshes of one sector (any may be null: nothing to draw) and their bounds.</summary>
public sealed class SectorMeshes
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
    internal long FlatKey { get; set; }
    internal long WallKey { get; set; }
}
