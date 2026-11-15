using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster;

/// <summary>
/// A Kartster track as its editor exports it (<c>Docs/track-meshes.md</c> in Kartster): <c>&lt;id&gt;.ribbon.obj</c>
/// is the road surface and its edge walls, <c>&lt;id&gt;.spline.obj</c> the centerline, one vertex per metre, sections
/// in route order. Plus the ground: the original's hilly <see cref="Terrain"/> from <c>&lt;id&gt;.terrain.txt</c> when
/// there is one, else a flat chequered ground under it all; the scenery from <c>&lt;id&gt;.scenery.txt</c> when there
/// is one (models from the <c>Scenery</c> folder beside the tracks folder) and the power-up pads from
/// <c>&lt;id&gt;.powerups.txt</c>. The road may run on several levels (a bridge over itself): <see cref="TryGetGround"/>
/// takes the highest road no higher than where it looks from, through a grid of the road's triangles built once.
/// </summary>
public sealed class Track
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float GroundMargin = 40f;  // metres of ground beyond the road on every side
    private const float GroundTileSize = 8f;
    private const float GroundDrop = 0.3f;   // below the road, so the two never fight in the depth buffer
    private const float TerrainDrawDistance = 400f; // the far plane: hills show from across the track
    private const float TangentSpan = 3f;    // metres either side of a point used for its direction
    private const float MaxPointSpacing = 2f; // the spline has a point per metre; a longer step is another branch
    private const float RoadCellSize = 4f;   // the road grid's squares, metres
    private const float MinRoadNormalY = 0.5f; // steeper triangles are the edge walls

    public static readonly Vector3 Asphalt = new(0.33f, 0.33f, 0.35f);
    private static readonly Vector3 WallColor = new(0.8f, 0.78f, 0.72f);
    private static readonly Vector3 Grass = new(0.2f, 0.45f, 0.15f);
    private static readonly Vector3 OtherGrass = new(0.26f, 0.52f, 0.18f);

    private readonly Vector3[] _centerline;
    private readonly float[] _distances; // route distance at each centerline point

    // The road grid: square c holds the road triangles _cellTriangles[_cellStarts[c] .. _cellStarts[c + 1]].
    private readonly float _gridMinX;
    private readonly float _gridMinZ;
    private readonly int _gridColumns;
    private readonly int _gridRows;
    private readonly int[] _cellStarts;
    private int[] _cellTriangles;

    //**********************************************************
    //** ctor
    //**********************************************************

    private Track(string name, Mesh road, Vector3[] centerline, Terrain terrain)
    {
        Name = name;
        Road = road;
        Terrain = terrain;
        _centerline = centerline;
        _distances = new float[centerline.Length];
        for (var i = 1; i < centerline.Length; i++)
            _distances[i] = _distances[i - 1] + Vector3.Distance(centerline[i - 1], centerline[i]);
        Length = _distances[^1];
        IsClosed = Vector3.DistanceSquared(centerline[0], centerline[^1]) < 1f;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in road.Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        var size = MathF.Max(max.X - min.X, max.Z - min.Z) + 2 * GroundMargin;
        var tiles = (int)MathF.Ceiling(size / GroundTileSize);
        Ground = Mesh.CreateFloor(tiles * GroundTileSize, tiles, Grass, OtherGrass);
        GroundHeight = min.Y - GroundDrop;
        GroundWorld = Matrix4x4.CreateTranslation((min.X + max.X) / 2, GroundHeight, (min.Z + max.Z) / 2);
        GroundHalfSize = tiles * GroundTileSize / 2;
        GroundCenter = new Vector3((min.X + max.X) / 2, GroundHeight, (min.Z + max.Z) / 2);

        _gridMinX = min.X;
        _gridMinZ = min.Z;
        _gridColumns = (int)((max.X - min.X) / RoadCellSize) + 1;
        _gridRows = (int)((max.Z - min.Z) / RoadCellSize) + 1;
        _cellStarts = new int[_gridColumns * _gridRows + 1];
        BuildRoadGrid();
    }

    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; }
    public Mesh Road { get; }
    public Mesh Ground { get; }
    public Matrix4x4 GroundWorld { get; }

    /// <summary>The flat chequered ground's height, 0.3 m below the lowest road point.</summary>
    public float GroundHeight { get; }

    public Vector3 GroundCenter { get; }
    public float GroundHalfSize { get; }
    public float Length { get; }
    public bool IsClosed { get; }
    public Scenery Scenery { get; private set; } = Scenery.Empty;

    /// <summary>The original's hilly ground from <c>&lt;id&gt;.terrain.txt</c>; null: the flat chequered ground.</summary>
    public Terrain Terrain { get; }

    /// <summary>Power-up pad centres (world axes) from <c>&lt;id&gt;.powerups.txt</c>; none without the file.</summary>
    public Vector3[] PowerUpPads { get; private set; } = [];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Loads <c>&lt;id&gt;.ribbon.obj</c>, <c>&lt;id&gt;.spline.obj</c> and, if they are there, <c>&lt;id&gt;.terrain.txt</c>,
    /// <c>&lt;id&gt;.scenery.txt</c> and <c>&lt;id&gt;.powerups.txt</c> from <paramref name="folder"/>.
    /// </summary>
    public static Track Load(string folder, string id)
    {
        var positions = new List<Vector3>();
        var indices = new List<int>();
        ObjFile.Read(Path.Combine(folder, id + ".ribbon.obj"), positions, indices);

        var road = new Mesh(positions.ToArray(), indices.ToArray(), new Vector3[indices.Count / 3]);
        for (var t = 0; t < road.TriangleCount; t++)
            road.TriangleColors[t] = road.TriangleNormals[t].Y > MinRoadNormalY ? Asphalt : WallColor;

        // The spline file has only points and polylines; the points in file order are the route, up to the first jump.
        // After it come the track's other branches (Prism Loop's north section), which are not part of the route.
        var splinePoints = new List<Vector3>();
        ObjFile.Read(Path.Combine(folder, id + ".spline.obj"), splinePoints, []);
        var centerline = new List<Vector3>(splinePoints.Count);
        foreach (var point in splinePoints)
        {
            if (centerline.Count > 0 && Vector3.DistanceSquared(centerline[^1], point) > MaxPointSpacing * MaxPointSpacing)
                break;
            // Neighbouring sections share their boundary point.
            if (centerline.Count == 0 || Vector3.DistanceSquared(centerline[^1], point) > 1e-6f)
                centerline.Add(point);
        }

        if (centerline.Count < 2)
            throw new InvalidDataException($"{id}.spline.obj needs at least two centerline points");

        var terrainPath = Path.Combine(folder, id + ".terrain.txt");
        var terrain = File.Exists(terrainPath) ? Terrain.Load(terrainPath) : null;
        var track = new Track(id, road, centerline.ToArray(), terrain);
        var scenery = Path.Combine(folder, id + ".scenery.txt");
        // On the original's terrain the scenery's heights are its own; on the chequer y = 0 moves down onto it.
        if (File.Exists(scenery))
            track.Scenery = Scenery.Load(scenery, Path.Combine(folder, "..", "Scenery"),
                terrain == null ? track.GroundHeight : 0f);
        track.PowerUpPads = PowerUps.PowerUpPads.Load(folder, id);

        return track;
    }

    /// <summary>
    /// Where the centerline is at <paramref name="distance"/> metres along the route, and which way it runs there.
    /// Wraps round a closed track, clamps on an open one.
    /// </summary>
    public (Vector3 Position, Vector3 Forward) Sample(float distance)
    {
        var position = PositionAt(distance);
        var forward = PositionAt(distance + TangentSpan) - PositionAt(distance - TangentSpan);
        return (position, forward == Vector3.Zero ? -Vector3.UnitZ : Vector3.Normalize(forward));
    }

    /// <summary>
    /// Whether <paramref name="position"/> is above the road surface (seen from straight above), and the height of the
    /// highest road there (the deck, where the road crosses itself on a bridge); <see cref="GroundHeight"/> when not.
    /// </summary>
    public bool TryGetRoadHeight(Vector3 position, out float height)
    {
        if (TryGetRoad(position.X, position.Z, float.MaxValue, out height, out _))
            return true;

        height = GroundHeight;
        return false;
    }

    /// <summary>
    /// What a kart at (x, z) stands on, looked for straight down from <paramref name="top"/>: the highest road
    /// triangle no higher than that, else the terrain, else the flat ground (everywhere, on a track without terrain).
    /// A road above <paramref name="top"/> (the deck of a bridge over the kart) does not count.
    /// </summary>
    /// <param name="onRoad">The surface is the road (asphalt), not the ground (grass).</param>
    /// <returns>False only past the edge of the terrain, where there is no ground at all.</returns>
    public bool TryGetGround(float x, float z, float top, out float height, out Vector3 normal, out bool onRoad)
    {
        onRoad = TryGetRoad(x, z, top, out height, out normal);
        if (onRoad)
            return true;
        if (Terrain != null)
            return Terrain.TryGetHeight(x, z, out height, out normal);

        height = GroundHeight;
        normal = Vector3.UnitY;
        return true;
    }

    /// <summary>
    /// Draws the ground: the terrain's tiles in view (culled like the scenery, out to the far plane), else the chequer.
    /// </summary>
    public void DrawGround(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 eye, Vector3 toLight)
    {
        if (Terrain != null)
            Terrain.Tiles.Draw(renderer, viewProjection, eye, toLight, TerrainDrawDistance);
        else
            renderer.Draw(Ground, GroundWorld, viewProjection, toLight);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The highest road triangle under (x, z) no higher than <paramref name="top"/>, and its normal.</summary>
    private bool TryGetRoad(float x, float z, float top, out float height, out Vector3 normal)
    {
        height = float.MinValue;
        normal = Vector3.UnitY;
        var column = (int)MathF.Floor((x - _gridMinX) / RoadCellSize);
        var row = (int)MathF.Floor((z - _gridMinZ) / RoadCellSize);
        if (column < 0 || row < 0 || column >= _gridColumns || row >= _gridRows)
            return false;

        var cell = row * _gridColumns + column;
        var position = new Vector3(x, 0f, z);
        var positions = Road.Positions;
        var indices = Road.Indices;
        var found = false;
        for (var i = _cellStarts[cell]; i < _cellStarts[cell + 1]; i++)
        {
            var t = _cellTriangles[i];
            var a = positions[indices[t * 3]];
            var b = positions[indices[t * 3 + 1]];
            var c = positions[indices[t * 3 + 2]];
            var area = Cross2D(a, b, c);
            var weightA = Cross2D(b, c, position) / area;
            var weightB = Cross2D(c, a, position) / area;
            var weightC = 1 - weightA - weightB;
            if (weightA < 0 || weightB < 0 || weightC < 0)
                continue;

            var y = weightA * a.Y + weightB * b.Y + weightC * c.Y;
            if (y <= top && y > height)
            {
                height = y;
                normal = Road.TriangleNormals[t];
                found = true;
            }
        }

        return found;
    }

    // A counting sort of the road triangles into the squares their bounds touch: count, sum, fill. Once, at load.
    private void BuildRoadGrid()
    {
        var cellCount = _gridColumns * _gridRows;
        int[] next = null;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var t = 0; t < Road.TriangleCount; t++)
            {
                var a = Road.Positions[Road.Indices[t * 3]];
                var b = Road.Positions[Road.Indices[t * 3 + 1]];
                var c = Road.Positions[Road.Indices[t * 3 + 2]];
                if (Road.TriangleNormals[t].Y <= MinRoadNormalY || Cross2D(a, b, c) == 0)
                    continue;

                var firstColumn = GridIndex(MathF.Min(a.X, MathF.Min(b.X, c.X)) - _gridMinX, _gridColumns);
                var lastColumn = GridIndex(MathF.Max(a.X, MathF.Max(b.X, c.X)) - _gridMinX, _gridColumns);
                var firstRow = GridIndex(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) - _gridMinZ, _gridRows);
                var lastRow = GridIndex(MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) - _gridMinZ, _gridRows);
                for (var row = firstRow; row <= lastRow; row++)
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    var cell = row * _gridColumns + column;
                    if (next == null)
                        _cellStarts[cell + 1]++;
                    else
                        _cellTriangles[next[cell]++] = t;
                }
            }

            if (next == null)
            {
                for (var cell = 0; cell < cellCount; cell++)
                    _cellStarts[cell + 1] += _cellStarts[cell];
                _cellTriangles = new int[_cellStarts[cellCount]];
                next = (int[])_cellStarts.Clone();
            }
        }
    }

    private static int GridIndex(float offset, int count) => Math.Clamp((int)(offset / RoadCellSize), 0, count - 1);

    /// <summary>Twice the signed area of a, b, p projected on the ground (x, z).</summary>
    private static float Cross2D(Vector3 a, Vector3 b, Vector3 p) =>
        (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);

    private Vector3 PositionAt(float distance)
    {
        distance = IsClosed
            ? distance - MathF.Floor(distance / Length) * Length
            : Math.Clamp(distance, 0, Length);

        var index = Array.BinarySearch(_distances, distance);
        if (index >= 0)
            return _centerline[index];

        var next = Math.Min(~index, _centerline.Length - 1);
        var previous = next - 1;
        var segment = _distances[next] - _distances[previous];
        return Vector3.Lerp(_centerline[previous], _centerline[next], (distance - _distances[previous]) / segment);
    }
}
