using System.IO;
using Doom.Maps;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

public class MapTests
{
    [Test]
    public void Load_TestMap_ReadsGeometry()
    {
        var (wad, _, textures, _) = TestWad.Load();

        var map = MapLoader.Load(wad, "E1M1", textures);

        Assert.That(map.Sectors, Has.Length.EqualTo(3));
        Assert.That(map.Lines, Has.Length.EqualTo(14));
        Assert.That(map.Subsectors, Has.Length.EqualTo(3));
        Assert.That(map.Things, Has.Length.EqualTo(2));
        Assert.That(map.Lines[4].Back, Is.Not.Null);
        Assert.That(map.Lines[0].Back, Is.Null);
    }

    [Test]
    public void Load_MissingMap_ThrowsInvalidData()
    {
        var (wad, _, textures, _) = TestWad.Load();

        Assert.Throws<InvalidDataException>(() => MapLoader.Load(wad, "E1M2", textures));
    }

    [TestCase(64, 128, 0)]
    [TestCase(264, 128, 1)]
    [TestCase(300, 20, 2)]
    public void SectorAt_Point_FindsSectorThroughNodes(float x, float y, int expected)
    {
        var (wad, _, textures, _) = TestWad.Load();
        var map = MapLoader.Load(wad, "E1M1", textures);

        Assert.That(map.SectorAt(x, y).Index, Is.EqualTo(expected));
    }

    [Test]
    public void Load_Sectors_ResolveFlats()
    {
        var (wad, _, textures, _) = TestWad.Load();

        var map = MapLoader.Load(wad, "E1M1", textures);

        Assert.That(map.Sectors[2].CeilingFlat, Is.EqualTo(textures.SkyFlat));
        Assert.That(map.Sectors[0].FloorFlat, Is.EqualTo(textures.FindFlat("FLOOR")));
    }

    [Test]
    public void Neighbours_DoorSector_AreBothRooms()
    {
        var (wad, _, textures, _) = TestWad.Load();
        var map = MapLoader.Load(wad, "E1M1", textures);

        var neighbours = Map.Neighbours(map.Sectors[TestWad.DoorSector]);

        Assert.That(neighbours, Has.Count.EqualTo(2));
    }

    [TestCase("E1M1", false, "E1M2")]
    [TestCase("E1M8", false, null)]
    [TestCase("E1M3", true, "E1M9")]
    [TestCase("E1M9", false, "E1M4")]
    [TestCase("MAP01", false, "MAP02")]
    [TestCase("MAP15", true, "MAP31")]
    [TestCase("MAP31", false, "MAP16")]
    [TestCase("MAP30", false, null)]
    public void Next_Map_FollowsEpisodeOrder(string current, bool secret, string expected) =>
        Assert.That(MapNames.Next(current, secret), Is.EqualTo(expected));

    [Test]
    public void Find_TestWad_ListsTheMap()
    {
        var (wad, _, _, _) = TestWad.Load();

        Assert.That(MapNames.Find(wad), Is.EqualTo(new[] { "E1M1" }));
    }
}
