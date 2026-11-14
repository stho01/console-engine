using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// Reads the geometry of a Wavefront OBJ file: <c>v</c> positions and <c>f</c> faces. Everything else (normals,
/// texture coordinates, materials, groups) is ignored. Faces with more than three corners become triangle fans.
/// </summary>
public static class ObjFile
{
    /// <summary>Appends the file's positions and triangle indices to the lists, so several files make one mesh.</summary>
    public static void Read(string path, List<Vector3> positions, List<int> indices)
    {
        var first = positions.Count;
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "v" && parts.Length >= 4)
            {
                positions.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
            }
            else if (parts[0] == "f")
            {
                if (parts.Length < 4)
                    throw new InvalidDataException($"{path}:{lineNumber}: a face needs at least three corners");

                var a = ParseIndex(parts[1], first, positions.Count);
                for (var i = 2; i + 1 < parts.Length; i++)
                {
                    indices.Add(a);
                    indices.Add(ParseIndex(parts[i], first, positions.Count));
                    indices.Add(ParseIndex(parts[i + 1], first, positions.Count));
                }
            }
        }
    }

    private static float ParseFloat(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>A corner is <c>v</c>, <c>v/vt</c>, <c>v//vn</c> or <c>v/vt/vn</c>, 1-based or negative (relative).</summary>
    private static int ParseIndex(string corner, int first, int count)
    {
        var slash = corner.IndexOf('/');
        var index = int.Parse(slash < 0 ? corner : corner[..slash], CultureInfo.InvariantCulture);
        return index < 0 ? count + index : first + index - 1;
    }
}
