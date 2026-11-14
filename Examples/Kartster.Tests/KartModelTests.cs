using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Kartster.Tests;

public class KartModelTests
{
    [Test]
    public void Load_ShippedKart_SkipsMarkerParts()
    {
        var mesh = KartModel.Load(Path.Combine("Assets", "Karts", "olabil"), Vector3.One);

        Assert.That(mesh.TriangleCount, Is.GreaterThan(1000));
        Assert.That(mesh.TriangleColors, Has.Length.EqualTo(mesh.TriangleCount));
    }
}
