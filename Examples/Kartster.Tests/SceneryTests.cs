using System;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using NUnit.Framework;

namespace Kartster.Tests;

public class SceneryTests
{
    private static readonly string Models = Path.Combine("Assets", "Scenery");
    private static readonly Matrix4x4 Projection =
        Matrix4x4.CreatePerspectiveFieldOfView(50f * MathF.PI / 180f, 16f / 9f, 0.2f, 400f);

    /// <summary>Looking from the origin (2 m up) down -Z.</summary>
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 2, 0), new Vector3(0, 2, -1), Vector3.UnitY) * Projection;

    private static Scenery Parse(params string[] lines) => Scenery.Parse(lines, "test.scenery.txt", Models, -0.3f);

    private static string Triangle(float x, float z) => FormattableString.Invariant(
        $"tri {x} 0.5 {z} {x + 1} 0.5 {z} {x} 1.5 {z} 1 0 0");

    [Test]
    public void Parse_ModelAndTriangleLines_MakesAnInstanceAndACell()
    {
        var scenery = Parse(
            "# a comment",
            "",
            "model Box 1 2 3 90 1 1 1 0.5 0.5 0.5  # trailing comment",
            Triangle(0, 0));

        Assert.That(scenery.ModelCount, Is.EqualTo(1));
        Assert.That(scenery.Count, Is.EqualTo(2));
        Assert.That(scenery.TriangleCount, Is.EqualTo(1));
    }

    [TestCase("model Box 1 2 3", "test.scenery.txt:2: expected 'model <name> x y z yawDegrees scaleX scaleY scaleZ r g b'")]
    [TestCase("tri 1 2 3", "test.scenery.txt:2: expected 'tri x1 y1 z1 x2 y2 z2 x3 y3 z3 r g b'")]
    [TestCase("model Box 1 two 3 0 1 1 1 1 1 1", "test.scenery.txt:2: 'two' is not a number")]
    [TestCase("tree 1 2 3", "test.scenery.txt:2: unknown line 'tree', expected model or tri")]
    public void Parse_BadLine_ThrowsWithTheLineNumber(string line, string message)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Parse("# header", line));

        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void Parse_MissingModel_ThrowsWithTheLineNumber()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Parse("model Nothing 0 0 0 0 1 1 1 1 1 1"));

        Assert.That(exception.Message,
            Does.StartWith("test.scenery.txt:1: model file ").And.EndWith("Nothing.obj not found"));
    }

    [Test]
    public void Load_ShippedStarCircuit_HasModelsAndTriangles()
    {
        var track = Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit");

        Assert.That(track.Scenery.ModelCount, Is.GreaterThan(10)); // pipes and power-up boxes
        Assert.That(track.Scenery.TriangleCount, Is.GreaterThan(1000)); // fences, signs, the start/finish line
    }

    [Test]
    public void Draw_InFrontAndNear_IsDrawn()
    {
        var renderer = new CountingRenderer();

        var drawn = Parse(Triangle(0, -10)).Draw(renderer, ViewProjection, new Vector3(0, 2, 0), Vector3.UnitY);

        Assert.That(drawn, Is.EqualTo(1));
        Assert.That(renderer.Draws, Is.EqualTo(1));
    }

    [Test]
    public void Draw_BehindTheCamera_IsSkipped()
    {
        var renderer = new CountingRenderer();

        var drawn = Parse(Triangle(0, 10)).Draw(renderer, ViewProjection, new Vector3(0, 2, 0), Vector3.UnitY);

        Assert.That(drawn, Is.Zero);
        Assert.That(renderer.Draws, Is.Zero);
    }

    [Test]
    public void Draw_BeyondTheDrawDistance_IsSkipped()
    {
        var scenery = Parse(Triangle(0, -(Scenery.DrawDistance + 10)));

        var drawn = scenery.Draw(new CountingRenderer(), ViewProjection, new Vector3(0, 2, 0), Vector3.UnitY);

        Assert.That(drawn, Is.Zero);
    }

    [Test]
    public void Draw_InSteadyState_DoesNotAllocate()
    {
        var scenery = Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit").Scenery;
        var renderer = new CountingRenderer();
        scenery.Draw(renderer, ViewProjection, new Vector3(0, 2, 0), Vector3.UnitY);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10; i++)
            scenery.Draw(renderer, ViewProjection, new Vector3(0, 2, 0), Vector3.UnitY);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    private sealed class CountingRenderer : IRenderer3D
    {
        public int Draws { get; private set; }
        public int Width => 0;
        public int Height => 0;
        public bool TrueColor => false;
        public ConsoleColor[] Colors => [];
        public uint[] Rgb => [];

        public void Clear(ReadOnlySpan<ConsoleColor> background) { }
        public void Clear(ReadOnlySpan<uint> backgroundRgb) { }
        public void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight) => Draws++;
        public void Finish() { }
        public void Dispose() { }
    }
}
