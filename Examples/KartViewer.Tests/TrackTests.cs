using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace KartViewer.Tests;

public class TrackTests
{
    private static Track StarCircuit() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit");

    [Test]
    public void Load_ShippedTrack_IsAClosedLoopWithARoad()
    {
        var track = StarCircuit();

        Assert.That(track.IsClosed, Is.True);
        Assert.That(track.Length, Is.GreaterThan(500f));
        Assert.That(track.Road.TriangleCount, Is.GreaterThan(1000));
    }

    [Test]
    public void Sample_PastTheEndOfAClosedTrack_WrapsToTheStart()
    {
        var track = StarCircuit();

        var (start, _) = track.Sample(10);
        var (wrapped, _) = track.Sample(track.Length + 10);

        Assert.That(Vector3.Distance(start, wrapped), Is.LessThan(0.01f));
    }

    [Test]
    public void Sample_Forward_PointsToWhereTheRouteGoesNext()
    {
        var track = StarCircuit();

        var (here, forward) = track.Sample(50);
        var (ahead, _) = track.Sample(52);

        Assert.That(forward.Length(), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(Vector3.Dot(Vector3.Normalize(ahead - here), forward), Is.GreaterThan(0.95f));
    }

    [Test]
    public void TryGetRoadHeight_OnTheCenterline_IsOnTheRoad()
    {
        var track = StarCircuit();
        var (onCenterline, _) = track.Sample(100);

        Assert.That(track.TryGetRoadHeight(onCenterline, out var height), Is.True);
        Assert.That(height, Is.EqualTo(onCenterline.Y).Within(0.01f));
    }

    [Test]
    public void TryGetRoadHeight_FarOutside_IsGrassAtGroundHeight()
    {
        var track = StarCircuit();
        var outside = track.GroundCenter + new Vector3(track.GroundHalfSize - 1, 0, track.GroundHalfSize - 1);

        Assert.That(track.TryGetRoadHeight(outside, out var height), Is.False);
        Assert.That(height, Is.EqualTo(track.GroundHeight));
    }
}
