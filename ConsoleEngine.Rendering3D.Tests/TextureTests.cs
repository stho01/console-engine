using System;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

public class TextureTests
{
    private const int Size = 32;
    private const uint Background = 0x204061;
    private const uint Red = 0xFFFF0000;
    private const uint Green = 0xFF00FF00;
    private const uint Blue = 0xFF0000FF;
    private const uint Hole = 0x00FFFFFF;

    // Camera at z = 5 looking down -Z: a quad at z = 0 from -2 to 2 covers the middle of the frame.
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    /// <summary>Red, green on the top row; blue and a hole below.</summary>
    private static readonly MeshTexture Quadrants = new(2, 2, [Red, Green, Blue, Hole]);

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>A square facing the camera at depth <paramref name="z"/>, coordinates 0..<paramref name="repeat"/>.</summary>
    private static Mesh Quad(float z, MeshTexture texture, Vector3 tint, bool unlit = true, float repeat = 1)
    {
        Vector3[] positions = [new(-2, 2, z), new(2, 2, z), new(2, -2, z), new(-2, -2, z)];
        Vector2[] uvs = [new(0, 0), new(repeat, 0), new(repeat, repeat), new(0, repeat)];
        return new Mesh(positions, [0, 3, 2, 0, 2, 1], [tint, tint])
        {
            TriangleTextures = [texture, texture], TextureCoordinates = uvs, IsUnlit = unlit
        };
    }

    private static Rasterizer Cleared()
    {
        var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor: true);
        var background = new uint[Size * Size];
        Array.Fill(background, Background);
        rasterizer.Clear(background);
        return rasterizer;
    }

    private static uint At(Rasterizer rasterizer, int x, int y) => rasterizer.Rgb[y * Size + x];

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Draw_TexturedQuad_ShowsEachTexelWhereItBelongs()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, 10, 10), Is.EqualTo(0xFF0000));
        Assert.That(At(rasterizer, 21, 10), Is.EqualTo(0x00FF00));
        Assert.That(At(rasterizer, 10, 21), Is.EqualTo(0x0000FF));
    }

    [Test]
    public void Draw_TextureHole_LeavesWhatIsBehindAndWritesNoDepth()
    {
        var rasterizer = Cleared();
        var behind = Quad(-1, new MeshTexture(1, 1, [Green]), Vector3.One);

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();
        var hole = At(rasterizer, 21, 21);
        rasterizer.Draw(behind, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(hole, Is.EqualTo(Background));
        Assert.That(At(rasterizer, 21, 21), Is.EqualTo(0x00FF00));
        Assert.That(At(rasterizer, 10, 10), Is.EqualTo(0xFF0000), "the near quad still covers the far one");
    }

    [Test]
    public void Draw_TintedTexture_IsTheTexelTimesTheTint()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Quad(0, Quadrants, new Vector3(0.5f)), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, 10, 10), Is.EqualTo(0x800000));
    }

    [Test]
    public void Draw_LitTexture_IsTheTexelTimesTheLight()
    {
        var rasterizer = Cleared();
        rasterizer.Lighting = new SceneLighting { Ambient = new Vector3(0.25f), SunColor = Vector3.Zero };

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One, unlit: false), Matrix4x4.Identity, ViewProjection,
            Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, 10, 10), Is.EqualTo(0x400000));
    }

    [Test]
    public void Draw_CoordinatesBeyondOne_RepeatTheTexture()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One, repeat: 2), Matrix4x4.Identity, ViewProjection,
            Vector3.UnitZ);
        rasterizer.Finish();

        // Four copies: the top-left copy's green texel sits left of the middle.
        Assert.That(At(rasterizer, 13, 8), Is.EqualTo(0x00FF00));
        Assert.That(At(rasterizer, 18, 8), Is.EqualTo(0xFF0000));
    }

    [Test]
    public void Draw_TexturedFloorThroughTheNearPlane_IsTexturedUpToTheBottom()
    {
        var rasterizer = Cleared();
        var red = new MeshTexture(1, 1, [Red]);
        Vector3[] positions = [new(-10, -1, -10), new(-10, -1, 10), new(10, -1, 10), new(10, -1, -10)];
        var floor = new Mesh(positions, [0, 1, 2, 0, 2, 3], [Vector3.One, Vector3.One])
        {
            TriangleTextures = [red, red], TextureCoordinates = [new(0, 0), new(0, 4), new(4, 4), new(4, 0)],
            IsUnlit = true
        };

        rasterizer.Draw(floor, Matrix4x4.Identity, ViewProjection, Vector3.UnitY);
        rasterizer.Finish();

        Assert.That(At(rasterizer, Size / 2, Size - 1), Is.EqualTo(0xFF0000));
    }

    [Test]
    public void Draw_LitMeshInFog_FadesByDistance()
    {
        var rasterizer = Cleared();
        rasterizer.Lighting = new SceneLighting
        {
            Ambient = Vector3.One, SunColor = Vector3.Zero, FogStart = 0, FogEnd = 10, FogColor = Vector3.Zero
        };
        var white = new MeshTexture(1, 1, [0xFFFFFFFF]);

        rasterizer.Draw(Quad(0, white, Vector3.One, unlit: false), Matrix4x4.Identity, ViewProjection,
            Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, Size / 2, Size / 2) & 0xFF, Is.EqualTo(0x80).Within(1)); // 5 units: half fog
    }

    [Test]
    public void Draw_FlatLitMeshInFog_FadesToo()
    {
        var rasterizer = Cleared();
        rasterizer.Lighting = new SceneLighting
        {
            Ambient = Vector3.One, SunColor = Vector3.Zero, FogStart = 0, FogEnd = 10, FogColor = Vector3.One
        };
        var flat = new Mesh(Quad(0, null, Vector3.Zero).Positions, [0, 3, 2, 0, 2, 1], [Vector3.Zero, Vector3.Zero]);

        rasterizer.Draw(flat, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, Size / 2, Size / 2) & 0xFF, Is.EqualTo(0x80).Within(1));
    }

    [Test]
    public void Draw_UnlitMeshInFog_IsNotFogged()
    {
        var rasterizer = Cleared();
        rasterizer.Lighting = new SceneLighting { FogStart = 0, FogEnd = 10 };

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Finish();

        Assert.That(At(rasterizer, 10, 10), Is.EqualTo(0xFF0000));
    }

    [Test]
    public void Draw_TexturedInPaletteMode_DrawsTheNearestConsoleColour()
    {
        var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        rasterizer.Clear(new ConsoleColor[Size * Size]);

        rasterizer.Draw(Quad(0, Quadrants, Vector3.One), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        Assert.That(rasterizer.Colors[10 * Size + 21], Is.EqualTo(ConsoleColor.Green));
        Assert.That(rasterizer.Colors[21 * Size + 21], Is.EqualTo(ConsoleColor.Black), "hole");
    }

    [Test]
    public void Sample_OutsideZeroToOne_Wraps()
    {
        Assert.That(Quadrants.Sample(1.25f, -0.25f, out var color), Is.True);
        Assert.That(color, Is.EqualTo(new Vector3(0, 0, 1)));
        Assert.That(Quadrants.Sample(0.75f, 0.75f, out _), Is.False);
        Assert.That(Quadrants.HasHoles, Is.True);
    }

    [Test]
    public void Draw_TexturedSteadyState_DoesNotAllocate()
    {
        var rasterizer = Cleared();
        var quad = Quad(0, Quadrants, Vector3.One, unlit: false);
        var lighting = new SceneLighting { FogStart = 1, FogEnd = 20 };
        var background = new uint[Size * Size];
        rasterizer.Lighting = lighting;
        rasterizer.Draw(quad, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10; i++)
        {
            rasterizer.Clear(background);
            rasterizer.Lighting = lighting;
            rasterizer.Draw(quad, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
            rasterizer.Finish();
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
