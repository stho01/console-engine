using System;
using System.Diagnostics;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

public class TrueColorTests
{
    private const int Size = 32;
    private const int Center = Size / 2 * Size + Size / 2;
    private const uint Background = 0x204061;
    private static readonly Vector3 Orange = new(0.8f, 0.4f, 0.2f); // 0xCC6633

    // Camera at z = 5 looking down -Z, so a triangle at z = 0 counter-clockwise in x/y faces it.
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Draw_FrontFacingTriangleLitHeadOn_IsItsColourExactly()
    {
        var rgb = Render(r => r.Draw(Triangle(0, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_TriangleLitFromBehind_GetsOnlyTheAmbientLight()
    {
        var rgb = Render(r => r.Draw(Triangle(0, Orange), Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0x472412)); // 0.35 × (204, 102, 51) = (71.4, 35.7, 17.85)
    }

    [Test]
    public void Draw_UnlitTriangle_IgnoresTheLight()
    {
        var triangle = new Mesh(Triangle(0, Orange).Positions, [0, 1, 2], [Orange]) { IsUnlit = true };

        var rgb = Render(r => r.Draw(triangle, Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_ColourAboveOne_IsClamped()
    {
        var triangle = new Mesh(Triangle(0, Orange).Positions, [0, 1, 2], [new Vector3(2f, 0.5f, -1f)])
        {
            IsUnlit = true
        };

        var rgb = Render(r => r.Draw(triangle, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0xFF8000));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Draw_TwoOverlappingTriangles_NearerOneWinsWhateverTheOrder(bool nearFirst)
    {
        var near = Triangle(1, Orange);
        var far = Triangle(-1, Vector3.One);

        var rgb = Render(r =>
        {
            r.Draw(nearFirst ? near : far, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(nearFirst ? far : near, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });

        Assert.That(rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_FloorReachingBehindCamera_IsClippedAndStillFillsTheBottom()
    {
        Vector3[] positions = [new(-20, -1, 15), new(20, -1, 15), new(20, -1, -50), new(-20, -1, -50)];
        var floor = new Mesh(positions, [0, 1, 2, 0, 2, 3], [Orange, Orange]);

        var rgb = Render(r => r.Draw(floor, Matrix4x4.Identity, ViewProjection, Vector3.UnitY));

        Assert.That(rgb[(Size - 1) * Size + Size / 2], Is.EqualTo(0xCC6633));
        Assert.That(rgb[Size / 2], Is.EqualTo(Background)); // the sky above the horizon
    }

    [Test]
    public void Draw_HalfTransparentQuad_BlendsToTheMidpointOfItsColourAndTheBackground()
    {
        var rgb = Render(r => r.Draw(Quad(0, Orange, 0.5f), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0x76534A)); // ((204, 102, 51) + (32, 64, 97)) / 2
    }

    [Test]
    public void Draw_InvisibleQuad_LeavesTheBackground()
    {
        var rgb = Render(r => r.Draw(Quad(0, Orange, 0f), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(Background));
    }

    [Test]
    public void Draw_FartherOpaqueTriangleAfterTransparentQuad_IsNotHiddenByIt()
    {
        // Blended triangles write no depth: a farther opaque triangle drawn later covers the blend. Draw see-through
        // meshes last.
        var rgb = Render(r =>
        {
            r.Draw(Quad(1, Vector3.One, 0.5f), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(Triangle(-1, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });

        Assert.That(rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_TransparentQuadBehindOpaqueTriangle_IsHiddenByTheDepthTest()
    {
        var rgb = Render(r =>
        {
            r.Draw(Triangle(1, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            r.Draw(Quad(-1, Vector3.One, 0.5f), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        });

        Assert.That(rgb[Center], Is.EqualTo(0xCC6633));
    }

    [Test]
    public void Draw_MeshWithTransparentTriangleBeforeOpaqueOne_BlendsOverTheOpaqueOne()
    {
        // Within a mesh the opaque triangles go first, whatever their order in the mesh.
        var rgb = Render(r => r.Draw(Mixed(), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ));

        Assert.That(rgb[Center], Is.EqualTo(0x76534A)); // half orange over the opaque 0x204061 triangle
    }

    [Test]
    public void Clear_Background_ShowsWhereNothingIsDrawn()
    {
        var background = new uint[Size * Size];
        for (var i = 0; i < background.Length; i++)
            background[i] = (uint)(i * 2654435761u) & 0xFFFFFF;

        var rgb = Render(r => r.Draw(Triangle(0, Orange), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ),
            background);

        Assert.That(rgb[0], Is.EqualTo(background[0]));
        Assert.That(rgb[Size - 1], Is.EqualTo(background[Size - 1]));
        Assert.That(rgb[Size * Size - 1], Is.EqualTo(background[Size * Size - 1]));
    }

    [Test]
    public void Clear_WithTheOtherModesBackground_Throws()
    {
        using var trueColor = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor: true);
        using var palette = new Rasterizer(Size, Size, ConsolePalette.Campbell);

        Assert.Throws<InvalidOperationException>(() => trueColor.Clear(new ConsoleColor[Size * Size]));
        Assert.Throws<InvalidOperationException>(() => palette.Clear(new uint[Size * Size]));
        Assert.That(trueColor.Colors, Is.Empty);
        Assert.That(palette.Rgb, Is.Empty);
    }

    [Test]
    public void DrawAndFinish_InSteadyState_DoesNotAllocate()
    {
        const int width = 320;
        const int height = 180;
        using var rasterizer = new Rasterizer(width, height, null, trueColor: true);
        var floor = Mesh.CreateFloor(80, 40, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        var smoke = Quad(0, Vector3.One, 0.4f);
        var background = new uint[width * height];
        var viewProjection = Matrix4x4.CreateLookAt(new Vector3(0, 3, 10), Vector3.Zero, Vector3.UnitY)
                             * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 16f / 9, 0.1f, 200);
        for (var i = 0; i < 20; i++)
            Frame();

        const int frames = 100;
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
            rasterizer.Clear(background);
            rasterizer.Draw(floor, Matrix4x4.Identity, viewProjection, Vector3.UnitY);
            rasterizer.Draw(smoke, Matrix4x4.Identity, viewProjection, Vector3.UnitZ);
            rasterizer.Finish();
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
        Vector3[] positions = [new(-5, -5, z), new(5, -5, z), new(5, 5, z), new(-5, 5, z)];
        return new Mesh(positions, [0, 1, 2, 0, 2, 3], [color, color])
        {
            TriangleOpacity = [opacity, opacity],
            IsUnlit = true
        };
    }

    /// <summary>A half-transparent orange triangle at z = 1, then an opaque 0x204061 one at z = -1.</summary>
    private static Mesh Mixed()
    {
        Vector3[] positions = [new(-2, -2, 1), new(2, -2, 1), new(0, 2, 1), new(-2, -2, -1), new(2, -2, -1), new(0, 2, -1)];
        var behind = new Vector3(0x20, 0x40, 0x61) / 255f;
        return new Mesh(positions, [0, 1, 2, 3, 4, 5], [Orange, behind])
        {
            TriangleOpacity = [0.5f, 1f],
            IsUnlit = true
        };
    }

    private static uint[] Render(Action<Rasterizer> scene, uint[] background = null)
    {
        if (background == null)
        {
            background = new uint[Size * Size];
            Array.Fill(background, Background);
        }

        var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor: true);
        rasterizer.Clear(background);
        scene(rasterizer);
        rasterizer.Finish();
        return rasterizer.Rgb;
    }
}
