using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.Lighting;

/// <summary>
/// Where a kart's lights shine from and its glowing lenses, read from the kart folder's OBJ parts as Kartster's
/// <c>KartDesign</c> reads them: the middle of each <c>light_front…</c>, <c>light_long…</c> and <c>light_rear…</c>
/// marker part's bounds (model space), and the <c>lamp_white…</c>, <c>lamp_yellow…</c> and <c>lamp_red…</c> lenses as
/// one unlit mesh in the lens colours, puffed out by <see cref="LensLift"/> so it covers the kart's own (lit) lens.
/// </summary>
public sealed class KartLamps
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>How far the glowing lens stands out of the drawn one, metres (it must win the depth test).</summary>
    public const float LensLift = 0.01f;

    private static readonly Vector3 WhiteLens = new(1f, 1f, 0.9f);
    private static readonly Vector3 YellowLens = new(1f, 0.85f, 0.25f);
    private static readonly Vector3 RedLens = new(0.95f, 0.1f, 0.08f);

    //**********************************************************
    //** ctor
    //**********************************************************

    private KartLamps(Vector3[] front, Vector3[] longRange, Vector3[] rear, Mesh lenses, Vector3[] lensCorners)
    {
        Front = front;
        LongRange = longRange;
        Rear = rear;
        Lenses = lenses;
        LensCorners = lensCorners;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Headlights (<c>light_front…</c>), model space.</summary>
    public Vector3[] Front { get; }

    /// <summary>Long-range lights (<c>light_long…</c>), model space.</summary>
    public Vector3[] LongRange { get; }

    /// <summary>Rear lights (<c>light_rear…</c>), model space.</summary>
    public Vector3[] Rear { get; }

    /// <summary>The lenses, unlit, in their colours; null when the kart has none.</summary>
    public Mesh Lenses { get; }

    /// <summary>A few of the lens files' own corners, to recognise the kart's mesh by (<see cref="IsPartOf"/>).</summary>
    private Vector3[] LensCorners { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public static KartLamps Load(string folder)
    {
        var front = new List<Vector3>();
        var longRange = new List<Vector3>();
        var rear = new List<Vector3>();
        var positions = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Vector3>();
        var corners = new List<Vector3>();
        var files = Directory.GetFiles(folder, "*.obj");
        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var part = Path.GetFileNameWithoutExtension(file);
            if (part.StartsWith("light_front", StringComparison.OrdinalIgnoreCase))
                front.Add(MiddleOf(file));
            else if (part.StartsWith("light_long", StringComparison.OrdinalIgnoreCase))
                longRange.Add(MiddleOf(file));
            else if (part.StartsWith("light_rear", StringComparison.OrdinalIgnoreCase))
                rear.Add(MiddleOf(file));
            else if (LensColor(part) is { } color)
                AddLens(file, color, positions, indices, colors, corners);
        }

        var lenses = indices.Count == 0
            ? null
            : new Mesh(positions.ToArray(), indices.ToArray(), colors.ToArray()) { IsUnlit = true };
        return new KartLamps(front.ToArray(), longRange.ToArray(), rear.ToArray(), lenses, corners.ToArray());
    }

    /// <summary>
    /// Whether <paramref name="kart"/> (a <c>KartModel</c> mesh, maybe recoloured) was loaded from this folder: its
    /// positions hold the lens corners. False for a kart without lenses.
    /// </summary>
    public bool IsPartOf(Mesh kart)
    {
        if (LensCorners.Length == 0)
            return false;

        foreach (var corner in LensCorners)
        {
            if (Array.IndexOf(kart.Positions, corner) < 0)
                return false;
        }

        return true;
    }

    /// <summary>The lamps of the folder <paramref name="kart"/> was loaded from, or null.</summary>
    public static KartLamps Find(Mesh kart, KartLamps[] folders)
    {
        foreach (var lamps in folders)
        {
            if (lamps.IsPartOf(kart))
                return lamps;
        }

        return null;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static Vector3? LensColor(string part)
    {
        if (part.StartsWith("lamp_white", StringComparison.OrdinalIgnoreCase))
            return WhiteLens;
        if (part.StartsWith("lamp_yellow", StringComparison.OrdinalIgnoreCase))
            return YellowLens;
        if (part.StartsWith("lamp_red", StringComparison.OrdinalIgnoreCase))
            return RedLens;
        return null;
    }

    private static Vector3 MiddleOf(string file)
    {
        var positions = new List<Vector3>();
        ObjFile.Read(file, positions, []);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return (min + max) / 2;
    }

    /// <summary>
    /// Appends the lens, each corner moved <see cref="LensLift"/> out along the mean of its triangles' normals
    /// (several lenses in one file stay apart, as they would not when scaled about one middle).
    /// </summary>
    private static void AddLens(string file, Vector3 color, List<Vector3> positions, List<int> indices,
        List<Vector3> colors, List<Vector3> corners)
    {
        var lens = new List<Vector3>();
        var lensIndices = new List<int>();
        ObjFile.Read(file, lens, lensIndices);
        for (var i = 0; i < Math.Min(3, lens.Count); i++)
            corners.Add(lens[i]);

        var normals = new Vector3[lens.Count];
        for (var i = 0; i < lensIndices.Count; i += 3)
        {
            var (a, b, c) = (lensIndices[i], lensIndices[i + 1], lensIndices[i + 2]);
            var normal = Vector3.Cross(lens[b] - lens[a], lens[c] - lens[a]);
            if (normal == Vector3.Zero)
                continue;
            normal = Vector3.Normalize(normal);
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }

        var first = positions.Count;
        for (var i = 0; i < lens.Count; i++)
            positions.Add(lens[i] + (normals[i] == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(normals[i]) * LensLift));
        foreach (var index in lensIndices)
            indices.Add(first + index);
        for (var i = 0; i < lensIndices.Count / 3; i++)
            colors.Add(color);
    }
}
