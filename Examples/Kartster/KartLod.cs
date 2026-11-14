using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster;

/// <summary>
/// Levels of detail of one kart mesh, built once at load: the full mesh, then simpler ones by vertex clustering on a
/// grid of <see cref="CellMeters"/> (every vertex in a cell moves to the cell's average, triangles that collapse are
/// dropped, as are repeats). Each triangle keeps its part colour and opacity, so glass stays see-through. A kart is
/// drawn at the simplest level whose cell is no bigger than <see cref="PixelsPerCell"/> screen pixels where it is
/// (<see cref="Pick"/>): the real karts have 20 000 to 40 000 triangles, mostly far smaller than a pixel.
/// </summary>
public sealed class KartLod
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The grid of each simpler level, metres.</summary>
    public static readonly float[] CellMeters = [0.04f, 0.1f, 0.25f];

    /// <summary>How many pixels a level's cell may cover on screen.</summary>
    public const float PixelsPerCell = 0.75f;

    //**********************************************************
    //** ctor
    //**********************************************************

    public KartLod(Mesh full)
    {
        Levels = new Mesh[CellMeters.Length + 1];
        Levels[0] = full;
        for (var i = 0; i < CellMeters.Length; i++)
            Levels[i + 1] = Simplify(full, CellMeters[i]);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The full mesh first, then each <see cref="CellMeters"/> level.</summary>
    public Mesh[] Levels { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The level for a kart <paramref name="distance"/> metres from the eye, where one metre at one metre away is
    /// <paramref name="pixelsPerMeter"/> pixels (screen height / (2 tan(fov / 2))).
    /// </summary>
    public static int LevelFor(float distance, float pixelsPerMeter)
    {
        var pixelMeters = distance / pixelsPerMeter;
        var level = 0;
        while (level < CellMeters.Length && CellMeters[level] <= pixelMeters * PixelsPerCell)
            level++;
        return level;
    }

    /// <summary>The mesh to draw a kart with at <paramref name="distance"/> (see <see cref="LevelFor"/>).</summary>
    public Mesh Pick(float distance, float pixelsPerMeter) => Levels[LevelFor(distance, pixelsPerMeter)];

    /// <summary>
    /// <paramref name="mesh"/> clustered on a grid of <paramref name="cell"/> metres: colours, opacity and unlit kept
    /// per triangle, collapsed and repeated triangles dropped (a repeat with the other winding is kept: the back of a
    /// thin part).
    /// </summary>
    public static Mesh Simplify(Mesh mesh, float cell)
    {
        var cells = new Dictionary<(int, int, int), int>();
        var sums = new List<Vector3>();
        var counts = new List<int>();
        var remap = new int[mesh.Positions.Length];
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            var p = mesh.Positions[i] / cell;
            var key = ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));
            if (!cells.TryGetValue(key, out var index))
            {
                cells[key] = index = sums.Count;
                sums.Add(Vector3.Zero);
                counts.Add(0);
            }

            sums[index] += mesh.Positions[i];
            counts[index]++;
            remap[i] = index;
        }

        var positions = new Vector3[sums.Count];
        for (var i = 0; i < positions.Length; i++)
            positions[i] = sums[i] / counts[i];

        var indices = new List<int>();
        var colors = new List<Vector3>();
        var opacities = mesh.TriangleOpacity == null ? null : new List<float>();
        var seen = new HashSet<(int, int, int, Vector3)>();
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var a = remap[mesh.Indices[t * 3]];
            var b = remap[mesh.Indices[t * 3 + 1]];
            var c = remap[mesh.Indices[t * 3 + 2]];
            if (a == b || b == c || a == c)
                continue;

            // the same turn from the lowest index, so a repeat in either rotation is found
            var key = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
            if (!seen.Add((key.Item1, key.Item2, key.Item3, mesh.TriangleColors[t])))
                continue;

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            colors.Add(mesh.TriangleColors[t]);
            opacities?.Add(mesh.TriangleOpacity[t]);
        }

        return new Mesh(positions, indices.ToArray(), colors.ToArray())
        {
            TriangleOpacity = opacities?.ToArray(),
            IsUnlit = mesh.IsUnlit,
        };
    }
}
