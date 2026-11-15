using System;
using System.Collections.Generic;
using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Frames;
using Microsoft.Xna.Framework;

namespace KartsterExport;

/// <summary>
/// The road surface as triangles between neighbouring frames' edges, to ask how high the road passes over a point:
/// what lies under a bridge deck, and which walls stand over a lower road.
/// </summary>
internal sealed class RoadDecks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly List<(Vector3 A, Vector3 B, Vector3 C)> _triangles = [];
    private readonly TrackTerrain _terrain;

    //**********************************************************
    //** ctor
    //**********************************************************

    public RoadDecks(IEnumerable<TrackSectionFrames> sections, TrackTerrain terrain)
    {
        _terrain = terrain;
        foreach (var section in sections)
        {
            for (var index = 1; index < section.Frames.Count; index++)
            {
                var from = section.Frames[index - 1];
                var to = section.Frames[index];
                _triangles.Add((from.LeftEdge, from.RightEdge, to.RightEdge));
                _triangles.Add((from.LeftEdge, to.RightEdge, to.LeftEdge));
            }
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The highest road at (x, z) = (<paramref name="point"/>.X, .Y); <see cref="float.NegativeInfinity"/>
    /// where there is none.</summary>
    public float Highest(Vector2 point)
    {
        var highest = float.NegativeInfinity;
        foreach (var triangle in _triangles)
        {
            if (TryHeight(triangle, point, out var height))
                highest = MathF.Max(highest, height);
        }

        return highest;
    }

    /// <summary>The lowest road at (x, z); <see cref="float.PositiveInfinity"/> where there is none.</summary>
    public float Lowest(Vector2 point)
    {
        var lowest = float.PositiveInfinity;
        foreach (var triangle in _triangles)
        {
            if (TryHeight(triangle, point, out var height))
                lowest = MathF.Min(lowest, height);
        }

        return lowest;
    }

    /// <summary>The lowest a kart can drive at (x, z): road or the terrain (no terrain: road
    /// only); <see cref="float.PositiveInfinity"/> where there is neither.</summary>
    public float Ground(Vector2 point) =>
        _terrain != null && _terrain.TryGetHeight(point.X, point.Y, out var height, out _)
            ? MathF.Min(height, Lowest(point))
            : Lowest(point);

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Barycentric in the plane; the triangle's winding does not matter.</summary>
    private static bool TryHeight((Vector3 A, Vector3 B, Vector3 C) triangle, Vector2 point, out float height)
    {
        var (a, b, c) = triangle;
        height = 0f;
        var v0 = new Vector2(b.X - a.X, b.Z - a.Z);
        var v1 = new Vector2(c.X - a.X, c.Z - a.Z);
        var v2 = new Vector2(point.X - a.X, point.Y - a.Z);
        var denominator = v0.X * v1.Y - v1.X * v0.Y;
        if (MathF.Abs(denominator) < 1e-8f)
            return false;
        var u = (v2.X * v1.Y - v1.X * v2.Y) / denominator;
        var v = (v0.X * v2.Y - v2.X * v0.Y) / denominator;
        if (u < 0f || v < 0f || u + v > 1f)
            return false;
        height = a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y);
        return true;
    }
}
