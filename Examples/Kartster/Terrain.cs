using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster;

/// <summary>
/// The hilly ground beside a track's road, as Kartster's <c>TrackTerrain</c>: heights on a square grid, none where
/// there is no ground (a band along the road). Loaded from <c>&lt;id&gt;.terrain.txt</c>, which the original's own
/// terrain wrote (<c>tools/KartsterExport</c>, <c>TerrainExporter</c>):
/// <code>
/// terrain originX originZ cellSize columns rows r g b
/// h h h …   one line per row, west to east (X grows), rows in Z order, NaN where there is no ground
/// </code>
/// The height at column c, row r is at X = originX + c·cellSize, Z = originZ + r·cellSize. Driven on as the original
/// does (<see cref="TryGetHeight"/>), drawn as two triangles per cell split as the original's <c>TrackTerrainMesh</c>,
/// in tiles of 16 × 16 cells culled like the scenery.
/// </summary>
public sealed class Terrain
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int TileCells = 16;
    private const float OtherShade = 1.12f; // every other cell a little lighter, so speed shows on the grass

    private readonly float[] _heights;

    //**********************************************************
    //** ctor
    //**********************************************************

    private Terrain(float originX, float originZ, float cellSize, int columns, int rows, float[] heights, Vector3 color)
    {
        OriginX = originX;
        OriginZ = originZ;
        CellSize = cellSize;
        Columns = columns;
        Rows = rows;
        _heights = heights;
        MinHeight = float.MaxValue;
        MaxHeight = float.MinValue;
        foreach (var height in heights)
        {
            if (float.IsNaN(height))
                continue;
            MinHeight = MathF.Min(MinHeight, height);
            MaxHeight = MathF.Max(MaxHeight, height);
        }

        Tiles = Scenery.FromMeshes(BuildTiles(color));
    }

    //**********************************************************
    //** props
    //**********************************************************

    public float OriginX { get; }
    public float OriginZ { get; }
    public float CellSize { get; }
    public int Columns { get; }
    public int Rows { get; }
    public float MinHeight { get; }
    public float MaxHeight { get; }

    /// <summary>The tiles to draw, culled per bounding sphere (<see cref="Scenery.Draw"/>).</summary>
    public Scenery Tiles { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Terrain Load(string path) => Parse(File.ReadLines(path), Path.GetFileName(path));

    /// <param name="source">The file name, for error messages.</param>
    /// <exception cref="InvalidDataException">The header or a row is malformed.</exception>
    public static Terrain Parse(IEnumerable<string> lines, string source)
    {
        float originX = 0, originZ = 0, cellSize = 0;
        int columns = 0, rows = 0, row = 0, lineNumber = 0;
        var color = Vector3.Zero;
        float[] heights = null;

        foreach (var line in lines)
        {
            lineNumber++;
            var hash = line.IndexOf('#');
            var parts = (hash < 0 ? line : line[..hash]).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (heights == null)
            {
                if (parts.Length != 9 || parts[0] != "terrain")
                    throw Error(source, lineNumber, "expected 'terrain originX originZ cellSize columns rows r g b'");
                originX = Number(parts[1], source, lineNumber);
                originZ = Number(parts[2], source, lineNumber);
                cellSize = Number(parts[3], source, lineNumber);
                columns = (int)Number(parts[4], source, lineNumber);
                rows = (int)Number(parts[5], source, lineNumber);
                color = new Vector3(Number(parts[6], source, lineNumber), Number(parts[7], source, lineNumber),
                    Number(parts[8], source, lineNumber));
                if (cellSize <= 0 || columns < 2 || rows < 2)
                    throw Error(source, lineNumber, "needs a cell size above 0 and at least 2 columns and rows");
                heights = new float[columns * rows];
                continue;
            }

            if (row >= rows)
                throw Error(source, lineNumber, $"more than {rows} rows");
            if (parts.Length != columns)
                throw Error(source, lineNumber, $"expected {columns} heights, found {parts.Length}");
            for (var column = 0; column < columns; column++)
                heights[row * columns + column] = Number(parts[column], source, lineNumber);
            row++;
        }

        if (heights == null || row != rows)
            throw new InvalidDataException($"{source}: expected a terrain header and {rows} rows, found {row}");

        return new Terrain(originX, originZ, cellSize, columns, rows, heights, color);
    }

    /// <summary>
    /// The ground's height and up direction under (x, z), blended between the four grid points round it, as the
    /// original's <c>TrackTerrain.TryGetHeight</c> (smooth terrain). False where any of them has no ground.
    /// </summary>
    public bool TryGetHeight(float x, float z, out float height, out Vector3 normal)
    {
        var column = (x - OriginX) / CellSize;
        var row = (z - OriginZ) / CellSize;
        var left = (int)MathF.Floor(column);
        var top = (int)MathF.Floor(row);
        height = 0f;
        normal = Vector3.UnitY;
        if (left < 0 || top < 0 || left + 1 >= Columns || top + 1 >= Rows)
            return false;

        var a = _heights[top * Columns + left];
        var b = _heights[top * Columns + left + 1];
        var c = _heights[(top + 1) * Columns + left];
        var d = _heights[(top + 1) * Columns + left + 1];
        if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d))
            return false;

        var u = column - left;
        var v = row - top;
        height = float.Lerp(float.Lerp(a, b, u), float.Lerp(c, d, u), v);
        var slopeX = float.Lerp(b - a, d - c, v) / CellSize;
        var slopeZ = float.Lerp(c - a, d - b, u) / CellSize;
        normal = Vector3.Normalize(new Vector3(-slopeX, 1f, -slopeZ));
        return true;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Mesh[] BuildTiles(Vector3 color)
    {
        var other = Vector3.Min(color * OtherShade, Vector3.One);
        var tiles = new List<Mesh>();
        var positions = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Vector3>();
        for (var tileRow = 0; tileRow < Rows - 1; tileRow += TileCells)
        for (var tileColumn = 0; tileColumn < Columns - 1; tileColumn += TileCells)
        {
            positions.Clear();
            indices.Clear();
            colors.Clear();
            for (var row = tileRow; row < Math.Min(tileRow + TileCells, Rows - 1); row++)
            for (var column = tileColumn; column < Math.Min(tileColumn + TileCells, Columns - 1); column++)
            {
                var a = Corner(column, row);
                var b = Corner(column + 1, row);
                var c = Corner(column, row + 1);
                var d = Corner(column + 1, row + 1);
                if (float.IsNaN(a.Y) || float.IsNaN(b.Y) || float.IsNaN(c.Y) || float.IsNaN(d.Y))
                    continue;

                // Split from b to c as the original; both counter-clockwise seen from above.
                var first = positions.Count;
                positions.Add(a);
                positions.Add(b);
                positions.Add(c);
                positions.Add(d);
                indices.AddRange([first, first + 2, first + 1, first + 1, first + 2, first + 3]);
                var shade = (row + column) % 2 == 0 ? color : other;
                colors.Add(shade);
                colors.Add(shade);
            }

            if (indices.Count > 0)
                tiles.Add(new Mesh(positions.ToArray(), indices.ToArray(), colors.ToArray()));
        }

        return tiles.ToArray();
    }

    private Vector3 Corner(int column, int row) =>
        new(OriginX + column * CellSize, _heights[row * Columns + column], OriginZ + row * CellSize);

    private static float Number(string text, string source, int lineNumber) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Error(source, lineNumber, $"'{text}' is not a number");

    private static InvalidDataException Error(string source, int lineNumber, string message) =>
        new($"{source}:{lineNumber}: {message}");
}
