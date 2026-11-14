using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Collision;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.Collision;

/// <summary>The collision file, and karts against walls and posts at Kartster's 120 steps per second.</summary>
public class TrackCollisionTests
{
    private const float Step = TrackScene.StepSeconds;
    private static readonly string TracksFolder = Path.Combine("Assets", "Tracks");

    private static readonly Lazy<Track> StarCircuit = new(() => Track.Load(TracksFolder, "star-circuit"));

    //** parsing

    [Test]
    public void Parse_SegmentsCirclesAndComments_ReadsShapes()
    {
        var collision = TrackCollision.Parse(
            ["# walls", "", "segment 1 2 3 4 0.25  # fence", "circle -5 6.5 1.2"], "test.collision.txt");

        Assert.That(collision.Segments, Is.EqualTo(new[]
        {
            new WallSegment(new(1f, 2f), new(3f, 4f), 0.25f),
        }));
        Assert.That(collision.Posts, Is.EqualTo(new[] { new WallPost(new(-5f, 6.5f), 1.2f) }));
    }

    [TestCase("segment 1 2 3 0.25", "test.collision.txt:1: expected 'segment x1 y1 x2 y2 halfThickness'")]
    [TestCase("circle 1 2", "test.collision.txt:1: expected 'circle x y radius'")]
    [TestCase("circle 1 x 2", "test.collision.txt:1: 'x' is not a number")]
    [TestCase("circle 1 2 0", "test.collision.txt:1: '0' must be more than 0")]
    [TestCase("segment 1 2 1 2 0.25", "test.collision.txt:1: segment has no length")]
    [TestCase("wall 1 2", "test.collision.txt:1: unknown line 'wall', expected segment or circle")]
    public void Parse_BadLine_ThrowsWithFileAndLine(string line, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => TrackCollision.Parse([line], "test.collision.txt"));

        Assert.That(error.Message, Is.EqualTo(message));
    }

    [Test]
    public void Load_NoFile_IsEmpty()
    {
        var collision = TrackCollision.Load(TracksFolder, "no-such-track");

        Assert.That(collision, Is.SameAs(TrackCollision.Empty));
    }

    [Test]
    public void Load_StarCircuit_HasOuterWallFencesAndPipes()
    {
        var collision = TrackCollision.Load(TracksFolder, "star-circuit");
        using var world = new World();
        var group = new SystemGroup(world);
        var physics = new PhysicsSystem();
        group.Add(physics);

        var bodies = collision.AddTo(world);
        group.Update(Step);

        Assert.That(collision.Segments, Has.Length.EqualTo(4 + 15), "outer wall + fences");
        Assert.That(collision.Posts, Has.Length.EqualTo(10), "pipes");
        Assert.That(collision.Segments, Has.All.Matches<WallSegment>(s => s.HalfThickness == 0.25f));
        Assert.That(collision.Posts, Has.All.Matches<WallPost>(p => p.Radius == 1.2f));
        Assert.That(bodies, Is.EqualTo(29));
        Assert.That(physics.BodyCount, Is.EqualTo(29));
        Assert.That(physics.ContactCount, Is.Zero, "static bodies never touch each other");
    }

    //** karts against walls

    [Test]
    public void Kart_FullThrottleIntoFenceAt32MetresPerSecond_StopsAtItWithoutTunnelling()
    {
        using var world = new World();
        Wall(world, new(-20f, 0f), new(20f, 0f));
        var (group, _) = Driving(world, out var kart, new Vector2(0f, 6f), heading: 0f); // faces -y, at the wall
        world.Get<Velocity>(kart).Value = new Vector2(0f, -32f);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f };
        var front = KartCatalog.Kart.ChassisLengthMeters / 2f;

        var nearest = float.MaxValue;
        var fastestRebound = 0f;
        for (var step = 0; step < 2 * 120; step++)
        {
            group.Update(Step);
            nearest = MathF.Min(nearest, world.Get<Position>(kart).Value.Y);
            fastestRebound = MathF.Max(fastestRebound, world.Get<Velocity>(kart).Value.Y);
        }

        TestContext.Out.WriteLine($"nearest centre {nearest:0.000} m, rebound {fastestRebound:0.00} m/s");
        Assert.That(nearest, Is.GreaterThan(0.25f + front - 0.1f), "the nose stops at the wall face");
        Assert.That(world.Get<Position>(kart).Value.Y, Is.GreaterThan(0.25f + front - 0.1f));
        Assert.That(fastestRebound, Is.LessThan(32f * 0.15f), "restitution 0.1: no high bounce");
        Assert.That(world.Get<Velocity>(kart).Value.Length(), Is.LessThan(1f), "held against the wall");
    }

    [Test]
    public void Kart_IntoWallAt45Degrees_SlidesAlongItAndKeepsSomeSpeed()
    {
        using var world = new World();
        Wall(world, new(-60f, -60f), new(60f, 60f)); // the line y = x, a turned box
        var (group, _) = Driving(world, out var kart, new Vector2(0f, 6f), heading: 0f); // straight down onto it
        world.Get<Velocity>(kart).Value = new Vector2(0f, -25f);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f };
        var alongWall = Vector2.Normalize(new Vector2(-1f, -1f));
        var fromWall = Vector2.Normalize(new Vector2(-1f, 1f));
        var start = world.Get<Position>(kart).Value;

        var nearest = float.MaxValue;
        for (var step = 0; step < 120 + 60; step++)
        {
            group.Update(Step);
            nearest = MathF.Min(nearest, Vector2.Dot(world.Get<Position>(kart).Value, fromWall));
        }

        var travelled = Vector2.Dot(world.Get<Position>(kart).Value - start, alongWall);
        var speed = world.Get<Velocity>(kart).Value.Length();
        TestContext.Out.WriteLine($"along the wall {travelled:0.0} m, speed {speed:0.0} m/s, nearest {nearest:0.00} m");
        Assert.That(nearest, Is.GreaterThan(0.25f + KartCatalog.Kart.ChassisWidthMeters / 2f - 0.1f),
            "stays on its side of the wall");
        Assert.That(travelled, Is.GreaterThan(15f), "slides along the wall");
        Assert.That(speed, Is.GreaterThan(5f), "a glancing hit costs speed, not all of it");
    }

    [Test]
    public void Kart_IntoPipeOffCentre_IsDeflectedAway()
    {
        using var world = new World();
        new TrackCollision([], [new WallPost(new(0f, 0f), 1.2f)]).AddTo(world);
        var (group, _) = Driving(world, out var kart, new Vector2(0.8f, 8f), heading: 0f);
        world.Get<Velocity>(kart).Value = new Vector2(0f, -20f);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f };

        var nearest = float.MaxValue;
        for (var step = 0; step < 120; step++)
        {
            group.Update(Step);
            nearest = MathF.Min(nearest, world.Get<Position>(kart).Value.Length());
        }

        var position = world.Get<Position>(kart).Value;
        TestContext.Out.WriteLine($"ends at {position}, nearest {nearest:0.00} m");
        Assert.That(nearest, Is.GreaterThan(1.2f), "never inside the pipe");
        Assert.That(position.X, Is.GreaterThan(1.5f), "pushed off to the side it hit it on");
    }

    //** races

    [Test]
    public void Race_ThreeBotsWithWallsFor60Seconds_EveryBotCompletesALap()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = Tunings(3);
        var markers = RaceMarkers.Load(TracksFolder, "star-circuit");
        var race = TrackScene.CreateSession(track, markers, laps: 5);
        var karts = TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: -1, race);
        TrackCollision.Load(TracksFolder, "star-circuit").AddTo(world);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings, race);

        for (var step = 0; step < (60 + RaceSession.CountdownSeconds) * 120; step++)
            group.Update(Step);

        for (var i = 0; i < karts.Length; i++)
        {
            var progress = world.Get<RaceProgress>(karts[i]);
            TestContext.Out.WriteLine($"bot {i}: {progress.CompletedLaps} laps, best {progress.BestLapSeconds:0.0} s");
            Assert.That(progress.CompletedLaps, Is.GreaterThanOrEqualTo(1), $"bot {i} drives on");
            Assert.That(progress.MissedCheckpoints, Is.Zero, $"bot {i} follows the route");
        }
    }

    [Test]
    public void FixedStep_SixKartsWithWalls_DoesNotAllocate()
    {
        MeasureSixKarts(walls: true, out _); // warms the JIT up, so the two runs compare
        var withoutWalls = MeasureSixKarts(walls: false, out _);
        var withWalls = MeasureSixKarts(walls: true, out var allocated);

        TestContext.Out.WriteLine($"6 karts: {withoutWalls:0} µs per fixed step without walls, {withWalls:0} µs with");
        Assert.That(allocated, Is.Zero);
    }

    //** helpers

    private static double MeasureSixKarts(bool walls, out long allocated)
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = Tunings(6);
        var markers = RaceMarkers.Load(TracksFolder, "star-circuit");
        var race = TrackScene.CreateSession(track, markers, laps: 5);
        var karts = TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: 5, race);
        if (walls)
            TrackCollision.Load(TracksFolder, "star-circuit").AddTo(world);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings, race);
        world.Get<KartControls>(karts[5]) = new KartControls { Throttle = 1f, Steer = 0.2f };
        for (var step = 0; step < (RaceSession.CountdownSeconds + 2) * 120; step++)
            group.Update(Step);

        const int steps = 600;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var step = 0; step < steps; step++)
            group.Update(Step);
        var microseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMicroseconds / steps;
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return microseconds;
    }

    private static void Wall(World world, System.Numerics.Vector2 start, System.Numerics.Vector2 end) =>
        new TrackCollision([new WallSegment(start, end, 0.25f)], []).AddTo(world);

    /// <summary>One <see cref="KartCatalog.Kart"/> on the kart model and the physics, no track under it.</summary>
    private static (SystemGroup Group, KartPhysicsSystem Physics) Driving(World world, out Entity kart,
        Vector2 position, float heading)
    {
        var tunings = new[] { KartCatalog.Kart };
        kart = KartEntity.Create(world, tunings[0], position, heading);
        var group = new SystemGroup(world);
        var physics = new KartPhysicsSystem(tunings);
        group.Add(physics);
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        return (group, physics);
    }

    private static KartTuning[] Tunings(int count)
    {
        KartTuning[] catalog = [KartCatalog.Olabil, KartCatalog.Wagon, KartCatalog.Kart];
        var tunings = new KartTuning[count];
        for (var i = 0; i < count; i++)
            tunings[i] = catalog[i % catalog.Length];
        return tunings;
    }
}
