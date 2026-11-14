using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using KartViewer.Rendering;

namespace KartViewer;

/// <summary>
/// A Kartster track as its editor exports it (<c>Docs/track-meshes.md</c> in Kartster): <c>&lt;id&gt;.ribbon.obj</c>
/// is the road surface and its edge walls, <c>&lt;id&gt;.spline.obj</c> the centerline, one vertex per metre, sections
/// in route order. Plus a flat chequered ground under it all.
/// </summary>
public sealed class Track
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float GroundMargin = 40f;  // metres of ground beyond the road on every side
    private const float GroundTileSize = 8f;
    private const float GroundDrop = 0.3f;   // below the road, so the two never fight in the depth buffer
    private const float TangentSpan = 3f;    // metres either side of a point used for its direction

    private static readonly Vector3 Asphalt = new(0.33f, 0.33f, 0.35f);
    private static readonly Vector3 WallColor = new(0.8f, 0.78f, 0.72f);
    private static readonly Vector3 Grass = new(0.2f, 0.45f, 0.15f);
    private static readonly Vector3 OtherGrass = new(0.26f, 0.52f, 0.18f);

    private readonly Vector3[] _centerline;
    private readonly float[] _distances; // route distance at each centerline point

    //**********************************************************
    //** ctor
    //**********************************************************

    private Track(string name, Mesh road, Vector3[] centerline)
    {
        Name = name;
        Road = road;
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
        GroundWorld = Matrix4x4.CreateTranslation((min.X + max.X) / 2, min.Y - GroundDrop, (min.Z + max.Z) / 2);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; }
    public Mesh Road { get; }
    public Mesh Ground { get; }
    public Matrix4x4 GroundWorld { get; }
    public float Length { get; }
    public bool IsClosed { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Loads <c>&lt;id&gt;.ribbon.obj</c> and <c>&lt;id&gt;.spline.obj</c> from <paramref name="folder"/>.
    /// </summary>
    public static Track Load(string folder, string id)
    {
        var positions = new List<Vector3>();
        var indices = new List<int>();
        ObjFile.Read(Path.Combine(folder, id + ".ribbon.obj"), positions, indices);

        var road = new Mesh(positions.ToArray(), indices.ToArray(), new Vector3[indices.Count / 3]);
        for (var t = 0; t < road.TriangleCount; t++)
            road.TriangleColors[t] = road.TriangleNormals[t].Y > 0.5f ? Asphalt : WallColor;

        // The spline file has only points and polylines; the points in file order are the route.
        var splinePoints = new List<Vector3>();
        ObjFile.Read(Path.Combine(folder, id + ".spline.obj"), splinePoints, []);
        var centerline = new List<Vector3>(splinePoints.Count);
        foreach (var point in splinePoints)
        {
            // Neighbouring sections share their boundary point.
            if (centerline.Count == 0 || Vector3.DistanceSquared(centerline[^1], point) > 1e-6f)
                centerline.Add(point);
        }

        if (centerline.Count < 2)
            throw new InvalidDataException($"{id}.spline.obj needs at least two centerline points");

        return new Track(id, road, centerline.ToArray());
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

    //**********************************************************
    //** internals
    //**********************************************************

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
