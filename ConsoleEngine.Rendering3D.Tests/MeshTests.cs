using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

public class MeshTests
{
    [Test]
    public void MarkChanged_Twice_BumpsVersionByTwo()
    {
        var mesh = Mesh.CreateFloor(1f, 1, Vector3.One, Vector3.One);
        var before = mesh.Version;

        mesh.MarkChanged();
        mesh.MarkChanged();

        Assert.That(mesh.Version, Is.EqualTo(before + 2));
    }
}
