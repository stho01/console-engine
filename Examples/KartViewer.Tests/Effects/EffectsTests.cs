using System;
using System.Numerics;
using KartViewer.Driving;
using KartViewer.Effects;
using NUnit.Framework;

namespace KartViewer.Tests.Effects;

public class SkidMarksTests
{
    private static readonly Vector3 Road = new(0.33f);

    /// <summary>Drives straight along -Z for <paramref name="metres"/>, a frame every 10 cm.</summary>
    private static SkidMarks Drive(float frontSlip, float rearSlip, bool onAsphalt = true, float metres = 5f)
    {
        var marks = new SkidMarks(Road);
        for (var z = 0f; z <= metres; z += 0.1f)
            marks.Update(new KartWheels(new Vector3(0, 0, -z), -Vector3.UnitZ, KartCatalog.Kart), frontSlip, rearSlip,
                onAsphalt);
        return marks;
    }

    private static int Segments(SkidMarks marks)
    {
        var count = 0;
        var positions = marks.Mesh.Positions;
        for (var v = 0; v < positions.Length; v += 4)
            if (positions[v] != positions[v + 2])
                count++;
        return count;
    }

    [Test]
    public void Update_RearSliding_MarksBehindBothRearWheelsOnly()
    {
        var marks = Drive(frontSlip: 1f, rearSlip: 5f);

        Assert.That(Segments(marks), Is.EqualTo(2 * 9)); // 4.5 m in 0.5 m segments, two wheels
    }

    [Test]
    public void Update_FrontSlipAlone_LeavesNoMarks()
    {
        Assert.That(Segments(Drive(frontSlip: 8f, rearSlip: 0f)), Is.Zero);
    }

    [Test]
    public void Update_Spinning_MarksAllFourWheels()
    {
        Assert.That(Segments(Drive(frontSlip: 10f, rearSlip: 10f)), Is.EqualTo(4 * 9));
    }

    [Test]
    public void Update_OnGrass_LeavesNoMarks()
    {
        Assert.That(Segments(Drive(frontSlip: 10f, rearSlip: 10f, onAsphalt: false)), Is.Zero);
    }

    [Test]
    public void Update_Marks_FaceUpAndDarkenTheRoad()
    {
        var marks = Drive(frontSlip: 0f, rearSlip: 10f);
        var p = marks.Mesh.Positions;

        var normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        Assert.That(normal.Y, Is.GreaterThan(0f));
        Assert.That(marks.Mesh.TriangleColors[0].X, Is.LessThan(Road.X));
    }

    [Test]
    public void Update_JumpFurtherThan5Metres_StartsANewStripInsteadOfALine()
    {
        var marks = new SkidMarks(Road);
        marks.Update(new KartWheels(Vector3.Zero, -Vector3.UnitZ, KartCatalog.Kart), 0f, 5f, true);
        marks.Update(new KartWheels(new Vector3(0, 0, -20), -Vector3.UnitZ, KartCatalog.Kart), 0f, 5f, true);

        Assert.That(Segments(marks), Is.Zero);
    }

    [Test]
    public void Update_MoreSegmentsThanTheRing_WrapsWithoutAllocating()
    {
        var marks = new SkidMarks(Road);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var z = 0f; z < SkidMarks.MaximumSegments; z += 0.1f)
            marks.Update(new KartWheels(new Vector3(0, 0, -z), -Vector3.UnitZ, KartCatalog.Kart), 0f, 5f, true);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        Assert.That(Segments(marks), Is.EqualTo(SkidMarks.MaximumSegments));
    }
}

public class SlideSmokeTests
{
    private const float Frame = 1f / 100f;
    private static readonly KartWheels Wheels = new(Vector3.Zero, -Vector3.UnitZ, KartCatalog.Kart);

    private static void Run(SlideSmoke smoke, float frontSlip, float rearSlip, float seconds)
    {
        for (var t = 0f; t < seconds - 1e-4f; t += Frame)
            smoke.Update(Wheels, new Vector3(2f, 0f, -20f), Vector3.UnitX, frontSlip, rearSlip, Frame);
    }

    [Test]
    public void Update_FrontAxleFullySliding_PuffsAtFullRateFromBothFrontWheels()
    {
        var smoke = new SlideSmoke();

        Run(smoke, frontSlip: 7f, rearSlip: 0f, seconds: 0.5f);

        Assert.That(smoke.Count, Is.EqualTo((int)(2 * SlideSmoke.RatePerWheel * 0.5f)).Within(2));
    }

    [Test]
    public void Update_NotSliding_MakesNoSmoke()
    {
        var smoke = new SlideSmoke();

        Run(smoke, frontSlip: SlideSmoke.SlidingThreshold, rearSlip: 0f, seconds: 1f);

        Assert.That(smoke.Count, Is.Zero);
    }

    [Test]
    public void Update_AfterTheSlideStops_PuffsAreGoneWithin1point1Seconds()
    {
        var smoke = new SlideSmoke();
        Run(smoke, frontSlip: 7f, rearSlip: 7f, seconds: 0.5f);

        Run(smoke, frontSlip: 0f, rearSlip: 0f, seconds: 1.2f);

        Assert.That(smoke.Count, Is.Zero);
    }

    [Test]
    public void BuildQuads_LivePuff_FacesTheCameraAndIsSeeThrough()
    {
        var smoke = new SlideSmoke();
        Run(smoke, frontSlip: 7f, rearSlip: 0f, seconds: 0.1f);

        smoke.BuildQuads(Vector3.UnitX, Vector3.UnitY); // camera looking down -Z
        var p = smoke.Mesh.Positions;

        Assert.That(Vector3.Cross(p[1] - p[0], p[2] - p[0]).Z, Is.GreaterThan(0f)); // towards the camera
        Assert.That(smoke.Mesh.TriangleOpacity[0], Is.InRange(0.01f, 0.36f));
    }

    [Test]
    public void UpdateAndBuildQuads_InSteadyState_DoNotAllocate()
    {
        var smoke = new SlideSmoke();
        Run(smoke, 7f, 7f, 1f);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            smoke.Update(Wheels, Vector3.UnitZ, Vector3.UnitX, 7f, 7f, Frame);
            smoke.BuildQuads(Vector3.UnitX, Vector3.UnitY);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
