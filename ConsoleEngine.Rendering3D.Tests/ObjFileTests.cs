using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

public class ObjFileTests
{
    private string _path;

    [SetUp]
    public void SetUp() => _path = Path.GetTempFileName();

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Read_QuadWithSlashedCorners_BecomesTwoTriangles()
    {
        File.WriteAllText(_path, "# quad\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1.5 0\nvn 0 0 1\nf 1/1/1 2/2/1 3//1 4\n");
        var positions = new List<Vector3>();
        var indices = new List<int>();

        ObjFile.Read(_path, positions, indices);

        Assert.That(positions, Has.Count.EqualTo(4));
        Assert.That(positions[3], Is.EqualTo(new Vector3(0, 1.5f, 0)));
        Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
    }

    [Test]
    public void Read_SecondFile_OffsetsIndicesPastFirstFilesPositions()
    {
        File.WriteAllText(_path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        var positions = new List<Vector3>();
        var indices = new List<int>();

        ObjFile.Read(_path, positions, indices);
        ObjFile.Read(_path, positions, indices);

        Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void Read_NegativeIndices_CountFromLastPosition()
    {
        File.WriteAllText(_path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf -3 -2 -1\n");
        var positions = new List<Vector3>();
        var indices = new List<int>();

        ObjFile.Read(_path, positions, indices);

        Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2 }));
    }
}
