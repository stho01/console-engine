using System;
using System.Diagnostics;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.OpenGL.Tests;

/// <summary>
/// Renders scenes in true colour with <see cref="Rasterizer"/> and <see cref="GlRenderer"/> and compares the pixels:
/// at least 99 % must be within 2 per channel (edge pixels and the GPU's 8-bit blending may differ). Ignored where
/// OpenGL 3.3 is missing (CI).
/// </summary>
[Category("Gpu")]
public class GlRendererTrueColorTests
{
    private const int Size = 64;
    private const int Center = Size / 2 * Size + Size / 2;
    private const int MaxChannelDifference = 2;
    private const double MinAgreement = 0.99;
    private static readonly Vector3 Orange = new(0.8f, 0.4f, 0.2f);

    // Camera at z = 5 looking down -Z, so a triangle at z = 0 counter-clockwise in x/y faces it.
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    private static readonly uint[] GradientBackground = BuildGradient();

    //**********************************************************
    //** tests
    //**********************************************************

    [TestCase(1f)]
    [TestCase(-1f)]
    public void Draw_TriangleLitFromFrontOrBehind_ShadesLikeCpu(float lightZ)
    {
        var gpu = Compare(r => r.Draw(Triangle(0, Orange), Matrix4x4.Identity, ViewProjection,
            new Vector3(0, 0, lightZ)));

        Assert.That(gpu.Rgb[Center], Is.EqualTo(lightZ > 0 ? 0xCC6633 : 0x472412));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Draw_TwoOverlappingTriangles_NearerOneWinsLikeCpu(bool nearFirst)
    {
        var near = Triangle(1, Orange);
        var far = Triangle(-1, Vector3.One);

        Compare(r =>
        {
            r.Draw(nearFirst ? near : far, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(nearFirst ? far : near, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });
    }

    [Test]
    public void Draw_FloorReachingBehindCamera_IsClippedLikeCpu()
    {
        Vector3[] positions = [new(-20, -1, 15), new(20, -1, 15), new(20, -1, -50), new(-20, -1, -50)];
        var floor = new Mesh(positions, [0, 1, 2, 0, 2, 3], [Orange, Orange]);

        var gpu = Compare(r => r.Draw(floor, Matrix4x4.Identity, ViewProjection, Vector3.UnitY));

        Assert.That(gpu.Rgb[Size / 2], Is.EqualTo(GradientBackground[Size / 2]));
    }

    [Test]
    public void Draw_ChequeredFloorAndGradientQuad_MatchCpu()
    {
        var floor = Mesh.CreateFloor(40, 8, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        Vector3[] positions = [new(-2, -1, 0), new(2, -1, 0), new(2, 2, 0), new(-2, 2, 0)];
        var quad = new Mesh(positions, [0, 1, 2, 0, 2, 3], [new Vector3(0.9f, 0.4f, 0.1f), new Vector3(0.1f, 0.5f, 0.8f)]);
        var world = Matrix4x4.CreateRotationY(0.3f) * Matrix4x4.CreateTranslation(0, -1, 0);
        var toLight = Vector3.Normalize(new Vector3(0.3f, 1, 0.2f));

        Compare(r =>
        {
            r.Draw(floor, world, ViewProjection, toLight);
            r.Draw(quad, Matrix4x4.CreateRotationY(0.6f), ViewProjection, toLight);
        });
    }

    [TestCase(0.25f)]
    [TestCase(0.5f)]
    [TestCase(0.8f)]
    public void Draw_SeeThroughQuadOverGradient_BlendsLikeCpu(float opacity)
    {
        Compare(r =>
        {
            r.Draw(Triangle(-1, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(Quad(0, Vector3.One, opacity), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(Quad(1, new Vector3(0.1f, 0.2f, 0.9f), opacity), Matrix4x4.CreateRotationZ(0.4f), ViewProjection,
                Vector3.UnitZ);
        });
    }

    [Test]
    public void Draw_FartherOpaqueTriangleAfterTransparentQuad_CoversTheBlendLikeCpu()
    {
        var gpu = Compare(r =>
        {
            r.Draw(Quad(1, Vector3.One, 0.5f), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(Triangle(-1, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });

        Assert.That(gpu.Rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_MeshWithTransparentTriangleBeforeOpaqueOne_BlendsOverItLikeCpu()
    {
        Vector3[] positions = [new(-2, -2, 1), new(2, -2, 1), new(0, 2, 1), new(-3, -3, -1), new(3, -3, -1), new(0, 3, -1)];
        var mesh = new Mesh(positions, [0, 1, 2, 3, 4, 5], [Orange, new Vector3(0.1f, 0.7f, 0.3f)])
        {
            TriangleOpacity = [0.5f, 1f]
        };

        Compare(r => r.Draw(mesh, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));
    }

    [Test]
    public void Clear_Background_ShowsWhereNothingIsDrawn()
    {
        var gpu = Compare(r => r.Draw(Triangle(0, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(gpu.Rgb[0], Is.EqualTo(GradientBackground[0]));
        Assert.That(gpu.Rgb[Size - 1], Is.EqualTo(GradientBackground[Size - 1]));
        Assert.That(gpu.Rgb[Size * Size - 1], Is.EqualTo(GradientBackground[Size * Size - 1]));
    }

    [Test]
    public void Clear_WithTheOtherModesBackground_Throws()
    {
        using var trueColor = CreateOrIgnore();
        using var palette = new GlRenderer(Size, Size, ConsolePalette.Campbell);

        Assert.Throws<InvalidOperationException>(() => trueColor.Clear(new ConsoleColor[Size * Size]));
        Assert.Throws<InvalidOperationException>(() => palette.Clear(new uint[Size * Size]));
    }

    [Test]
    public void DrawAndFinish_InSteadyState_DoesNotAllocate()
    {
        const int width = 320;
        const int height = 180;
        using var gpu = CreateOrIgnore(width, height);
        var floor = Mesh.CreateFloor(80, 40, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        var smoke = Quad(0, Vector3.One, 0.4f);
        var background = new uint[width * height];
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

        TestContext.Out.WriteLine($"{floor.TriangleCount + smoke.TriangleCount} triangles at {width}x{height}: " +
                                  $"{elapsed.TotalMilliseconds / frames:F3} ms per frame");
        Assert.That(allocated, Is.Zero);

        void Frame()
        {
            gpu.Clear(background);
            gpu.Draw(floor, Matrix4x4.Identity, viewProjection, Vector3.UnitY);
            gpu.Draw(smoke, Matrix4x4.Identity, viewProjection, Vector3.UnitZ);
            gpu.Finish();
        }
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static Mesh Triangle(float z, Vector3 color)
    {
        Vector3[] positions = [new(-2, -2, z), new(2, -2, z), new(0, 2, z)];
        return new Mesh(positions, [0, 1, 2], [color]);
    }

    private static Mesh Quad(float z, Vector3 color, float opacity)
    {
        Vector3[] positions = [new(-1.5f, -1.5f, z), new(1.5f, -1.5f, z), new(1.5f, 1.5f, z), new(-1.5f, 1.5f, z)];
        return new Mesh(positions, [0, 1, 2, 0, 2, 3], [color, color])
        {
            TriangleOpacity = [opacity, opacity],
            IsUnlit = true
        };
    }

    /// <summary>Red grows to the right, green downwards, blue fixed: every row and column differs.</summary>
    private static uint[] BuildGradient()
    {
        var background = new uint[Size * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
            background[y * Size + x] = (uint)(x * 4 << 16 | y * 4 << 8 | 0x60);
        return background;
    }

    private static GlRenderer CreateOrIgnore(int width = Size, int height = Size)
    {
        // Only verified on Windows; GLFW wants the main thread on macOS, which NUnit doesn't run tests on.
        if (!OperatingSystem.IsWindows()
            || !GlRenderer.TryCreate(width, height, ConsolePalette.Campbell, trueColor: true, out var gpu))
        {
            Assert.Ignore("OpenGL 3.3 not available");
            return null;
        }

        return gpu;
    }

    private static void Render(IRenderer3D renderer, Action<IRenderer3D> scene)
    {
        renderer.Clear(GradientBackground);
        scene(renderer);
        renderer.Finish();
    }

    /// <summary>Renders the scene on both and asserts they agree; returns the GPU result (already disposed).</summary>
    private static IRenderer3D Compare(Action<IRenderer3D> scene)
    {
        using var gpu = CreateOrIgnore();
        using var cpu = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor: true);
        Render(cpu, scene);
        Render(gpu, scene);

        var mismatches = 0;
        var exact = 0;
        var maxDifference = 0;
        for (var i = 0; i < cpu.Rgb.Length; i++)
        {
            var difference = MaxChannelDifferenceOf(cpu.Rgb[i], gpu.Rgb[i]);
            maxDifference = Math.Max(maxDifference, difference);
            if (difference == 0)
                exact++;
            if (difference > MaxChannelDifference)
                mismatches++;
        }

        var agreement = 1.0 - (double)mismatches / cpu.Rgb.Length;
        TestContext.Out.WriteLine($"{exact} of {cpu.Rgb.Length} pixels exact, {mismatches} differ by more than " +
                                  $"{MaxChannelDifference} ({agreement:P2} agree), largest difference {maxDifference}");
        Assert.That(agreement, Is.GreaterThanOrEqualTo(MinAgreement));
        return gpu;
    }

    private static int MaxChannelDifferenceOf(uint a, uint b)
    {
        var max = 0;
        for (var shift = 0; shift < 24; shift += 8)
            max = Math.Max(max, Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)));
        return max;
    }
}
