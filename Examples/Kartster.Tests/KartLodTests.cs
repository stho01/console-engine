using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using ConsoleEngine.Rendering3D.OpenGL;
using Kartster.Lighting;
using Kartster.Tests.Lighting;
using NUnit.Framework;

namespace Kartster.Tests;

public class KartLodTests
{
    private const int Width = 320;
    private const int Height = 180;
    private static readonly string Karts = Path.Combine(AppContext.BaseDirectory, "Assets", "Karts");
    private static readonly Vector3 Body = new(0.2f, 0.4f, 0.8f);
    private static readonly Matrix4x4 Projection =
        Matrix4x4.CreatePerspectiveFieldOfView(50f * MathF.PI / 180f, 16f / 9f, 0.2f, 400f);

    private static readonly float PixelsPerMeter = Height * Projection.M22 / 2f;

    // The six heaviest shipped karts, 34 000 to 40 000 triangles each.
    private static readonly string[] HeavyKarts = ["whosp", "hektor", "lasagna", "baffi", "raleur", "itg"];

    private static IEnumerable<string> KartNames() =>
        Directory.GetDirectories(Karts).Select(Path.GetFileName).Order(StringComparer.Ordinal);

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void LevelFor_Distance_FullNearThenCoarserOnceACellIsUnderAPixel()
    {
        Assert.That(KartLod.LevelFor(5.5f, PixelsPerMeter), Is.Zero, "the chase camera's distance: full");
        for (var level = 1; level <= KartLod.CellMeters.Length; level++)
        {
            var distance = StartOf(level);
            Assert.That(KartLod.LevelFor(distance * 0.99f, PixelsPerMeter), Is.EqualTo(level - 1));
            Assert.That(KartLod.LevelFor(distance * 1.01f, PixelsPerMeter), Is.EqualTo(level));
        }
    }

    [Test]
    public void Simplify_TwoTrianglesInOneCellAndARepeat_DropsThemKeepsColourAndOpacity()
    {
        Vector3[] positions = [new(0, 0, 0), new(0.01f, 0, 0), new(0, 0.01f, 0), new(1, 0, 0), new(0, 1, 0)];
        int[] indices = [0, 1, 2, 0, 3, 4, 1, 3, 4, 3, 4, 0];
        Vector3[] colors = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY];
        var mesh = new Mesh(positions, indices, colors) { TriangleOpacity = [1f, 0.4f, 0.4f, 0.4f] };

        var simple = KartLod.Simplify(mesh, 0.1f);

        Assert.That(simple.TriangleCount, Is.EqualTo(1), "collapsed, then 0-3-4 thrice (as 1-3-4 and 3-4-0)");
        Assert.That(simple.TriangleColors[0], Is.EqualTo(Vector3.UnitY));
        Assert.That(simple.TriangleOpacity[0], Is.EqualTo(0.4f));
    }

    [TestCaseSource(nameof(KartNames))]
    public void Levels_ShippedKart_FewerTrianglesSameColoursAndGlass(string kart)
    {
        var lod = new KartLod(KartModel.Load(Path.Combine(Karts, kart), Body));
        var full = lod.Levels[0];

        for (var level = 1; level < lod.Levels.Length; level++)
        {
            var mesh = lod.Levels[level];
            Assert.That(mesh.TriangleCount, Is.LessThanOrEqualTo(lod.Levels[level - 1].TriangleCount));
            Assert.That(mesh.TriangleCount, Is.GreaterThan(0));
            Assert.That(mesh.TriangleColors.Distinct(), Is.SubsetOf(full.TriangleColors.Distinct()));
            Assert.That(mesh.TriangleOpacity == null, Is.EqualTo(full.TriangleOpacity == null), "glass stays");
        }

        if (full.TriangleCount > 20000)
            Assert.That(lod.Levels[1].TriangleCount, Is.LessThan(full.TriangleCount * 7 / 10), "a real saving");
    }

    /// <summary>
    /// Every shipped kart drawn where each level starts, from behind and side on, against the full mesh there: no more
    /// of its pixels change than when the full kart moves by a third of a pixel (the noise of drawing a
    /// 20 000-triangle kart into a few hundred pixels), plus a little.
    /// </summary>
    [TestCaseSource(nameof(KartNames))]
    public void Pick_WhereEachLevelStarts_ChangesNoMoreThanASubpixelShift(string kart)
    {
        var lod = new KartLod(KartModel.Load(Path.Combine(Karts, kart), Body));
        using var renderer = new Rasterizer(Width, Height, KartsterGame.ScenePalette, trueColor: true);
        var background = new uint[Width * Height];
        for (var level = 1; level < lod.Levels.Length; level++)
        {
            var distance = StartOf(level) * 1.01f;
            foreach (var direction in new[] { Vector3.UnitZ, Vector3.UnitX })
            {
                var eye = direction * distance + Vector3.UnitY * 1.2f;
                var viewProjection = Matrix4x4.CreateLookAt(eye, new Vector3(0, 0.4f, 0), Vector3.UnitY) * Projection;
                var third = distance / PixelsPerMeter / 3f; // a third of a pixel there, metres
                var shift = Matrix4x4.CreateTranslation(Vector3.Cross(direction, Vector3.UnitY) * third);
                var full = Draw(lod.Levels[0], Matrix4x4.Identity, viewProjection);
                var shifted = Different(full, Draw(lod.Levels[0], shift, viewProjection));
                var simple = Different(full, Draw(lod.Pick(distance, PixelsPerMeter), Matrix4x4.Identity, viewProjection));

                Assert.That(simple, Is.LessThanOrEqualTo(shifted * 2 + 10),
                    $"{kart} level {level} at {distance:0.0} m: {simple} pixels differ, shifted {shifted}");
            }
        }

        uint[] Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection)
        {
            renderer.Clear(background);
            renderer.Draw(mesh, world, viewProjection, KartsterGame.ToSun);
            return renderer.CopyFinished();
        }
    }

    /// <summary>
    /// Six heavy karts in a bunch ahead of the chase camera, 320×180, as the race draws them (ground, road, scenery,
    /// shadows, karts, lenses), on Star Circuit and on hilly Norbergring, by day and at night, at full detail and with
    /// <see cref="KartLod"/>, on the CPU in both colour modes and on the GPU when there is one. Prints the times (run
    /// in Release for real numbers).
    /// </summary>
    [Test]
    [Explicit("A measurement, slow in Debug: dotnet test -c Release --filter Frame_SixHeavyKarts_ReportsTheRenderTimes")]
    public void Frame_SixHeavyKarts_ReportsTheRenderTimes()
    {
        foreach (var trackName in new[] { "star-circuit", "norbergring" })
        {
            var track = Track.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks"), trackName);
            var folders = Array.ConvertAll(HeavyKarts, kart => Path.Combine(Karts, kart));
            var meshes = Array.ConvertAll(folders, folder => KartModel.Load(folder, Body));
            var lods = Array.ConvertAll(meshes, mesh => new KartLod(mesh));
            var worlds = new Matrix4x4[meshes.Length];
            for (var i = 0; i < meshes.Length; i++)
            {
                var (position, forward) = track.Sample(30f - 5f * i);
                position += Vector3.Cross(forward, Vector3.UnitY) * (i % 2 == 0 ? -1.5f : 1.5f);
                worlds[i] = Matrix4x4.CreateWorld(position, forward, Vector3.UnitY);
            }

            var (player, playerForward) = track.Sample(30f - 5f * meshes.Length);
            var eye = player - playerForward * 5.5f + Vector3.UnitY * 2.2f;
            var viewProjection = Matrix4x4.CreateLookAt(eye, player + playerForward * 4f + Vector3.UnitY,
                Vector3.UnitY) * Projection;
            var lights = new KartLights(meshes, folders, trackName, Width, Height, _ => false);
            var sky = new ConsoleColor[Width * Height];
            var skyRgb = new uint[Width * Height];
            var triangles = 0;
            var drawn = 0;
            for (var i = 0; i < meshes.Length; i++)
            {
                triangles += meshes[i].TriangleCount;
                drawn += Pick(i).TriangleCount;
            }

            TestContext.Out.WriteLine($"{trackName}: {triangles} kart triangles, {drawn} with KartLod");
            Report("CPU 16 colours", new Rasterizer(Width, Height, KartsterGame.ScenePalette));
            Report("CPU 24-bit", new Rasterizer(Width, Height, KartsterGame.ScenePalette, trueColor: true));
            if (OperatingSystem.IsWindows() && GlRenderer.TryCreate(Width, Height, KartsterGame.ScenePalette, out var gpu))
                Report("GPU 16 colours", gpu);

            Mesh Pick(int kart) => lods[kart].Pick(Vector3.Distance(eye, worlds[kart].Translation), PixelsPerMeter);

            void Report(string name, IRenderer3D renderer)
            {
                using (renderer)
                {
                    Time(renderer, night: true, karts: true, lod: false); // JIT warm-up
                    var day = Time(renderer, night: false, karts: true, lod: false);
                    var night = Time(renderer, night: true, karts: true, lod: false);
                    var lodDay = Time(renderer, night: false, karts: true, lod: true);
                    var lodNight = Time(renderer, night: true, karts: true, lod: true);
                    var emptyDay = Time(renderer, night: false, karts: false, lod: false);
                    var emptyNight = Time(renderer, night: true, karts: false, lod: false);
                    TestContext.Out.WriteLine($"  {name}: full detail day {day:F2} / night {night:F2} ms, KartLod " +
                        $"{lodDay:F2} / {lodNight:F2} ms, without the karts {emptyDay:F2} / {emptyNight:F2} ms");
                }
            }

            double Time(IRenderer3D renderer, bool night, bool karts, bool lod)
            {
                lights.Night = night;
                for (var i = 0; i < 20; i++)
                    Frame(renderer, karts, lod);
                const int frames = 100;
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < frames; i++)
                    Frame(renderer, karts, lod);
                return Stopwatch.GetElapsedTime(start).TotalMilliseconds / frames;
            }

            void Frame(IRenderer3D renderer, bool karts, bool lod)
            {
                if (renderer.TrueColor)
                    renderer.Clear(skyRgb);
                else
                    renderer.Clear(sky);
                lights.Begin(renderer, worlds, eye);
                track.DrawGround(renderer, viewProjection, eye, KartsterGame.ToSun);
                renderer.Draw(track.Road, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
                track.Scenery.Draw(renderer, viewProjection, eye, KartsterGame.ToSun);
                lights.DrawShadows(renderer, viewProjection, worlds);
                for (var i = 0; karts && i < meshes.Length; i++)
                    renderer.Draw(lod ? Pick(i) : meshes[i], worlds[i], viewProjection, KartsterGame.ToSun);
                lights.DrawLenses(renderer, viewProjection, worlds);
                renderer.Finish();
            }
        }
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>The distance <paramref name="level"/> starts at: its cell is one pixel there.</summary>
    private static float StartOf(int level) =>
        KartLod.CellMeters[level - 1] * PixelsPerMeter / KartLod.PixelsPerCell;

    /// <summary>Pixels at least 48 apart in a channel.</summary>
    private static int Different(uint[] a, uint[] b)
    {
        var count = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (ColorDistance(a[i], b[i]) > 48)
                count++;
        }

        return count;
    }

    private static int ColorDistance(uint a, uint b) =>
        Math.Max(Channel(a, b, 16), Math.Max(Channel(a, b, 8), Channel(a, b, 0)));

    private static int Channel(uint a, uint b, int shift) =>
        Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF));
}
