using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Kartster.Tests;

/// <summary>
/// Height on Kartster tracks (<c>docs/design/kart-physics-plan.md</c> § 11): slopes, crests, a road on two levels,
/// the road grid and the terrain, on made-up tracks and the original's.
/// </summary>
public class HeightTests
{
    private const float Step = TrackScene.StepSeconds;
    private static readonly string TracksFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks");
    private static readonly Lazy<Track> Norbergring = new(() => Track.Load(TracksFolder, "norbergring"));

    //** slopes

    [Test]
    public void Slope_KartAtRestOnARamp_RollsDownhill()
    {
        var track = Ramp(0.15f);
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, -100f), heading: 0f); // facing uphill (-Z)
        var start = world.Get<Kart>(kart).Height;

        Run(group, 2f);

        Assert.That(world.Get<Velocity>(kart).Value.Y, Is.GreaterThan(2f), "rolls back down (+Z)");
        Assert.That(world.Get<Kart>(kart).Height, Is.LessThan(start - 0.2f));
        Assert.That(world.Get<Kart>(kart).Airborne, Is.False);
    }

    [Test]
    public void Slope_FullThrottle_IsSlowerUphillAndFasterDownhillThanOnTheFlat()
    {
        var flat = SpeedAfter(Ramp(0f), heading: 0f, seconds: 3f);
        var uphill = SpeedAfter(Ramp(0.15f), heading: 0f, seconds: 3f);
        var downhill = SpeedAfter(Ramp(0.15f), heading: MathF.PI, seconds: 3f);

        TestContext.Out.WriteLine($"after 3 s: uphill {uphill:0.00}, flat {flat:0.00}, downhill {downhill:0.00} m/s");
        Assert.That(uphill, Is.LessThan(flat - 1f));
        Assert.That(downhill, Is.GreaterThan(flat + 1f));
    }

    [Test]
    public void Slope_KartOnTheRamp_TiltsWithIt()
    {
        var track = Ramp(0.15f);
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, -100f), heading: 0f);

        Run(group, 0.5f);

        // The road rises towards -Z, so its up direction leans towards +Z.
        var normal = world.Get<Kart>(kart).Normal;
        Assert.That(normal.Z, Is.EqualTo(0.15f / MathF.Sqrt(1f + 0.15f * 0.15f)).Within(0.01f));
        Assert.That(normal.Length(), Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void Crest_FastOverASharpCrest_LeavesTheGroundAndLandsAgain()
    {
        // Up at 20 % for 60 m, then down at 30 %: at 25 m/s the road falls away faster than gravity follows.
        var track = Profile(z => z > -60f ? -z * 0.2f : 12f - (-z - 60f) * 0.3f, 0f, -140f);
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, -40f), heading: 0f);
        world.Get<Velocity>(kart).Value = new Vector2(0f, -25f);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f };
        var flew = false;
        var landed = false;

        for (var step = 0; step < 600 && !landed; step++)
        {
            group.Update(Step);
            var state = world.Get<Kart>(kart);
            flew |= state.Airborne;
            landed = flew && !state.Airborne;
        }

        var position = world.Get<Position>(kart).Value;
        track.TryGetGround(position.X, position.Y, float.MaxValue, out var road, out _, out _);
        Assert.That(flew, Is.True, "leaves the ground over the crest");
        Assert.That(landed, Is.True, "comes down again");
        Assert.That(position.Y, Is.LessThan(-65f), "lands past the crest");
        Assert.That(world.Get<Kart>(kart).Height, Is.EqualTo(road).Within(KartGroundSystem.GroundedTolerance));
    }

    // A rounded crest of radius 60 m holds a kart while v²/R < g, so up to 24 m/s.
    [TestCase(15f, false)]
    [TestCase(30f, true)]
    public void Crest_OverARoundedCrest_LeavesTheGroundOnlyWhenFasterThanGravityFollows(float speed, bool flies)
    {
        const float radius = 60f;
        var track = Profile(z => 20f - (z + 60f) * (z + 60f) / (2f * radius), -20f, -100f);
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, -40f), heading: 0f);
        world.Get<Velocity>(kart).Value = new Vector2(0f, -speed);
        var flew = false;

        while (world.Get<Position>(kart).Value.Y > -75f)
        {
            // Holds the speed along the road, so the crest decides.
            var along = world.Get<Velocity>(kart).Value.Length();
            world.Get<KartControls>(kart) = new KartControls
            {
                Throttle = Math.Clamp((speed - along) * 2f, 0f, 1f),
                Brake = Math.Clamp((along - speed) * 2f, 0f, 1f),
            };
            group.Update(Step);
            flew |= world.Get<Kart>(kart).Airborne;
        }

        Assert.That(flew, Is.EqualTo(flies));
    }

    //** two levels

    [Test]
    public void Bridge_KartDrivingUnderTheDeck_StaysOnTheLowerRoad()
    {
        var track = Crossing();
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(-40f, 0f), heading: -MathF.PI / 2f); // facing +X, under the deck
        world.Get<Velocity>(kart).Value = new Vector2(20f, 0f);

        for (var step = 0; step < 480; step++)
        {
            group.Update(Step);
            Assert.That(world.Get<Kart>(kart).Height, Is.EqualTo(0f).Within(0.05f), $"step {step}");
        }

        Assert.That(world.Get<Position>(kart).Value.X, Is.GreaterThan(10f), "drove right under the deck");
    }

    [Test]
    public void Bridge_KartDrivingOverTheDeck_StaysOnTheUpperRoad()
    {
        var track = Crossing();
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, 40f), heading: 0f, height: 6f); // facing -Z, on the deck
        world.Get<Velocity>(kart).Value = new Vector2(0f, -20f);

        for (var step = 0; step < 480; step++)
        {
            group.Update(Step);
            Assert.That(world.Get<Kart>(kart).Height, Is.EqualTo(6f).Within(0.05f), $"step {step}");
        }

        Assert.That(world.Get<Position>(kart).Value.Y, Is.LessThan(-10f), "drove right over the lower road");
    }

    [TestCase(254, 0f)]
    [TestCase(1121, 7f)]
    public void Bridge_NorbergringRouteThroughTheCrossing_KeepsItsLevel(int metres, float level)
    {
        var track = Norbergring.Value;
        using var world = new World();
        var group = Driving(world, track);
        var (point, forward) = track.Sample(metres - 15f);
        var kart = Place(world, track, new Vector2(point.X, point.Z), KartFrame.HeadingOf(forward), point.Y);
        world.Get<Velocity>(kart).Value = new Vector2(forward.X, forward.Z) * 15f;

        for (var step = 0; step < 240; step++)
        {
            group.Update(Step);
            Assert.That(world.Get<Kart>(kart).Height, Is.EqualTo(level).Within(1.5f), $"step {step}");
        }

        var position = world.Get<Position>(kart).Value;
        var (crossing, _) = track.Sample(metres);
        Assert.That(Vector2.Distance(position, new Vector2(crossing.X, crossing.Z)), Is.GreaterThan(5f), "drove past it");
        Assert.That(world.Get<Kart>(kart).Surface, Is.EqualTo(Surface.Asphalt));
    }

    //** road grid and terrain

    [TestCase("star-circuit")]
    [TestCase("norbergring")]
    [TestCase("dunsfold-neon")]
    public void TryGetRoadHeight_RandomPoints_FindsWhatEveryTriangleFinds(string id)
    {
        var track = id == "norbergring" ? Norbergring.Value : Track.Load(TracksFolder, id);
        var random = new Random(7);
        var hits = 0;
        for (var i = 0; i < 4000; i++)
        {
            // Half near the centerline, half anywhere over the ground square.
            var (point, _) = track.Sample((float)random.NextDouble() * track.Length);
            var probe = i % 2 == 0
                ? point + new Vector3((float)random.NextDouble() * 16f - 8f, 0f, (float)random.NextDouble() * 16f - 8f)
                : track.GroundCenter + new Vector3(((float)random.NextDouble() * 2f - 1f) * track.GroundHalfSize, 0f,
                    ((float)random.NextDouble() * 2f - 1f) * track.GroundHalfSize);

            var expected = BruteForceRoadHeight(track, probe, out var height);
            Assert.That(track.TryGetRoadHeight(probe, out var found), Is.EqualTo(expected), $"{probe}");
            if (expected)
            {
                Assert.That(found, Is.EqualTo(height).Within(1e-4f), $"{probe}");
                hits++;
            }
        }

        Assert.That(hits, Is.GreaterThan(1500));
    }

    [Test]
    public void Terrain_Parse_BlendsTheFourCornersAndHasNoGroundBesideANaN()
    {
        var terrain = Terrain.Parse([
            "# test",
            "terrain 10 20 5 3 2 0.2 0.45 0.15",
            "0 2 NaN",
            "4 6 1",
        ], "test.terrain.txt");

        Assert.That(terrain.TryGetHeight(12.5f, 22.5f, out var middle, out var normal), Is.True);
        Assert.That(middle, Is.EqualTo(3f).Within(1e-5f));
        Assert.That(normal.X, Is.LessThan(0f), "rises towards +X, so leans towards -X");
        Assert.That(normal.Z, Is.LessThan(0f));
        Assert.That(terrain.TryGetHeight(17.5f, 22.5f, out _, out _), Is.False, "a corner has no ground");
        Assert.That(terrain.TryGetHeight(9f, 22f, out _, out _), Is.False, "outside the grid");
        Assert.That(terrain.Tiles.Count, Is.EqualTo(1));
    }

    [TestCase("terrain 0 0 5 2 2 1 1 1\n0 0", "test.terrain.txt: expected a terrain header and 2 rows, found 1")]
    [TestCase("terrain 0 0 5 2 2 1 1 1\n0 0 0", "test.terrain.txt:2: expected 2 heights, found 3")]
    [TestCase("ground 0 0 5 2 2", "test.terrain.txt:1: expected 'terrain originX originZ cellSize columns rows r g b'")]
    public void Terrain_Parse_BadFile_ThrowsWithFileAndLine(string text, string message)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Terrain.Parse(text.Split('\n'), "test.terrain.txt"));
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void Load_Norbergring_HasTerrainUnderTheRoad()
    {
        var track = Norbergring.Value;

        Assert.That(track.Terrain, Is.Not.Null);
        Assert.That(track.Terrain.Tiles.Count, Is.GreaterThan(10));
        for (var metres = 0f; metres < track.Length; metres += 25f)
        {
            var (point, _) = track.Sample(metres);
            if (!track.Terrain.TryGetHeight(point.X, point.Z, out var ground, out _))
                continue;
            // The original fits it 5 cm under the road; under the bridge it is the lower road's.
            Assert.That(ground, Is.LessThan(point.Y + 0.5f), $"{metres} m");
        }
    }

    [Test]
    public void Ground_KartOffTheRoadOnTerrain_StandsOnIt()
    {
        var track = Norbergring.Value;
        using var world = new World();
        var group = Driving(world, track);
        var (point, forward) = track.Sample(600f);
        var side = new Vector3(-forward.Z, 0f, forward.X) * 25f;
        var spot = point + side;
        Assume.That(track.TryGetRoadHeight(spot, out _), Is.False);
        Assert.That(track.Terrain.TryGetHeight(spot.X, spot.Z, out var ground, out _), Is.True);
        var kart = Place(world, track, new Vector2(spot.X, spot.Z), 0f, ground + 1f);

        Run(group, 1f);

        var position = world.Get<Position>(kart).Value;
        track.Terrain.TryGetHeight(position.X, position.Y, out ground, out _);
        Assert.That(world.Get<Kart>(kart).Height, Is.EqualTo(ground).Within(0.01f));
        Assert.That(world.Get<Kart>(kart).Surface, Is.EqualTo(Surface.Grass));
    }

    //** races

    [TestCase("norbergring")]
    [TestCase("dunsfold-neon")]
    [TestCase("kinnekulle")]
    [TestCase("brands-hatch-indy")]
    public void Race_ThreeBotsOnATrackWithHeight_EveryBotCompletesALap(string id)
    {
        const int capSeconds = 240;
        var track = id == "norbergring" ? Norbergring.Value : Track.Load(TracksFolder, id);
        using var world = new World();
        var tunings = new[] { KartCatalog.Olabil, KartCatalog.Wagon, KartCatalog.Kart };
        var markers = RaceMarkers.Load(TracksFolder, id);
        var race = TrackScene.CreateSession(track, markers, laps: 5);
        var karts = TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: -1, race);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings, race);
        float lowest = float.MaxValue, highest = float.MinValue;

        var steps = 0;
        for (; steps < (capSeconds + RaceSession.CountdownSeconds) * 120 && !EveryKartLapped(); steps++)
        {
            group.Update(Step);
            for (var i = 0; i < karts.Length; i++)
            {
                lowest = MathF.Min(lowest, world.Get<Kart>(karts[i]).Height);
                highest = MathF.Max(highest, world.Get<Kart>(karts[i]).Height);
            }
        }

        TestContext.Out.WriteLine($"{id}: every bot lapped after {steps / 120f - RaceSession.CountdownSeconds:0.0} s, " +
                                  $"karts between {lowest:0.0} and {highest:0.0} m");
        for (var i = 0; i < karts.Length; i++)
        {
            var progress = world.Get<RaceProgress>(karts[i]);
            TestContext.Out.WriteLine($"bot {i}: {progress.CompletedLaps} laps, best {progress.BestLapSeconds:0.0} s");
            Assert.That(progress.CompletedLaps, Is.GreaterThanOrEqualTo(1), $"bot {i} drives on");
            Assert.That(progress.MissedCheckpoints, Is.Zero, $"bot {i} follows the route");
        }

        bool EveryKartLapped()
        {
            for (var i = 0; i < karts.Length; i++)
            {
                if (world.Get<RaceProgress>(karts[i]).CompletedLaps < 1)
                    return false;
            }

            return true;
        }
    }

    [TestCase("star-circuit")]
    [TestCase("norbergring")]
    public void FixedStep_SixKartsOnATrackWithHeight_DoesNotAllocate(string id)
    {
        var track = id == "norbergring" ? Norbergring.Value : Track.Load(TracksFolder, id);
        using var world = new World();
        var tunings = new KartTuning[6];
        Array.Fill(tunings, KartCatalog.Kart);
        var markers = RaceMarkers.Load(TracksFolder, id);
        var race = TrackScene.CreateSession(track, markers, laps: 5);
        TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: 5, race);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings, race);
        for (var step = 0; step < (RaceSession.CountdownSeconds + 3) * 120; step++)
            group.Update(Step);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        const int steps = 1200;
        for (var step = 0; step < steps; step++)
            group.Update(Step);
        var microseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMicroseconds / steps;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.Out.WriteLine($"6 karts on {id}: {microseconds:0} µs per fixed step");
        Assert.That(allocated, Is.Zero);
    }

    //** drawing

    [TestCase("star-circuit")]
    [TestCase("norbergring")]
    [TestCase("dunsfold-neon")]
    [TestCase("kinnekulle")]
    [TestCase("brands-hatch-indy")]
    public void Draw_ChaseViewsRoundTheTrack_DrawsTheGroundAndPrintsTheFrameTime(string id)
    {
        var track = id == "norbergring" ? Norbergring.Value : Track.Load(TracksFolder, id);
        var sky = new uint[320 * 180];
        using var cpu = new ConsoleEngine.Rendering3D.Rasterizer(320, 180, KartsterGame.ScenePalette, trueColor: true);
        var cpuMs = Frames(cpu, track, sky, out var tiles);
        var gpuMs = double.NaN;
        if (ConsoleEngine.Rendering3D.OpenGL.GlRenderer.TryCreate(320, 180, KartsterGame.ScenePalette, true, out var gpu))
        {
            using (gpu)
                gpuMs = Frames(gpu, track, sky, out _);
        }

        TestContext.Out.WriteLine($"{id}: {cpuMs:0.00} ms a frame on the CPU, {gpuMs:0.00} ms on the GPU, " +
                                  $"{tiles / 40f:0} terrain tiles drawn a frame");
        Assert.That(track.Terrain == null || tiles > 0, Is.True, "the terrain is drawn");
    }

    // 40 chase-camera frames round the route (ground, road, scenery: KartRenderSystem's big draws), ms a frame.
    private static double Frames(ConsoleEngine.Rendering3D.IRenderer3D renderer, Track track, uint[] sky,
        out int terrainTiles)
    {
        var projection = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(50f * MathF.PI / 180f, 320f / 180f,
            0.2f, 400f);
        terrainTiles = 0;
        var start = 0L;
        for (var frame = -5; frame < 40; frame++) // five to warm up
        {
            if (frame == 0)
                start = System.Diagnostics.Stopwatch.GetTimestamp();
            var (position, forward) = track.Sample(Math.Max(frame, 0) * track.Length / 40f);
            var eye = position - forward * 5.5f + Vector3.UnitY * 2.2f;
            var view = System.Numerics.Matrix4x4.CreateLookAt(eye, position + forward * 4f + Vector3.UnitY,
                Vector3.UnitY);
            var viewProjection = view * projection;
            renderer.Clear(sky);
            if (track.Terrain == null)
                track.DrawGround(renderer, viewProjection, eye, KartsterGame.ToSun);
            else // what DrawGround does, counted
                terrainTiles += track.Terrain.Tiles.Draw(renderer, viewProjection, eye, KartsterGame.ToSun, 400f);
            renderer.Draw(track.Road, System.Numerics.Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
            track.Scenery.Draw(renderer, viewProjection, eye, KartsterGame.ToSun);
            renderer.Finish();
        }

        return System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds / 40;
    }

    //** helpers

    private static SystemGroup Driving(World world, Track track)
    {
        var group = new SystemGroup(world);
        group.Add(new KartPhysicsSystem(KartCatalog.Kart));
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        group.Add(new KartGroundSystem(track));
        return group;
    }

    private static Entity Place(World world, Track track, Vector2 position, float heading, float? height = null)
    {
        var kart = KartEntity.Create(world, KartCatalog.Kart, position, heading);
        ref var state = ref world.Get<Kart>(kart);
        if (height.HasValue)
            state.Height = height.Value;
        else
            track.TryGetRoadHeight(new Vector3(position.X, 0f, position.Y), out state.Height);
        return kart;
    }

    private static void Run(SystemGroup group, float seconds)
    {
        for (var step = 0; step < seconds / Step; step++)
            group.Update(Step);
    }

    private static float SpeedAfter(Track track, float heading, float seconds)
    {
        using var world = new World();
        var group = Driving(world, track);
        var kart = Place(world, track, new Vector2(0f, -100f), heading);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f };
        Run(group, seconds);
        return world.Get<Velocity>(kart).Value.Length();
    }

    /// <summary>A straight road 10 m wide along -Z, 200 m long, rising by <paramref name="slope"/> towards -Z.</summary>
    private static Track Ramp(float slope) => Profile(z => -z * slope, 0f, -200f);

    /// <summary>A straight road 10 m wide along -Z from <paramref name="from"/> to <paramref name="to"/>, y(z).</summary>
    private static Track Profile(Func<float, float> height, float from, float to)
    {
        var road = new Ribbon();
        for (var z = from; z > to; z -= 1f)
            road.Quad(new Vector3(-5f, height(z), z), new Vector3(5f, height(z), z),
                new Vector3(5f, height(z - 1f), z - 1f), new Vector3(-5f, height(z - 1f), z - 1f));
        var spline = new List<Vector3>();
        for (var z = from; z >= to; z -= 1f)
            spline.Add(new Vector3(0f, height(z), z));
        return Write("profile", road, spline);
    }

    /// <summary>
    /// Two straight roads 10 m wide crossing at the origin: along X on the ground (y = 0) and along Z on a deck 6 m up.
    /// </summary>
    private static Track Crossing()
    {
        var road = new Ribbon();
        for (var x = -100f; x < 100f; x += 2f)
            road.Quad(new Vector3(x, 0f, 5f), new Vector3(x + 2f, 0f, 5f), new Vector3(x + 2f, 0f, -5f),
                new Vector3(x, 0f, -5f));
        for (var z = 100f; z > -100f; z -= 2f)
            road.Quad(new Vector3(-5f, 6f, z), new Vector3(5f, 6f, z), new Vector3(5f, 6f, z - 2f),
                new Vector3(-5f, 6f, z - 2f));
        var spline = new List<Vector3>();
        for (var x = -100f; x <= 100f; x += 1f)
            spline.Add(new Vector3(x, 0f, 0f));
        return Write("crossing", road, spline);
    }

    private static Track Write(string id, Ribbon road, List<Vector3> spline)
    {
        var folder = Path.Combine(Path.GetTempPath(), "kartster-height-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, id + ".ribbon.obj"), road.ToObj());
        var text = new StringBuilder();
        foreach (var point in spline)
            text.Append(CultureInfo.InvariantCulture, $"v {point.X} {point.Y} {point.Z}\n");
        File.WriteAllText(Path.Combine(folder, id + ".spline.obj"), text.ToString());
        try
        {
            return Track.Load(folder, id);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static bool BruteForceRoadHeight(Track track, Vector3 position, out float height)
    {
        var road = track.Road;
        var found = false;
        height = float.MinValue;
        for (var t = 0; t < road.TriangleCount; t++)
        {
            if (road.TriangleNormals[t].Y <= 0.5f)
                continue;
            var a = road.Positions[road.Indices[t * 3]];
            var b = road.Positions[road.Indices[t * 3 + 1]];
            var c = road.Positions[road.Indices[t * 3 + 2]];
            var area = Cross(a, b, c);
            if (area == 0)
                continue;
            var weightA = Cross(b, c, position) / area;
            var weightB = Cross(c, a, position) / area;
            var weightC = 1 - weightA - weightB;
            if (weightA < 0 || weightB < 0 || weightC < 0)
                continue;
            height = MathF.Max(height, weightA * a.Y + weightB * b.Y + weightC * c.Y);
            found = true;
        }

        return found;

        static float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
    }

    /// <summary>Road quads as OBJ faces, counter-clockwise seen from above.</summary>
    private sealed class Ribbon
    {
        private readonly StringBuilder _vertices = new();
        private readonly StringBuilder _faces = new();
        private int _count;

        /// <param name="a">Then b, c, d: +X from a, then towards -Z, which faces up.</param>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            foreach (var point in (ReadOnlySpan<Vector3>)[a, b, c, d])
                _vertices.Append(CultureInfo.InvariantCulture, $"v {point.X} {point.Y} {point.Z}\n");
            _faces.Append(CultureInfo.InvariantCulture, $"f {_count + 1} {_count + 2} {_count + 3}\n");
            _faces.Append(CultureInfo.InvariantCulture, $"f {_count + 1} {_count + 3} {_count + 4}\n");
            _count += 4;
        }

        public string ToObj() => _vertices.ToString() + _faces;
    }
}
