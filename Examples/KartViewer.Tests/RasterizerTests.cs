using System;
using System.Numerics;
using KartViewer.Rendering;
using NUnit.Framework;

namespace KartViewer.Tests;

public class RasterizerTests
{
    private const int Size = 32;
    private static readonly Vector3 White = Vector3.One;
    private static readonly Vector3 Black = Vector3.Zero;

    // Camera at z = 5 looking down -Z, so a triangle at z = 0 counter-clockwise in x/y faces it.
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    private static Mesh Triangle(float z, Vector3 color, bool clockwise = false)
    {
        Vector3[] positions = [new(-2, -2, z), new(2, -2, z), new(0, 2, z)];
        int[] indices = clockwise ? [0, 2, 1] : [0, 1, 2];
        return new Mesh(positions, indices, [color]);
    }

    private static ConsoleColor Center(Rasterizer rasterizer) => rasterizer.Colors[Size / 2 * Size + Size / 2];

    private static Rasterizer Cleared()
    {
        var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        rasterizer.Clear(new ConsoleColor[Size * Size]); // all Black
        return rasterizer;
    }

    [Test]
    public void Draw_FrontFacingTriangle_FillsCenter()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Triangle(0, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        Assert.That(Center(rasterizer), Is.Not.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_BackFacingTriangle_IsCulled()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Triangle(0, White, clockwise: true), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        Assert.That(Center(rasterizer), Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_TriangleBehindCamera_IsNotDrawn()
    {
        var rasterizer = Cleared();

        rasterizer.Draw(Triangle(10, White), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        Assert.That(Center(rasterizer), Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_FloorReachingBehindCamera_IsClippedAndStillFillsTheBottom()
    {
        var rasterizer = Cleared();
        // A floor 1 below the camera, from 10 behind it to 50 in front: every triangle crosses the near plane.
        Vector3[] positions = [new(-20, -1, 15), new(20, -1, 15), new(20, -1, -50), new(-20, -1, -50)];
        var floor = new Mesh(positions, [0, 1, 2, 0, 2, 3], [White, White]);

        rasterizer.Draw(floor, Matrix4x4.Identity, ViewProjection, Vector3.UnitY);

        Assert.That(rasterizer.Colors[(Size - 1) * Size + Size / 2], Is.Not.EqualTo(ConsoleColor.Black));
        Assert.That(rasterizer.Colors[Size / 2], Is.EqualTo(ConsoleColor.Black)); // the sky above the horizon
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Draw_TwoOverlappingTriangles_NearerOneWinsWhateverTheOrder(bool nearFirst)
    {
        var rasterizer = Cleared();
        var near = Triangle(1, White);
        var far = Triangle(-1, Black);

        rasterizer.Draw(nearFirst ? near : far, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        rasterizer.Draw(nearFirst ? far : near, Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);

        Assert.That(Center(rasterizer), Is.Not.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Draw_TriangleFacingAwayFromLight_IsDarkerThanLitOne()
    {
        var lit = Cleared();
        var unlit = Cleared();
        var gray = new Vector3(0.8f);

        lit.Draw(Triangle(0, gray), Matrix4x4.Identity, ViewProjection, Vector3.UnitZ);
        unlit.Draw(Triangle(0, gray), Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ);

        Assert.That(Center(lit), Is.EqualTo(ConsoleColor.Gray).Or.EqualTo(ConsoleColor.White));
        Assert.That(Center(unlit), Is.EqualTo(ConsoleColor.DarkGray).Or.EqualTo(ConsoleColor.Black));
    }
}
