using System;
using System.Numerics;
using Doom.Maps;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Which subsectors (and so sectors) the camera can see, found the way <see cref="WorldRenderer"/> finds its walls:
/// the BSP tree is walked front to back, and each subsector's floor shape and each seg facing the camera are
/// projected onto the view's columns. A subsector over a column that is still open is visible; a solid wall, or a
/// two-sided line whose opening is shut (a closed door), closes its columns. The walk stops once every column is
/// closed. Heights don't narrow the columns, so it errs on the side of drawing too much. Visible segs' lines are
/// marked for the automap. Allocates nothing per frame.
/// </summary>
public sealed class Visibility
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly int _width;
    private readonly float _halfWidth;
    private readonly bool[] _solid;
    private readonly Vector2[][] _polygons;
    private readonly bool[] _visibleSector;
    private readonly int[] _visibleSubsectors;
    private int _cameraSubsector;
    private int _open;
    private Map _map;
    private Camera _camera;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="polygons">Each subsector's floor shape (<see cref="SubsectorPolygons"/>).</param>
    public Visibility(int width, int sectorCount, Vector2[][] polygons)
    {
        _width = width;
        _halfWidth = width / 2f;
        _polygons = polygons;
        _solid = new bool[width];
        _visibleSector = new bool[sectorCount];
        _visibleSubsectors = new int[polygons.Length];
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The visible subsectors, near to far, after <see cref="Find"/>.</summary>
    public ReadOnlySpan<int> VisibleSubsectors => _visibleSubsectors.AsSpan(0, Count);

    public int Count { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Whether any subsector of the sector is visible.</summary>
    public bool IsSectorVisible(int sector) => _visibleSector[sector];

    public void Find(Map map, in Camera camera)
    {
        _map = map;
        _camera = camera;
        Array.Fill(_solid, false);
        Array.Fill(_visibleSector, false);
        Count = 0;
        _open = _width;
        _cameraSubsector = -1;
        if (map.Subsectors.Length == 0)
            return;

        var cameraSubsector = map.SubsectorAt(camera.X, camera.Y);
        _cameraSubsector = Array.IndexOf(map.Subsectors, cameraSubsector);
        Walk(map.Root);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Walk(int child)
    {
        if (_open <= 0)
            return;

        if ((child & Map.SubsectorFlag) != 0)
        {
            var index = child & ~Map.SubsectorFlag;
            var subsector = _map.Subsectors[index];
            if (index == _cameraSubsector || IsOpen(_polygons[index]))
            {
                _visibleSubsectors[Count++] = index;
                if (subsector.Sector != null)
                    _visibleSector[subsector.Sector.Index] = true;
            }

            var end = Math.Min(subsector.FirstSeg + subsector.SegCount, _map.Segs.Length);
            for (var i = subsector.FirstSeg; i < end; i++)
                CheckSeg(_map.Segs[i]);
            return;
        }

        var node = _map.Nodes[child];
        var behind = node.IsOnBackSide(_camera.X, _camera.Y);
        Walk(behind ? node.Back : node.Front);
        Walk(behind ? node.Front : node.Back);
    }

    private void CheckSeg(Seg seg)
    {
        var front = seg.FrontSector;
        if (front == null)
            return;

        var v1 = seg.V1;
        var v2 = seg.V2;
        if ((v2.X - v1.X) * (_camera.Y - v1.Y) - (v2.Y - v1.Y) * (_camera.X - v1.X) >= 0)
            return;

        _camera.ToView(v1.X, v1.Y, out var side1, out var depth1);
        _camera.ToView(v2.X, v2.Y, out var side2, out var depth2);
        var near = WorldRenderer.NearPlane;
        if (depth1 < near && depth2 < near)
            return;

        if (depth1 < near)
        {
            side1 += (side2 - side1) * (near - depth1) / (depth2 - depth1);
            depth1 = near;
        }
        else if (depth2 < near)
        {
            side2 += (side1 - side2) * (near - depth2) / (depth1 - depth2);
            depth2 = near;
        }

        // Focal length = half the width (90° across), as in WorldRenderer.
        var x1 = Math.Max(0, (int)MathF.Ceiling(_halfWidth + side1 * _halfWidth / depth1 - 0.5f));
        var x2 = Math.Min(_width, (int)MathF.Ceiling(_halfWidth + side2 * _halfWidth / depth2 - 0.5f));
        if (x1 >= x2)
            return;

        var back = seg.BackSector;
        var closes = back == null || back.CeilingHeight <= back.FloorHeight
                                  || back.CeilingHeight <= front.FloorHeight || back.FloorHeight >= front.CeilingHeight;
        var seen = false;
        for (var x = x1; x < x2; x++)
        {
            if (_solid[x])
                continue;

            seen = true;
            if (!closes)
                continue;

            _solid[x] = true;
            _open--;
        }

        if (!seen)
            return;

        seg.Line.Mapped = true;
    }

    /// <summary>Whether the polygon covers a column that is still open (in front of the near plane).</summary>
    private bool IsOpen(Vector2[] polygon)
    {
        var near = WorldRenderer.NearPlane;
        var min = float.MaxValue;
        var max = float.MinValue;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            _camera.ToView(polygon[j].X, polygon[j].Y, out var sideA, out var depthA);
            _camera.ToView(polygon[i].X, polygon[i].Y, out var sideB, out var depthB);
            if (depthA >= near)
                Include(sideA, depthA, ref min, ref max);
            if ((depthA >= near) != (depthB >= near))
                Include(sideA + (sideB - sideA) * (near - depthA) / (depthB - depthA), near, ref min, ref max);
        }

        var x1 = Math.Max(0, (int)MathF.Floor(min));
        var x2 = Math.Min(_width, (int)MathF.Ceiling(max));
        for (var x = x1; x < x2; x++)
        {
            if (!_solid[x])
                return true;
        }

        return false;
    }

    private void Include(float side, float depth, ref float min, ref float max)
    {
        var x = _halfWidth + side * _halfWidth / depth;
        min = MathF.Min(min, x);
        max = MathF.Max(max, x);
    }
}
