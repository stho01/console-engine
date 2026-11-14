using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using KartViewer.Rendering;

namespace KartViewer;

/// <summary>
/// Loads a Kartster kart: a folder with one OBJ file per part. The part's file name decides its colour (the colour
/// words follow Kartster's <c>KartPartColors</c>), parts without one take the player colour, and marker parts
/// (where lights, the driver or the exhaust sit) are not drawn.
/// </summary>
public static class KartModel
{
    //**********************************************************
    //** fields
    //**********************************************************

    private static readonly (string Name, Vector3 Color)[] PartColors =
    [
        ("wheel", new Vector3(0.08f)), // tyres
        ("dark", new Vector3(0.12f)),
        ("lamp_red", new Vector3(0.9f, 0.1f, 0.08f)),
        ("lamp_white", new Vector3(1f, 1f, 0.9f)),
        ("chrome", new Vector3(0.78f, 0.8f, 0.84f)),
        ("white", new Vector3(0.95f)),
        ("brass", new Vector3(0.8f, 0.62f, 0.24f)),
        ("copper", new Vector3(0.72f, 0.4f, 0.22f)),
        ("bronze", new Vector3(0.7f, 0.46f, 0.24f)),
        ("red", new Vector3(0.68f, 0.07f, 0.06f)),
        ("leather", new Vector3(0.25f, 0.14f, 0.08f)),
        ("gray", new Vector3(0.42f, 0.43f, 0.45f)),
        ("wood", new Vector3(0.8f, 0.63f, 0.4f)),
        ("osb", new Vector3(0.66f, 0.5f, 0.3f)),
        ("bread", new Vector3(0.82f, 0.58f, 0.3f)),
        ("sesame", new Vector3(0.95f, 0.9f, 0.72f)),
        ("sausage", new Vector3(0.42f, 0.17f, 0.08f)),
        ("char", new Vector3(0.13f, 0.06f, 0.04f)),
        ("mustard", new Vector3(0.86f, 0.66f, 0.06f)),
        ("yellow", new Vector3(0.98f, 0.82f, 0.15f)),
        ("brown", new Vector3(0.3f, 0.17f, 0.07f)),
        ("blue", new Vector3(0.1f, 0.3f, 0.75f)),
        ("ruby", new Vector3(0.95f, 0.04f, 0.14f)),
        ("pink", new Vector3(0.92f, 0.6f, 0.68f)),
        ("green", new Vector3(0.15f, 0.45f, 0.2f)),
        ("amber", new Vector3(1f, 0.5f, 0.05f)),
        ("steel", new Vector3(0.62f, 0.64f, 0.66f)),
        ("lemon", new Vector3(1f, 0.78f, 0.08f)),
        ("enamel", new Vector3(0.93f, 0.93f, 0.9f)),
        ("rocket", new Vector3(0.93f, 0.16f, 0.08f)),
        ("gunmetal", new Vector3(0.2f, 0.21f, 0.23f))
    ];

    private static readonly string[] Markers = ["seat", "eye", "wall", "exhaust", "light_"];

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Mesh Load(string folder, Vector3 playerColor)
    {
        var files = Directory.GetFiles(folder, "*.obj");
        Array.Sort(files, StringComparer.Ordinal);

        var positions = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Vector3>();

        foreach (var file in files)
        {
            var part = Path.GetFileNameWithoutExtension(file);
            if (IsMarker(part))
                continue;

            ObjFile.Read(file, positions, indices);
            var color = PartColor(part, playerColor);
            while (colors.Count < indices.Count / 3)
                colors.Add(color);
        }

        if (indices.Count == 0)
            throw new InvalidDataException($"No kart parts (*.obj) in {folder}");

        return new Mesh(positions.ToArray(), indices.ToArray(), colors.ToArray());
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool IsMarker(string part)
    {
        foreach (var marker in Markers)
        {
            if (part.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Vector3 PartColor(string part, Vector3 playerColor)
    {
        foreach (var (name, color) in PartColors)
        {
            if (part.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return color;
        }

        return playerColor;
    }
}
