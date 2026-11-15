using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D.OpenGL.Tests;

/// <summary>
/// Scenes with a <see cref="SceneLighting"/> for the GPU-vs-CPU tests, seen by their camera (z = 5, looking down -Z):
/// a floor at y = -1 reaching behind the camera (clipped at the near plane), lit by one kind of light each.
/// </summary>
internal static class LitScenes
{
    private static readonly Vector3 Gray = new(0.6f);
    private static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.3f, 1, 0.2f));

    private static readonly Mesh Floor = new(
        [new(-20, -1, 15), new(20, -1, 15), new(20, -1, -50), new(-20, -1, -50)], [0, 1, 2, 0, 2, 3], [Gray, Gray]);

    private static readonly Mesh Wall = new(
        [new(-2, -1, -1), new(2, -1, -1), new(2, 2, -1), new(-2, 2, -1)], [0, 1, 2, 0, 2, 3],
        [new Vector3(0.9f, 0.5f, 0.3f), new Vector3(0.3f, 0.5f, 0.9f)]);

    private static readonly Mesh SeeThroughUnlitQuad = new(
        [new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)], [0, 1, 2, 0, 2, 3], [Vector3.One, Vector3.One])
    {
        TriangleOpacity = [0.5f, 0.5f],
        IsUnlit = true
    };

    public static Action<IRenderer3D> PointLight(Matrix4x4 viewProjection, bool lightsOn = true) =>
        Scene(viewProjection, lightsOn, Night(new PointLight(new Vector3(0.5f, -0.4f, 1), new Vector3(1.5f), 4f)));

    public static Action<IRenderer3D> SpotLight(Matrix4x4 viewProjection, bool lightsOn = true) =>
        Scene(viewProjection, lightsOn, Night(new PointLight(new Vector3(0, -0.5f, 3), new Vector3(4f), 12f,
            Vector3.Normalize(new Vector3(0, -0.15f, -1)), 0.85f)));

    /// <summary>A red and a blue point light, a cyan spotlight, under a coloured ambient and sun.</summary>
    public static Action<IRenderer3D> ColouredLights(Matrix4x4 viewProjection, bool lightsOn = true)
    {
        var lighting = Night(
            new PointLight(new Vector3(-1, -0.5f, 0.5f), new Vector3(1.5f, 0.1f, 0.1f), 3f),
            new PointLight(new Vector3(1, 0, 0), new Vector3(0.1f, 0.2f, 2f), 3f),
            new PointLight(new Vector3(0, 1, 3), new Vector3(0, 2, 2), 10f, Vector3.Normalize(new Vector3(0, -1, -2)),
                0.9f));
        lighting.Ambient = new Vector3(0.05f, 0.1f, 0.2f);
        lighting.SunColor = new Vector3(0.3f, 0.2f, 0.1f);
        return Scene(viewProjection, lightsOn, lighting);
    }

    private static SceneLighting Night(params PointLight[] lights)
    {
        var lighting = new SceneLighting { Ambient = new Vector3(0.1f), SunColor = new Vector3(0.15f) };
        foreach (var light in lights)
            lighting.Add(light);
        return lighting;
    }

    /// <summary>
    /// The floor and a wall lit by <paramref name="lighting"/> (without its local lights unless
    /// <paramref name="lightsOn"/>), a see-through unlit quad in front.
    /// </summary>
    private static Action<IRenderer3D> Scene(Matrix4x4 viewProjection, bool lightsOn, SceneLighting lighting)
    {
        if (!lightsOn)
            lighting.ClearLights();
        return renderer => Draw(renderer, viewProjection, lighting);
    }

    private static void Draw(IRenderer3D renderer, Matrix4x4 viewProjection, SceneLighting lighting)
    {
        renderer.Lighting = lighting;
        renderer.Draw(Floor, Matrix4x4.Identity, viewProjection, ToSun);
        renderer.Draw(Wall, Matrix4x4.CreateRotationY(0.4f), viewProjection, ToSun);
        renderer.Draw(SeeThroughUnlitQuad, Matrix4x4.Identity, viewProjection, ToSun);
    }
}
