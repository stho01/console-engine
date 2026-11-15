using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Rendering3D;
using ConsoleEngine.Rendering3D.OpenGL;
using Kartster.Lighting;
using NUnit.Framework;

namespace Kartster.Tests.Lighting;

public class KartLightsTests
{
    private const int Width = 320;
    private const int Height = 180;
    private static readonly string Karts = Path.Combine(AppContext.BaseDirectory, "Assets", "Karts");
    private static readonly string Wagon = Path.Combine(Karts, "wagon");
    private static readonly string Olabil = Path.Combine(Karts, "olabil");
    private static readonly Vector3 Body = new(0.2f, 0.4f, 0.8f);
    private static readonly Matrix4x4 Projection =
        Matrix4x4.CreatePerspectiveFieldOfView(50f * MathF.PI / 180f, 16f / 9f, 0.2f, 400f);

    // Behind and above a kart at the origin facing -Z, as the chase camera sees it.
    private static readonly Vector3 Eye = new(0, 2.2f, 5.5f);
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(Eye, new Vector3(0, 1, -4), Vector3.UnitY) * Projection;

    private static readonly Mesh Ground = Mesh.CreateFloor(200, 1, new Vector3(0.33f), new Vector3(0.33f));

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void Load_Wagon_HasTwoHeadlightsAtTheFrontAndTwoRearLightsAtTheBack()
    {
        var lamps = KartLamps.Load(Wagon);

        Assert.That(lamps.Front, Has.Length.EqualTo(2));
        Assert.That(lamps.Rear, Has.Length.EqualTo(2));
        Assert.That(lamps.Front[0].Z, Is.LessThan(0)); // -Z is forward
        Assert.That(lamps.Rear[0].Z, Is.GreaterThan(lamps.Front[0].Z));
        Assert.That(lamps.Lenses.IsUnlit, Is.True);
    }

    [Test]
    public void Find_RecolouredKartMesh_FindsItsOwnFolder()
    {
        var wagon = KartLamps.Load(Wagon);
        var olabil = KartLamps.Load(Olabil);
        var mesh = KartModel.Recolor(KartModel.Load(Olabil, Body), Body, Vector3.One);

        Assert.That(KartLamps.Find(mesh, [wagon, olabil]), Is.SameAs(olabil));
        Assert.That(wagon.IsPartOf(mesh), Is.False);
    }

    [Test]
    public void Constructor_NightTrack_StartsAtNight()
    {
        Assert.That(Lights("prism-loop").Night, Is.True);
        Assert.That(Lights("star-circuit").Night, Is.False);
    }

    [Test]
    public void Begin_ByDay_LeavesTheDefaultLight()
    {
        using var renderer = Cleared();

        Lights("star-circuit").Begin(renderer, [Matrix4x4.Identity], Eye);

        Assert.That(renderer.Lighting, Is.Null);
    }

    [Test]
    public void Begin_N_TogglesNight()
    {
        var pressed = false;
        var lights = new KartLights([KartModel.Load(Wagon, Body)], [Wagon], "star-circuit", Width, Height,
            key => pressed && key == Key.N);
        using var renderer = Cleared();

        pressed = true;
        lights.Begin(renderer, [Matrix4x4.Identity], Eye);

        Assert.That(lights.Night, Is.True);
        Assert.That(renderer.Lighting, Is.SameAs(lights.Lighting));
    }

    [Test]
    public void Begin_AtNight_GivesEveryKartItsHeadlightsAlongItsForwardAndRearLights()
    {
        var lights = Lights("prism-loop");
        var world = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(10, 0, 0); // facing -X
        using var renderer = Cleared();

        lights.Begin(renderer, [world], Eye);

        var lighting = renderer.Lighting;
        Assert.That(lighting.LightCount, Is.EqualTo(4));
        var headlight = lighting.Lights[0];
        Assert.That(headlight.ConeCosine, Is.GreaterThan(0));
        Assert.That(headlight.Direction.X, Is.LessThan(-0.9f));
        Assert.That(headlight.Direction.Y, Is.LessThan(0)); // a little down, onto the road
        Assert.That(headlight.Position, Is.EqualTo(Vector3.Transform(lights.Lamps[0].Front[0], world)));
        Assert.That(lighting.Lights[3].ConeCosine, Is.EqualTo(-1f)); // rear: all round
    }

    [Test]
    public void Begin_AtNightWithMoreKartsThanLights_KeepsTheNearestKartsLights()
    {
        var mesh = KartModel.Load(Wagon, Body);
        var meshes = new Mesh[6];
        Array.Fill(meshes, mesh);
        var lights = new KartLights(meshes, [Wagon], "prism-loop", Width, Height, _ => false);
        var worlds = new Matrix4x4[6];
        for (var i = 0; i < worlds.Length; i++)
            worlds[i] = Matrix4x4.CreateTranslation(0, 0, -10 * (worlds.Length - i)); // the last one nearest
        using var renderer = Cleared();

        lights.Begin(renderer, worlds, Eye);

        Assert.That(renderer.Lighting.LightCount, Is.EqualTo(SceneLighting.MaxLights));
        // Four lights a wagon: the karts at z = -10, -20, -30 and -40, nearest first.
        Assert.That(renderer.Lighting.Lights[0].Position.Z, Is.InRange(-15f, -5f));
        Assert.That(renderer.Lighting.Lights[^1].Position.Z, Is.InRange(-45f, -35f));
    }

    [Test]
    public void Headlights_AtNight_LightTheRoadAheadOfTheKart()
    {
        var ahead = RoadPixel(new Vector3(0, 0, -6));
        var day = Frame(Lights("star-circuit"));
        var night = Frame(Lights("prism-loop"));
        var dark = Frame(Lights("prism-loop"), lightsOn: false);

        Assert.That(Brightness(night[ahead]), Is.GreaterThan(Brightness(dark[ahead]) + 60));
        Assert.That(Brightness(dark[ahead]), Is.LessThan(Brightness(day[ahead])));
    }

    [Test]
    public void DrawShadows_HalvesTheGroundUnderTheKartAndReachesPastItsSide()
    {
        var lights = Lights("star-circuit");
        var under = RoadPixel(Vector3.Zero);
        var beside = RoadPixel(new Vector3(MaxX(KartModel.Load(Wagon, Body)) * 1.15f, 0, 0));

        using var renderer = Cleared();
        renderer.Draw(Ground, Matrix4x4.Identity, ViewProjection, KartsterGame.ToSun);
        var ground = renderer.CopyFinished();
        lights.DrawShadows(renderer, ViewProjection, [Matrix4x4.Identity]);
        var shaded = renderer.CopyFinished();

        var half = Brightness(ground[under]) / 2;
        Assert.That(Brightness(shaded[under]), Is.InRange(half - 6, half + 6));
        Assert.That(Brightness(shaded[beside]), Is.LessThan(Brightness(ground[beside]) * 0.8));
    }

    [Test]
    public void Frame_AtNightInSteadyState_DoesNotAllocate()
    {
        var lights = Lights("prism-loop");
        using var renderer = new Rasterizer(Width, Height, KartsterGame.ScenePalette, trueColor: true);
        var kart = KartModel.Load(Wagon, Body);
        Matrix4x4[] worlds = [Matrix4x4.Identity];
        var sky = new uint[Width * Height];
        Draw();

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 5; i++)
            Draw();

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);

        void Draw()
        {
            renderer.Clear(sky);
            lights.Begin(renderer, worlds, Eye);
            renderer.Draw(Ground, Matrix4x4.Identity, ViewProjection, KartsterGame.ToSun);
            lights.DrawShadows(renderer, ViewProjection, worlds);
            renderer.Draw(kart, worlds[0], ViewProjection, KartsterGame.ToSun);
            lights.DrawLenses(renderer, ViewProjection, worlds);
            renderer.Finish();
        }
    }

    /// <summary>
    /// Star Circuit with six karts, chase camera, 320×180, as the race draws it: the 3D time before this lighting
    /// (no shadows, no lights), by day (blob shadows) and at night (sky, lights, lenses), on the CPU in both colour
    /// modes and on the GPU when there is one. Prints the times (run in Release for real numbers).
    /// </summary>
    [Test]
    [Explicit("A measurement, slow in Debug: dotnet test -c Release --filter Frame_OnStarCircuit_ReportsTheRenderTimes")]
    public void Frame_OnStarCircuit_ReportsTheRenderTimes()
    {
        var track = Track.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks"), "star-circuit");
        var wagon = KartModel.Load(Wagon, Body);
        var olabil = KartModel.Load(Olabil, Body);
        var meshes = new Mesh[6];
        var worlds = new Matrix4x4[6];
        for (var i = 0; i < meshes.Length; i++)
        {
            meshes[i] = KartModel.Recolor(i % 2 == 0 ? wagon : olabil, Body, new Vector3(0.1f * i, 0.5f, 0.3f));
            var (position, forward) = track.Sample(30f - 7f * i);
            position += Vector3.Cross(forward, Vector3.UnitY) * (i % 2 == 0 ? -1.5f : 1.5f);
            worlds[i] = Matrix4x4.CreateWorld(position, forward, Vector3.UnitY);
        }

        var (playerPosition, playerForward) = (worlds[^1].Translation, -new Vector3(worlds[^1].M31, 0, worlds[^1].M33));
        var eye = playerPosition - playerForward * 5.5f + Vector3.UnitY * 2.2f;
        var viewProjection = Matrix4x4.CreateLookAt(eye, playerPosition + playerForward * 4f + Vector3.UnitY,
            Vector3.UnitY) * Projection;
        var lights = new KartLights(meshes, [Wagon, Olabil], "star-circuit", Width, Height, _ => false);
        var sky = new ConsoleColor[Width * Height];
        var skyRgb = new uint[Width * Height];

        Report("CPU 16 colours", new Rasterizer(Width, Height, KartsterGame.ScenePalette));
        Report("CPU 24-bit", new Rasterizer(Width, Height, KartsterGame.ScenePalette, trueColor: true));
        if (OperatingSystem.IsWindows() && GlRenderer.TryCreate(Width, Height, KartsterGame.ScenePalette, out var gpu))
            Report("GPU 16 colours", gpu);
        if (OperatingSystem.IsWindows() && GlRenderer.TryCreate(Width, Height, KartsterGame.ScenePalette, true, out gpu))
            Report("GPU 24-bit", gpu);

        void Report(string name, IRenderer3D renderer)
        {
            using (renderer)
            {
                Time(renderer, true, true); // JIT warm-up
                var before = Time(renderer, false, false);
                var day = Time(renderer, true, false);
                var night = Time(renderer, true, true);
                TestContext.Out.WriteLine(
                    $"{name}: before {before:F2} ms, day (shadows) {day:F2} ms, night (lights) {night:F2} ms");
            }
        }

        double Time(IRenderer3D renderer, bool shadows, bool night)
        {
            lights.Night = night;
            for (var i = 0; i < 20; i++)
                Frame(renderer, shadows);
            const int frames = 100;
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < frames; i++)
                Frame(renderer, shadows);
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds / frames;
        }

        void Frame(IRenderer3D renderer, bool shadows)
        {
            if (renderer.TrueColor)
                renderer.Clear(skyRgb);
            else
                renderer.Clear(sky);
            if (shadows)
                lights.Begin(renderer, worlds, eye);
            renderer.Draw(track.Ground, track.GroundWorld, viewProjection, KartsterGame.ToSun);
            renderer.Draw(track.Road, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
            track.Scenery.Draw(renderer, viewProjection, eye, KartsterGame.ToSun);
            if (shadows)
                lights.DrawShadows(renderer, viewProjection, worlds);
            for (var i = 0; i < meshes.Length; i++)
                renderer.Draw(meshes[i], worlds[i], viewProjection, KartsterGame.ToSun);
            if (shadows)
                lights.DrawLenses(renderer, viewProjection, worlds);
            renderer.Finish();
        }
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static KartLights Lights(string track) =>
        new([KartModel.Load(Wagon, Body)], [Olabil, Wagon], track, Width, Height, _ => false);

    private static Rasterizer Cleared()
    {
        var renderer = new Rasterizer(Width, Height, KartsterGame.ScenePalette, trueColor: true);
        renderer.Clear(new uint[Width * Height]);
        return renderer;
    }

    /// <summary>The ground and the kart at the origin, lit by <paramref name="lights"/> (true colour).</summary>
    private static uint[] Frame(KartLights lights, bool lightsOn = true)
    {
        using var renderer = Cleared();
        lights.Begin(renderer, [Matrix4x4.Identity], Eye);
        if (!lightsOn)
            renderer.Lighting?.ClearLights();
        renderer.Draw(Ground, Matrix4x4.Identity, ViewProjection, KartsterGame.ToSun);
        return renderer.CopyFinished();
    }

    private static int RoadPixel(Vector3 position)
    {
        var clip = Vector4.Transform(position, ViewProjection);
        var x = (int)((clip.X / clip.W + 1) * 0.5f * Width);
        var y = (int)((1 - clip.Y / clip.W) * 0.5f * Height);
        return y * Width + x;
    }

    private static float MaxX(Mesh mesh)
    {
        var max = float.MinValue;
        foreach (var position in mesh.Positions)
            max = MathF.Max(max, position.X);
        return max;
    }

    private static int Brightness(uint rgb) => (int)(((rgb >> 16) & 0xFF) + ((rgb >> 8) & 0xFF) + (rgb & 0xFF));
}

internal static class RasterizerExtensions
{
    public static uint[] CopyFinished(this Rasterizer renderer)
    {
        renderer.Finish();
        return (uint[])renderer.Rgb.Clone();
    }
}
