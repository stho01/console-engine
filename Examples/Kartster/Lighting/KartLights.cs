using System;
using System.Numerics;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Rendering3D;

namespace Kartster.Lighting;

/// <summary>
/// The race's light and mood, after Kartster's <c>SceneLighting</c>, <c>SceneLights</c> and
/// <c>BlobShadowRenderer</c>: a blob shadow under every kart, and at night (N toggles; on by default on Kartster's
/// night tracks, <see cref="NightTracks"/>) a dark sky, a dim moon, and every kart's headlights, long-range and rear
/// lights (<see cref="KartLamps"/>) as <see cref="PointLight"/>s, nearest the camera first, with glowing lenses. By
/// day lamps are off, as in Kartster. A frame: <see cref="Begin"/> (after <c>Clear</c>, before anything is drawn),
/// <see cref="DrawShadows"/> (after the ground), then <see cref="DrawLenses"/> (after the karts).
/// </summary>
public sealed class KartLights
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's tracks with <c>Mood = TrackMood.Night</c> (<c>PrismLoopFactory</c>).</summary>
    // shortcut: track ids, not exported moods; export the mood in tools/KartsterExport when more night tracks come.
    public static readonly string[] NightTracks = ["prism-loop"];

    /// <summary>Night's ambient and moon: Kartster's dim blue <c>SceneLighting.Night</c>, lifted for 16 colours.</summary>
    public static readonly Vector3 NightAmbient = new(0.12f, 0.13f, 0.2f);
    public static readonly Vector3 MoonColor = new(0.16f, 0.18f, 0.3f);

    private static readonly Vector3 NightSkyTop = new(0.02f, 0.03f, 0.08f);
    private static readonly Vector3 NightSkyHorizon = new(0.08f, 0.1f, 0.2f);

    // Kartster's SceneLights: brightness, reach (radius), cone cosine and how far the beam drops per metre ahead.
    private static readonly Vector3 HeadlightColor = new Vector3(1f, 0.95f, 0.8f) * 6f;
    private const float HeadlightRadius = 30f;
    private const float HeadlightCone = 0.85f;
    private const float HeadlightDrop = 0.12f;
    private static readonly Vector3 LongLightColor = new Vector3(1f, 0.95f, 0.8f) * 7f;
    private const float LongLightRadius = 42f;
    private const float LongLightCone = 0.945f;
    private const float LongLightDrop = 0.07f;
    private static readonly Vector3 RearLightColor = new Vector3(1f, 0.05f, 0.05f) * 0.5f;
    private const float RearLightRadius = 4f;

    // Kartster's blob: about a third bigger than the kart, darkest across the middle, soft at the rim.
    private const float ShadowLength = 1.3f;
    private const float ShadowWidth = 1.4f;
    private const float ShadowLift = 0.04f;
    private const float ShadowOpacity = 0.5f;
    private const float ShadowRimOpacity = 0.25f;
    private const float ShadowFlatRadius = 0.8f;
    private const int ShadowSegments = 16;

    private static readonly Mesh Shadow = CreateShadow();

    private readonly Func<Key, bool> _isPressed;
    private readonly SceneLighting _lighting = new() { Ambient = NightAmbient, SunColor = MoonColor };
    private readonly KartLamps[] _lamps; // by kart index; null: no lamps found
    private readonly Matrix4x4[] _shadowScales; // by kart index: the blob's size and place in kart space
    private readonly int[] _order; // kart indices, nearest the camera first
    private readonly float[] _distances;
    private readonly ConsoleColor[] _nightSky;
    private readonly uint[] _nightSkyRgb;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="kartMeshes">One per kart, by kart index (<c>KartModel</c> meshes).</param>
    /// <param name="kartFolders">The kart folders the meshes may come from; each mesh finds its own by its lenses.</param>
    /// <param name="isPressed">Whether a key was pressed this frame; null reads <c>Input.Instance</c>.</param>
    public KartLights(Mesh[] kartMeshes, string[] kartFolders, string trackName, int width, int height,
        Func<Key, bool> isPressed = null)
    {
        _isPressed = isPressed ?? (key => Input.Instance.GetKey(key).Pressed);
        Night = Array.IndexOf(NightTracks, trackName) >= 0;

        var folders = Array.ConvertAll(kartFolders, KartLamps.Load);
        _lamps = Array.ConvertAll(kartMeshes, mesh => KartLamps.Find(mesh, folders));
        _shadowScales = Array.ConvertAll(kartMeshes, ShadowScale);
        _order = new int[kartMeshes.Length];
        _distances = new float[kartMeshes.Length];

        _nightSky = new ConsoleColor[width * height];
        _nightSkyRgb = new uint[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var sky = Vector3.Lerp(NightSkyTop, NightSkyHorizon, (float)y / height);
            _nightSky[y * width + x] = KartsterGame.ScenePalette.ToConsoleColor(sky, x, y);
            var rgb = sky * 255f + new Vector3(0.5f);
            _nightSkyRgb[y * width + x] = ((uint)rgb.X << 16) | ((uint)rgb.Y << 8) | (uint)rgb.Z;
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public bool Night { get; set; }

    /// <summary>The kart's lamps by kart index; null where none were found.</summary>
    public ReadOnlySpan<KartLamps> Lamps => _lamps;

    /// <summary>This frame's light at night, null by day (what <see cref="Begin"/> gave the renderer).</summary>
    public SceneLighting Lighting => Night ? _lighting : null;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// N toggles night. At night: the night sky in place of the day's (a second <c>Clear</c>), the moon and the
    /// karts' lights (<paramref name="kartWorlds"/>, by kart index) nearest <paramref name="eye"/> first, up to
    /// <see cref="SceneLighting.MaxLights"/>.
    /// </summary>
    public void Begin(IRenderer3D renderer, ReadOnlySpan<Matrix4x4> kartWorlds, Vector3 eye)
    {
        if (_isPressed(Key.N))
            Night = !Night;
        if (!Night)
            return;

        if (renderer.TrueColor)
            renderer.Clear(_nightSkyRgb);
        else
            renderer.Clear(_nightSky);
        GatherLights(kartWorlds, eye);
        renderer.Lighting = _lighting;
    }

    /// <summary>A dark translucent disc under each kart. Draw it after the ground (the karts before or after).</summary>
    public void DrawShadows(IRenderer3D renderer, Matrix4x4 viewProjection, ReadOnlySpan<Matrix4x4> kartWorlds)
    {
        for (var i = 0; i < kartWorlds.Length; i++)
            renderer.Draw(Shadow, _shadowScales[i] * kartWorlds[i], viewProjection, Vector3.UnitY);
    }

    /// <summary>At night, the karts' lenses, glowing. Draw them after the karts.</summary>
    public void DrawLenses(IRenderer3D renderer, Matrix4x4 viewProjection, ReadOnlySpan<Matrix4x4> kartWorlds)
    {
        if (!Night)
            return;

        for (var i = 0; i < kartWorlds.Length; i++)
        {
            if (_lamps[i]?.Lenses is { } lenses)
                renderer.Draw(lenses, kartWorlds[i], viewProjection, Vector3.UnitY);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void GatherLights(ReadOnlySpan<Matrix4x4> kartWorlds, Vector3 eye)
    {
        // Insertion sort by distance: a handful of karts, no allocation.
        for (var i = 0; i < kartWorlds.Length; i++)
        {
            var distance = Vector3.DistanceSquared(kartWorlds[i].Translation, eye);
            var at = i;
            for (; at > 0 && _distances[at - 1] > distance; at--)
            {
                _distances[at] = _distances[at - 1];
                _order[at] = _order[at - 1];
            }

            _distances[at] = distance;
            _order[at] = i;
        }

        _lighting.ClearLights();
        for (var i = 0; i < kartWorlds.Length; i++)
        {
            var kart = _order[i];
            if (_lamps[kart] is not { } lamps)
                continue;

            var world = kartWorlds[kart];
            AddBeams(lamps.Front, world, HeadlightDrop, HeadlightColor, HeadlightRadius, HeadlightCone);
            AddBeams(lamps.LongRange, world, LongLightDrop, LongLightColor, LongLightRadius, LongLightCone);
            foreach (var point in lamps.Rear)
                _lighting.Add(new PointLight(Vector3.Transform(point, world), RearLightColor, RearLightRadius));
        }
    }

    /// <summary>A cone from each point, along the kart's forward (-Z) tipped down by <paramref name="drop"/>.</summary>
    private void AddBeams(Vector3[] points, Matrix4x4 world, float drop, Vector3 color, float radius, float cone)
    {
        var beam = Vector3.Normalize(Vector3.TransformNormal(new Vector3(0f, -drop, -1f), world));
        foreach (var point in points)
            _lighting.Add(new PointLight(Vector3.Transform(point, world), color, radius, beam, cone));
    }

    /// <summary>The unit disc scaled to the kart's footprint (x/z bounds), centred under it.</summary>
    private static Matrix4x4 ShadowScale(Mesh kart)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in kart.Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        var center = (min + max) / 2;
        return Matrix4x4.CreateScale((max.X - min.X) / 2 * ShadowWidth, 1f, (max.Z - min.Z) / 2 * ShadowLength)
               * Matrix4x4.CreateTranslation(center.X, ShadowLift, center.Z);
    }

    /// <summary>
    /// A unit disc on y = 0 facing up, black and unlit: a fan out to <see cref="ShadowFlatRadius"/> at
    /// <see cref="ShadowOpacity"/>, then a ring to the rim at <see cref="ShadowRimOpacity"/>.
    /// </summary>
    private static Mesh CreateShadow()
    {
        var positions = new Vector3[1 + 2 * ShadowSegments];
        var indices = new int[ShadowSegments * 9];
        var opacity = new float[ShadowSegments * 3];
        for (var s = 0; s < ShadowSegments; s++)
        {
            var (sin, cos) = MathF.SinCos(s * 2 * MathF.PI / ShadowSegments);
            positions[1 + s] = new Vector3(sin, 0, cos) * ShadowFlatRadius;
            positions[1 + ShadowSegments + s] = new Vector3(sin, 0, cos);
        }

        for (var s = 0; s < ShadowSegments; s++)
        {
            var next = (s + 1) % ShadowSegments;
            var (inner, innerNext) = (1 + s, 1 + next);
            var (outer, outerNext) = (inner + ShadowSegments, innerNext + ShadowSegments);
            // The angle grows from +Z towards +X: counter-clockwise seen from above, so these face up.
            ReadOnlySpan<int> triangles = [0, inner, innerNext, inner, outer, outerNext, inner, outerNext, innerNext];
            triangles.CopyTo(indices.AsSpan(s * 9));
            opacity[s * 3] = ShadowOpacity;
            opacity[s * 3 + 1] = opacity[s * 3 + 2] = ShadowRimOpacity;
        }

        return new Mesh(positions, indices, new Vector3[ShadowSegments * 3])
        {
            TriangleOpacity = opacity,
            IsUnlit = true
        };
    }
}
