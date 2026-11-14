using System;
using System.Diagnostics;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.OpenGL.Tests;

/// <summary>
/// Renders the CPU tests' scenes with <see cref="Rasterizer"/> and <see cref="GlRenderer"/> and compares the pixels.
/// Only edge pixels may differ (the two rasterizers' fill rules). Ignored where OpenGL 3.3 is missing (CI).
/// </summary>
[Category("Gpu")]
public class GlRendererTests
{
    private const int Size = 64;
    private const double MinAgreement = 0.97;
    private static readonly Vector3 White = Vector3.One;
    private static readonly Vector3 Black = Vector3.Zero;

    // Camera at z = 5 looking down -Z, so a triangle at z = 0 counter-clockwise in x/y faces it.
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    private static readonly ConsoleColor[] BlackBackground = new ConsoleColor[Size * Size];

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Draw_FrontFacingTriangle_MatchesCpu()
    {
        var gpu = Compare(r => r.Draw(Triangle(0, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(Center(gpu), Is.Not.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_BackFacingTriangle_IsCulledLikeCpu()
    {
        var gpu = Compare(r => r.Draw(Triangle(0, White, clockwise: true), Matrix4x4.Identity, ViewProjection,
            Vector3.UnitZ));

        Assert.That(Center(gpu), Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_TriangleBehindCamera_IsNotDrawnLikeCpu()
    {
        var gpu = Compare(r => r.Draw(Triangle(10, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(Center(gpu), Is.EqualTo(ConsoleColor.Black));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Draw_TwoOverlappingTriangles_NearerOneWinsLikeCpu(bool nearFirst)
    {
        var near = Triangle(1, White);
        var far = Triangle(-1, Black);

        var gpu = Compare(r =>
        {
            r.Draw(nearFirst ? near : far, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(nearFirst ? far : near, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });

        Assert.That(Center(gpu), Is.Not.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_FloorReachingBehindCamera_IsClippedLikeCpu()
    {
        Vector3[] positions = [new(-20, -1, 15), new(20, -1, 15), new(20, -1, -50), new(-20, -1, -50)];
        var floor = new Mesh(positions, [0, 1, 2, 0, 2, 3], [White, White]);

        var gpu = Compare(r => r.Draw(floor, Matrix4x4.Identity, ViewProjection, Vector3.UnitY));

        Assert.That(gpu.Colors[(Size - 1) * Size + Size / 2], Is.Not.EqualTo(ConsoleColor.Black));
        Assert.That(gpu.Colors[Size / 2], Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_ChequeredFloor_MatchesCpu()
    {
        var floor = Mesh.CreateFloor(40, 8, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        var world = Matrix4x4.CreateRotationY(0.3f) * Matrix4x4.CreateTranslation(0, -1, 0);

        Compare(r => r.Draw(floor, world, ViewProjection, Vector3.Normalize(new Vector3(0.3f, 1, 0.2f))));
    }

    [TestCase(0.25f)]
    [TestCase(0.5f)]
    public void Draw_SeeThroughQuad_DrawsTheSamePixelsAsCpu(float opacity)
    {
        var gpu = Compare(r => r.Draw(Quad(opacity, unlit: true), Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ));

        Assert.That(Array.FindAll(gpu.Colors, c => c != ConsoleColor.Black), Is.Not.Empty);
    }

    [Test]
    public void Draw_UnlitQuad_IgnoresTheLightLikeCpu()
    {
        var gpu = Compare(r => r.Draw(Quad(1f, unlit: true), Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ));

        Assert.That(Center(gpu), Is.EqualTo(ConsoleColor.White));
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void Draw_GrayTriangleLitFromFrontOrBehind_ShadesLikeCpu(float lightZ)
    {
        Compare(r => r.Draw(Triangle(0, new Vector3(0.8f)), Matrix4x4.Identity, ViewProjection,
            new Vector3(0, 0, lightZ)));
    }

    [Test]
    public void Draw_GradientOfColours_DithersLikeCpu()
    {
        // Every pixel of a big lit quad goes through the dither and the palette lookup with a different colour.
        Vector3[] positions = [new(-5, -5, 0), new(5, -5, 0), new(5, 5, 0), new(-5, 5, 0)];
        var quad = new Mesh(positions, [0, 1, 2, 0, 2, 3], [new Vector3(0.9f, 0.4f, 0.1f), new Vector3(0.1f, 0.5f, 0.8f)]);
        var world = Matrix4x4.CreateRotationY(0.6f) * Matrix4x4.CreateRotationX(0.3f);

        Compare(r => r.Draw(quad, world, ViewProjection, Vector3.Normalize(new Vector3(0.5f, 0.5f, 1))));
    }

    [Test]
    public void Draw_MeshChangedWithMarkChanged_ShowsTheChange()
    {
        using var gpu = CreateOrIgnore();
        using var cpu = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        var triangle = Triangle(0, White);
        Render(gpu, r => r.Draw(triangle, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ), BlackBackground);

        triangle.TriangleColors[0] = new Vector3(0.9f, 0.1f, 0.1f);
        triangle.Positions[2] = new Vector3(0, 1, 0);
        triangle.MarkChanged();
        Render(cpu, r => r.Draw(triangle, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ), BlackBackground);
        Render(gpu, r => r.Draw(triangle, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ), BlackBackground);

        AssertAgree(cpu, gpu);
        Assert.That(Center(gpu), Is.EqualTo(ConsoleColor.Red).Or.EqualTo(ConsoleColor.DarkRed));
    }

    [Test]
    public void Clear_Background_ShowsWhereNothingIsDrawn()
    {
        var background = new ConsoleColor[Size * Size];
        for (var i = 0; i < background.Length; i++)
            background[i] = (ConsoleColor)(i / Size % 16);

        var gpu = Compare(r => r.Draw(Triangle(0, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ),
            background);

        Assert.That(gpu.Colors[0], Is.EqualTo(background[0]));
        Assert.That(gpu.Colors[Size * Size - 1], Is.EqualTo(background[Size * Size - 1]));
    }

    [Test]
    public void DrawAndFinish_InSteadyState_DoesNotAllocate()
    {
        const int width = 320;
        const int height = 180;
        using var gpu = CreateOrIgnore(width, height);
        var floor = Mesh.CreateFloor(80, 40, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        var triangle = Triangle(0, White);
        var background = new ConsoleColor[width * height];
        var viewProjection = Matrix4x4.CreateLookAt(new Vector3(0, 3, 10), Vector3.Zero, Vector3.UnitY)
                             * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 16f / 9, 0.1f, 200);
        for (var i = 0; i < 20; i++)
            Frame();

        const int frames = 200;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < frames; i++)
            Frame();
        var elapsed = Stopwatch.GetElapsedTime(start);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        TestContext.Out.WriteLine($"{floor.TriangleCount + 1} triangles at {width}x{height}: " +
                                  $"{elapsed.TotalMilliseconds / frames:F3} ms per frame");
        Assert.That(allocated, Is.Zero);

        void Frame()
        {
            gpu.Clear(background);
            gpu.Draw(floor, Matrix4x4.Identity, viewProjection, Vector3.UnitY);
            gpu.Draw(triangle, Matrix4x4.Identity, viewProjection, Vector3.UnitZ);
            gpu.Finish();
        }
    }

    [Test]
    public void Constructor_EmptyFramebuffer_ThrowsSoCallersCanFallBack()
    {
        CreateOrIgnore().Dispose();

        var exception = Assert.Throws<InvalidOperationException>(() => new GlRenderer(0, 0, ConsolePalette.Campbell));

        TestContext.Out.WriteLine(exception.Message);
        Assert.That(exception.Message, Does.StartWith("OpenGL 3.3 is not available: "));
        Assert.That(GlRenderer.TryCreate(0, 0, ConsolePalette.Campbell, out _), Is.False);
    }

    [Test]
    public void Dispose_ThenCreateAgain_StillRenders()
    {
        CreateOrIgnore().Dispose();

        Compare(r => r.Draw(Triangle(0, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));
    }

    [Test]
    public void Draw_PointLight_LightsLikeCpu() => Compare(LitScenes.PointLight(ViewProjection));

    [Test]
    public void Draw_SpotLight_LightsLikeCpu() => Compare(LitScenes.SpotLight(ViewProjection));

    [Test]
    public void Draw_ColouredLightsAmbientAndSun_LightLikeCpu() => Compare(LitScenes.ColouredLights(ViewProjection));

    //**********************************************************
    //** helpers
    //**********************************************************

    private static Mesh Triangle(float z, Vector3 color, bool clockwise = false)
    {
        Vector3[] positions = [new(-2, -2, z), new(2, -2, z), new(0, 2, z)];
        int[] indices = clockwise ? [0, 2, 1] : [0, 1, 2];
        return new Mesh(positions, indices, [color]);
    }

    private static Mesh Quad(float opacity, bool unlit)
    {
        Vector3[] positions = [new(-5, -5, 0), new(5, -5, 0), new(5, 5, 0), new(-5, 5, 0)];
        return new Mesh(positions, [0, 1, 2, 0, 2, 3], [White, White])
        {
            TriangleOpacity = [opacity, opacity],
            IsUnlit = unlit
        };
    }

    private static ConsoleColor Center(IRenderer3D renderer) => renderer.Colors[Size / 2 * Size + Size / 2];

    private static GlRenderer CreateOrIgnore(int width = Size, int height = Size)
    {
        // Only verified on Windows; GLFW wants the main thread on macOS, which NUnit doesn't run tests on.
        if (!OperatingSystem.IsWindows() || !GlRenderer.TryCreate(width, height, ConsolePalette.Campbell, out var gpu))
        {
            Assert.Ignore("OpenGL 3.3 not available");
            return null;
        }

        return gpu;
    }

    private static void Render(IRenderer3D renderer, Action<IRenderer3D> scene, ConsoleColor[] background)
    {
        renderer.Clear(background);
        scene(renderer);
        renderer.Finish();
    }

    /// <summary>Renders the scene on both and asserts they agree; returns the GPU result (already disposed).</summary>
    private static IRenderer3D Compare(Action<IRenderer3D> scene, ConsoleColor[] background = null)
    {
        using var gpu = CreateOrIgnore();
        using var cpu = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        Render(cpu, scene, background ?? BlackBackground);
        Render(gpu, scene, background ?? BlackBackground);

        AssertAgree(cpu, gpu);
        return gpu;
    }

    private static void AssertAgree(IRenderer3D cpu, IRenderer3D gpu)
    {
        var mismatches = 0;
        for (var i = 0; i < cpu.Colors.Length; i++)
        {
            if (cpu.Colors[i] != gpu.Colors[i])
                mismatches++;
        }

        var agreement = 1.0 - (double)mismatches / cpu.Colors.Length;
        TestContext.Out.WriteLine($"{mismatches} of {cpu.Colors.Length} pixels differ ({agreement:P2} agree)");
        Assert.That(agreement, Is.GreaterThanOrEqualTo(MinAgreement));
    }
}
