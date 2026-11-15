using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster;

/// <summary>
/// Loads a Kartster kart: a folder with one OBJ file per part. The part's file name decides its colour (the colour
/// words follow Kartster's <c>KartPartColors</c> and <c>KartPart</c>), parts without one take the player colour,
/// <c>muted</c> parts a toned-down player colour, <c>glass</c> parts are see-through, and marker parts (where
/// lights, the driver, the exhaust, jet flames or robot limbs sit) are not drawn. Nor are the parts Kartster only
/// shows on a boost (<c>on_boost</c>) or in Whosp's robot (<c>robot</c>): the port has neither.
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
        ("lamp_yellow", new Vector3(0.98f, 0.82f, 0.15f)),
        ("glass_tinted", new Vector3(0.04f, 0.05f, 0.06f)),
        ("glass", new Vector3(0.75f, 0.88f, 0.92f)),
        // Kartster draws these in the player's contrast colour, mostly white; the port has one player colour.
        ("contrast", new Vector3(0.95f)),
        // Kartster lays a picture on them; here they are a plain white decal.
        ("sticker", new Vector3(0.95f)),
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

    // Kartster's glass opacities (PrimitiveSceneRenderer.GlassOpacity, TintedGlassOpacity).
    private static readonly (string Name, float Opacity)[] Glass = [("glass_tinted", 0.65f), ("glass", 0.25f)];

    // Kartster's KartCatalog markers (prefixes), and KartRobotPose's pose and joint markers.
    private static readonly string[] Markers =
        ["seat", "eye", "wall", "exhaust", "muffler", "thrust", "light_", "radar_pivot", "pose_", "joint_"];

    // Parts Kartster only shows on a boost or in the robot (KartPart.IsOnBoost, IsRobot): anywhere in the name.
    private static readonly string[] Hidden = ["on_boost", "robot"];

    // Kartster's PrimitiveSceneRenderer.MutedSaturation and MutedBrightness.
    private const float MutedSaturation = 0.5f;
    private const float MutedBrightness = 0.6f;

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
        var opacities = new List<float>();
        var hasGlass = false;

        foreach (var file in files)
        {
            var part = Path.GetFileNameWithoutExtension(file);
            if (!IsDrawn(part))
                continue;

            ObjFile.Read(file, positions, indices);
            var color = PartColor(part, playerColor);
            var opacity = PartOpacity(part);
            hasGlass |= opacity < 1f;
            while (colors.Count < indices.Count / 3)
            {
                colors.Add(color);
                opacities.Add(opacity);
            }
        }

        if (indices.Count == 0)
            throw new InvalidDataException($"No kart parts (*.obj) in {folder}");

        return new Mesh(positions.ToArray(), indices.ToArray(), colors.ToArray())
        {
            TriangleOpacity = hasGlass ? opacities.ToArray() : null,
        };
    }

    /// <summary>Whether a part is drawn: not a marker, nor only there on a boost or in the robot.</summary>
    public static bool IsDrawn(string part)
    {
        foreach (var marker in Markers)
        {
            if (part.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var hidden in Hidden)
        {
            if (part.Contains(hidden, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>The colour a part's name asks for: its colour word's, a muted or plain player colour otherwise.</summary>
    public static Vector3 PartColor(string part, Vector3 playerColor)
    {
        foreach (var (name, color) in PartColors)
        {
            if (part.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return color;
        }

        return part.StartsWith("muted", StringComparison.OrdinalIgnoreCase) ? Muted(playerColor) : playerColor;
    }

    /// <summary>
    /// The same kart with the triangles coloured <paramref name="from"/> (the player colour it was loaded with) in
    /// <paramref name="to"/>, and the muted ones in muted <paramref name="to"/>. Shares the positions and indices, so
    /// one loaded kart serves every colour.
    /// </summary>
    public static Mesh Recolor(Mesh mesh, Vector3 from, Vector3 to)
    {
        var colors = (Vector3[])mesh.TriangleColors.Clone();
        var mutedFrom = Muted(from);
        var mutedTo = Muted(to);
        for (var t = 0; t < colors.Length; t++)
        {
            if (colors[t] == from)
                colors[t] = to;
            else if (colors[t] == mutedFrom)
                colors[t] = mutedTo;
        }

        return new Mesh(mesh.Positions, mesh.Indices, colors) { TriangleOpacity = (float[])mesh.TriangleOpacity?.Clone() };
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float PartOpacity(string part)
    {
        foreach (var (name, opacity) in Glass)
        {
            if (part.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return opacity;
        }

        return 1f;
    }

    // Kartster's GetMutedColor: pulled towards a grey as bright as itself, then darkened.
    private static Vector3 Muted(Vector3 color)
    {
        var brightness = Vector3.Dot(color, new Vector3(0.2126f, 0.7152f, 0.0722f));
        return Vector3.Lerp(new Vector3(brightness), color, MutedSaturation) * MutedBrightness;
    }
}
