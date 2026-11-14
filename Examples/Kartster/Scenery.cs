using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster;

/// <summary>
/// What stands on a track besides the road: fences, pipes, signs, power-up boxes, the start/finish line, trees.
/// Loaded from <c>&lt;id&gt;.scenery.txt</c>, which Kartster's own placement and mesh code wrote (see
/// <c>docs/agent/03-rules.md</c>, Kartster scenery). One line per thing, <c>#</c> starts a comment:
/// <code>
/// model &lt;name&gt; x y z yawDegrees scaleX scaleY scaleZ r g b
/// tri x1 y1 z1 x2 y2 z2 x3 y3 z3 r g b
/// </code>
/// A <c>model</c> is <c>&lt;name&gt;.obj</c> from the models folder, scaled, turned counterclockwise seen from above,
/// then moved to x y z, in one colour (0..1). A <c>tri</c> is one triangle, counter-clockwise seen from its front.
/// Kartster's ground is at y = 0 and the port's sits a little lower (<see cref="Track.GroundHeight"/>), so a point at
/// y = 0 is moved down onto it: a fence or a pipe stands on the grass instead of floating.
/// </summary>
public sealed class Scenery
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Metres from the camera beyond which nothing is drawn (to the edge of its bounding sphere).</summary>
    public const float DrawDistance = 200f;

    private const float CellSize = 32f; // the triangles are cut into square cells this wide, one mesh each
    private const float OnGround = 1e-3f;

    private readonly Mesh[] _meshes;
    private readonly Matrix4x4[] _worlds;
    private readonly Vector3[] _centers; // bounding spheres, world space
    private readonly float[] _radii;
    private readonly Vector4[] _planes = new Vector4[5]; // left, right, bottom, top, near

    public static readonly Scenery Empty = new([], [], [], []);

    //**********************************************************
    //** ctor
    //**********************************************************

    private Scenery(Mesh[] meshes, Matrix4x4[] worlds, Vector3[] centers, float[] radii)
    {
        _meshes = meshes;
        _worlds = worlds;
        _centers = centers;
        _radii = radii;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>How many things are drawn separately: model instances plus triangle cells.</summary>
    public int Count => _meshes.Length;

    /// <summary>How many of them are model instances (they come first).</summary>
    public int ModelCount { get; private init; }

    public int TriangleCount { get; private init; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Loads a scenery file; its models come from <paramref name="modelsFolder"/>.</summary>
    public static Scenery Load(string path, string modelsFolder, float groundHeight) =>
        Parse(File.ReadLines(path), Path.GetFileName(path), modelsFolder, groundHeight);

    /// <param name="source">The file name, for error messages.</param>
    /// <exception cref="InvalidDataException">A line is not a model or tri line, or its model is missing.</exception>
    public static Scenery Parse(IEnumerable<string> lines, string source, string modelsFolder, float groundHeight)
    {
        var models = new Dictionary<string, Model>();
        var meshes = new Dictionary<(string, Vector3), Mesh>();
        var instances = new List<(Mesh Mesh, Matrix4x4 World, Vector3 Center, float Radius)>();
        var cells = new Dictionary<(int, int), Cell>();
        var triangles = 0;
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            var hash = line.IndexOf('#');
            var parts = (hash < 0 ? line : line[..hash]).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "model")
            {
                if (parts.Length != 12)
                    throw Error(source, lineNumber, "expected 'model <name> x y z yawDegrees scaleX scaleY scaleZ r g b'");

                var name = parts[1];
                var position = Vector(parts, 2, source, lineNumber);
                var yaw = Number(parts[5], source, lineNumber) * MathF.PI / 180f;
                var scale = Vector(parts, 6, source, lineNumber);
                var color = Vector(parts, 9, source, lineNumber);
                if (MathF.Abs(position.Y) < OnGround)
                    position.Y = groundHeight;

                if (!models.TryGetValue(name, out var model))
                {
                    model = Model.Load(Path.Combine(modelsFolder, name + ".obj"), source, lineNumber);
                    models[name] = model;
                }

                if (!meshes.TryGetValue((name, color), out var mesh))
                {
                    var colors = new Vector3[model.Indices.Length / 3];
                    Array.Fill(colors, color);
                    mesh = new Mesh(model.Positions, model.Indices, colors);
                    meshes[(name, color)] = mesh;
                }

                var world = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw) *
                            Matrix4x4.CreateTranslation(position);
                var largest = MathF.Max(MathF.Abs(scale.X), MathF.Max(MathF.Abs(scale.Y), MathF.Abs(scale.Z)));
                instances.Add((mesh, world, Vector3.Transform(model.Center, world), model.Radius * largest));
            }
            else if (parts[0] == "tri")
            {
                if (parts.Length != 13)
                    throw Error(source, lineNumber, "expected 'tri x1 y1 z1 x2 y2 z2 x3 y3 z3 r g b'");

                var a = Grounded(Vector(parts, 1, source, lineNumber), groundHeight);
                var b = Grounded(Vector(parts, 4, source, lineNumber), groundHeight);
                var c = Grounded(Vector(parts, 7, source, lineNumber), groundHeight);
                var middle = (a + b + c) / 3;
                var key = ((int)MathF.Floor(middle.X / CellSize), (int)MathF.Floor(middle.Z / CellSize));
                if (!cells.TryGetValue(key, out var cell))
                {
                    cell = new Cell();
                    cells[key] = cell;
                }

                cell.Add(a, b, c, Vector(parts, 10, source, lineNumber));
                triangles++;
            }
            else
            {
                throw Error(source, lineNumber, $"unknown line '{parts[0]}', expected model or tri");
            }
        }

        var modelCount = instances.Count;
        foreach (var cell in cells.Values)
        {
            var (center, radius) = Bounds(cell.Positions);
            instances.Add((new Mesh(cell.Positions.ToArray(), cell.Indices.ToArray(), cell.Colors.ToArray()),
                Matrix4x4.Identity, center, radius));
        }

        var count = instances.Count;
        var scenery = new Scenery(new Mesh[count], new Matrix4x4[count], new Vector3[count], new float[count])
        {
            ModelCount = modelCount,
            TriangleCount = triangles
        };
        for (var i = 0; i < count; i++)
        {
            (scenery._meshes[i], scenery._worlds[i], scenery._centers[i], scenery._radii[i]) = instances[i];
        }

        return scenery;
    }

    /// <summary>
    /// Draws everything that is within <paramref name="drawDistance"/> of <paramref name="eye"/> and inside the view
    /// (bounding sphere against the frustum's sides and near plane). Allocates nothing.
    /// </summary>
    /// <returns>How many meshes were drawn.</returns>
    public int Draw(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 eye, Vector3 toLight,
        float drawDistance = DrawDistance)
    {
        // Gribb-Hartmann: the planes come straight out of the matrix (row vectors, depth 0..1).
        var m = viewProjection;
        _planes[0] = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
        _planes[1] = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        _planes[2] = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
        _planes[3] = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        _planes[4] = new Vector4(m.M13, m.M23, m.M33, m.M43);
        for (var p = 0; p < _planes.Length; p++)
        {
            var plane = _planes[p];
            _planes[p] = plane / new Vector3(plane.X, plane.Y, plane.Z).Length();
        }

        var drawn = 0;
        for (var i = 0; i < _meshes.Length; i++)
        {
            var center = _centers[i];
            var radius = _radii[i];
            var reach = drawDistance + radius;
            if (Vector3.DistanceSquared(center, eye) > reach * reach || !IsInView(center, radius))
                continue;

            renderer.Draw(_meshes[i], _worlds[i], viewProjection, toLight);
            drawn++;
        }

        return drawn;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool IsInView(Vector3 center, float radius)
    {
        var point = new Vector4(center, 1f);
        for (var p = 0; p < _planes.Length; p++)
        {
            if (Vector4.Dot(_planes[p], point) < -radius)
                return false;
        }

        return true;
    }

    private static Vector3 Grounded(Vector3 point, float groundHeight) =>
        MathF.Abs(point.Y) < OnGround ? point with { Y = groundHeight } : point;

    private static (Vector3 Center, float Radius) Bounds(List<Vector3> positions)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return ((min + max) / 2, Vector3.Distance(min, max) / 2);
    }

    private static Vector3 Vector(string[] parts, int first, string source, int lineNumber) => new(
        Number(parts[first], source, lineNumber),
        Number(parts[first + 1], source, lineNumber),
        Number(parts[first + 2], source, lineNumber));

    private static float Number(string text, string source, int lineNumber) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Error(source, lineNumber, $"'{text}' is not a number");

    private static InvalidDataException Error(string source, int lineNumber, string message) =>
        new($"{source}:{lineNumber}: {message}");

    /// <summary>An OBJ model and its bounding sphere, in model space.</summary>
    private sealed record Model(Vector3[] Positions, int[] Indices, Vector3 Center, float Radius)
    {
        public static Model Load(string path, string source, int lineNumber)
        {
            if (!File.Exists(path))
                throw Error(source, lineNumber, $"model file {Path.GetFullPath(path)} not found");

            var positions = new List<Vector3>();
            var indices = new List<int>();
            ObjFile.Read(path, positions, indices);
            var (center, radius) = Bounds(positions);
            return new Model(positions.ToArray(), indices.ToArray(), center, radius);
        }
    }

    /// <summary>The triangles of one square cell, gathered into one mesh.</summary>
    private sealed class Cell
    {
        public readonly List<Vector3> Positions = [];
        public readonly List<int> Indices = [];
        public readonly List<Vector3> Colors = [];

        public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 color)
        {
            Indices.Add(Positions.Count);
            Indices.Add(Positions.Count + 1);
            Indices.Add(Positions.Count + 2);
            Positions.Add(a);
            Positions.Add(b);
            Positions.Add(c);
            Colors.Add(color);
        }
    }
}
