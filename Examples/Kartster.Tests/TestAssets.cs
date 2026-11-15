using System;
using System.IO;
using ConsoleEngine.Assets;
using Kartster.Collision;
using Kartster.Loading;
using Kartster.Race;

namespace Kartster.Tests;

/// <summary>The shipped content, read as the game reads it: <c>Assets/</c> next to the test assembly.</summary>
internal static class TestAssets
{
    /// <summary>The folder on disk behind <see cref="Shipped"/>, for tests that look at the files themselves.</summary>
    public static readonly string Root = Path.Combine(AppContext.BaseDirectory, DirectoryAssetSource.DefaultFolder);

    public static readonly AssetManager Shipped = In(Root);

    public static readonly AssetPath Karts = KartsterAssets.Karts;
    public static readonly AssetPath Tracks = KartsterAssets.Tracks;
    public static readonly AssetPath Scenery = "Scenery";

    /// <summary>An asset manager with Kartster's loaders over any folder, for files a test writes.</summary>
    public static AssetManager In(string folder) =>
        KartsterAssets.Register(new AssetManager(new DirectoryAssetSource(folder)));

    public static AssetPath Kart(string name) => Karts.Resolve(name);

    public static Track Track(string id) => Kartster.Track.Load(Shipped, Tracks, id);

    public static RaceMarkers Markers(string id) => RaceMarkers.Load(Shipped, Tracks, id);

    public static TrackCollision Collision(string id) => TrackCollision.Load(Shipped, Tracks, id);
}
