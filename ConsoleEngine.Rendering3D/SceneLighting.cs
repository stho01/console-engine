using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// The light an <see cref="IRenderer3D"/> draws with (<see cref="IRenderer3D.Lighting"/>): an ambient, a directional
/// sun (its direction is <c>Draw</c>'s <c>toLight</c>) and up to <see cref="MaxLights"/> <see cref="PointLight"/>s.
/// A lit triangle's colour is <c>albedo · (Ambient + SunColor · max(0, n·toLight))</c>, flat, plus per pixel
/// <c>albedo · Σ light</c>. Unlit meshes ignore all of it. Allocates nothing after construction: fill it each frame.
/// </summary>
public sealed class SceneLighting
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The most lights per frame; <see cref="Add"/> ignores the rest.</summary>
    public const int MaxLights = 16;

    /// <summary>The light without a <see cref="SceneLighting"/>: ambient 0.35, sun 0.65, both white.</summary>
    public static readonly Vector3 DefaultAmbient = new(0.35f);

    /// <inheritdoc cref="DefaultAmbient"/>
    public static readonly Vector3 DefaultSunColor = new(0.65f);

    private readonly PointLight[] _lights = new PointLight[MaxLights];

    //**********************************************************
    //** props
    //**********************************************************

    public Vector3 Ambient { get; set; } = DefaultAmbient;
    public Vector3 SunColor { get; set; } = DefaultSunColor;

    public int LightCount { get; private set; }

    public ReadOnlySpan<PointLight> Lights => _lights.AsSpan(0, LightCount);

    /// <summary>Bumped whenever the lights change, so a renderer knows when to sort them into screen tiles again.</summary>
    internal int Version { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void ClearLights()
    {
        LightCount = 0;
        Version++;
    }

    /// <summary>Adds a light; false (and nothing added) when there are <see cref="MaxLights"/> already.</summary>
    public bool Add(in PointLight light)
    {
        if (LightCount == MaxLights)
            return false;

        _lights[LightCount++] = light;
        Version++;
        return true;
    }

    /// <summary>
    /// A sphere round everything <paramref name="light"/> can reach: its radius, or for a spotlight narrower than
    /// about 76° either side the smaller sphere round the cone and its cap.
    /// </summary>
    public static (Vector3 Center, float Radius) Bounds(in PointLight light)
    {
        // A cap point at the cone's edge is r·sqrt(1.25 - cos) from the middle of the axis; apex and tip are r/2.
        var coneRadius = light.Radius * MathF.Max(0.5f, MathF.Sqrt(MathF.Max(0f, 1.25f - light.ConeCosine)));
        return light.ConeCosine < -0.5f || coneRadius >= light.Radius
            ? (light.Position, light.Radius)
            : (light.Position + light.Direction * (light.Radius / 2), coneRadius);
    }

    /// <summary>
    /// What <paramref name="light"/> adds at <paramref name="position"/> on a surface facing
    /// <paramref name="normal"/>, before the albedo; the GPU shader computes the same.
    /// </summary>
    public static Vector3 Contribution(in PointLight light, Vector3 position, Vector3 normal)
    {
        var toLight = light.Position - position;
        var squared = toLight.LengthSquared();
        var fraction = Math.Clamp(1f - squared / (light.Radius * light.Radius), 0f, 1f);
        if (fraction <= 0f)
            return Vector3.Zero;

        var direction = toLight / MathF.Max(MathF.Sqrt(squared), 0.001f);
        var facing = Math.Clamp(Vector3.Dot(normal, direction), 0f, 1f);
        var cone = light.ConeCosine < -0.5f
            ? 1f
            : SmoothStep(light.ConeCosine, light.ConeCosine + PointLight.ConeSoftness,
                -Vector3.Dot(direction, light.Direction));
        return light.Color * (fraction * fraction * facing * cone);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>GLSL's <c>smoothstep</c>.</summary>
    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
