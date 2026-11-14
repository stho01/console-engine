using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Kartster.Collision;
using Kartster.Race;
using NUnit.Framework;

namespace Kartster.Tests;

/// <summary>Every track in <c>Assets/Tracks</c> (what the track menu lists) loads and can be raced.</summary>
public class ShippedTracksTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks");

    private static IEnumerable<string> TrackIds()
    {
        foreach (var ribbon in Directory.GetFiles(Folder, "*.ribbon.obj"))
            yield return Path.GetFileName(ribbon)[..^".ribbon.obj".Length];
    }

    [Test]
    public void TracksFolder_HasTheExportedTracks()
    {
        Assert.That(TrackIds(), Is.SupersetOf(new[] { "eight-ball", "prism-loop", "star-circuit", "star-rally" }));
    }

    [TestCaseSource(nameof(TrackIds))]
    public void Load_ShippedTrack_HasRoadSceneryAndAClosedRoute(string id)
    {
        var track = Track.Load(Folder, id);

        Assert.That(track.Road.TriangleCount, Is.GreaterThan(500));
        Assert.That(track.Scenery.Count, Is.GreaterThan(0), "scenery and its models load");
        Assert.That(track.IsClosed, Is.True, "the exporter writes race markers for closed routes only");
    }

    [TestCaseSource(nameof(TrackIds))]
    public void Load_ShippedTrack_HasWalls(string id)
    {
        var collision = TrackCollision.Load(Folder, id);

        Assert.That(collision, Is.Not.SameAs(TrackCollision.Empty), "the exporter writes collision for every track");
        Assert.That(collision.Segments.Length + collision.Posts.Length, Is.GreaterThan(0));
    }

    [TestCaseSource(nameof(TrackIds))]
    public void Load_ShippedTrack_PowerUpPadsAreOnTheRoad(string id)
    {
        var track = Track.Load(Folder, id);

        Assert.That(File.Exists(Path.Combine(Folder, id + ".powerups.txt")), Is.True);
        Assert.That(track.PowerUpPads, Is.Not.Empty);
        for (var i = 0; i < track.PowerUpPads.Length; i++)
        {
            var pad = track.PowerUpPads[i];
            Assert.That(OnRoad(track, new Vector2(pad.X, pad.Z)), Is.True, $"pad {i} at {pad}");
        }
    }

    [TestCaseSource(nameof(TrackIds))]
    public void Load_ShippedTrack_StartCheckpointsAndGridAreOnTheRoad(string id)
    {
        var track = Track.Load(Folder, id);
        var markers = RaceMarkers.Load(Folder, id);

        Assert.That(markers.Grid, Has.Length.EqualTo(8));
        Assert.That(OnRoad(track, markers.Start.Point), Is.True, "start");
        for (var i = 0; i < markers.Checkpoints.Length; i++)
            Assert.That(OnRoad(track, markers.Checkpoints[i].Point), Is.True, $"checkpoint {i}");
        for (var i = 0; i < markers.Grid.Length; i++)
            Assert.That(OnRoad(track, markers.Grid[i].Position), Is.True, $"grid slot {i}");
    }

    private static bool OnRoad(Track track, Vector2 point) =>
        track.TryGetRoadHeight(new Vector3(point.X, 0f, point.Y), out _);
}
