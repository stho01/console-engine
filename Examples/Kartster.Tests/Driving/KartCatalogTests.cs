using System;
using System.Collections.Generic;
using System.IO;
using Kartster.Driving;
using NUnit.Framework;

namespace Kartster.Tests.Driving;

/// <summary>Every shipped kart folder has its own tuning, and each drives without the physics blowing up.</summary>
public class KartCatalogTests
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Dt = 1f / 120;
    private const int StepsPerSecond = 120;

    // Kartster tunes these with TuneGoKart on the default chassis: the same as the Kart.
    private static readonly string[] GoKartTuned = ["Kart", "banana", "hotdog", "kitcar", "lightcycle", "trike"];

    //**********************************************************
    //** tests
    //**********************************************************

    [Test]
    public void ShippedKarts_AreAllOfKartstersButUnicycleAndMcLuring()
    {
        Assert.That(ShippedKarts(), Is.EquivalentTo(new[]
        {
            "Kart", "baffi", "banana", "delorelei", "elephant", "halden", "hektor", "hotdog", "itg", "kitcar",
            "lasagna", "lightcycle", "olabil", "oleg", "pooper", "raleur", "rolf", "trike", "wagon", "whosp",
        }));
    }

    [TestCaseSource(nameof(ShippedKarts))]
    public void ForKart_ShippedFolder_HasItsOwnEntry(string name)
    {
        var tuning = KartCatalog.ForKart(name);

        if (Array.IndexOf(GoKartTuned, name) >= 0)
            Assert.That(tuning, Is.SameAs(KartCatalog.Kart));
        else
            Assert.That(tuning, Is.Not.SameAs(KartCatalog.Kart));
    }

    [TestCaseSource(nameof(ShippedKarts))]
    public void FullThrottle_For60Seconds_ReachesThePowerBalanceThenTurnsWithoutBlowingUp(string name)
    {
        var kart = new KartDynamics(KartCatalog.ForKart(name));
        var body = default(KartBody);

        Run(kart, ref body, new KartControls { Throttle = 1f }, 60 * StepsPerSecond);
        Assert.That(body.Velocity.Length(), Is.EqualTo(kart.TopSpeed).Within(2).Percent);

        Run(kart, ref body, new KartControls { Throttle = 1f, Steer = 1f }, 60 * StepsPerSecond);
        Assert.That(float.IsFinite(body.Position.X) && float.IsFinite(body.Position.Y), Is.True);
        Assert.That(float.IsFinite(body.Heading) && float.IsFinite(body.AngularVelocity), Is.True);
        Assert.That(body.Velocity.Length(), Is.LessThan(kart.TopSpeed * 1.05f));
        Assert.That(MathF.Abs(body.AngularVelocity), Is.LessThan(10f));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static IEnumerable<string> ShippedKarts()
    {
        foreach (var folder in Directory.GetDirectories(Path.Combine("Assets", "Karts")))
            yield return Path.GetFileName(folder);
    }

    private static void Run(KartDynamics kart, ref KartBody body, KartControls controls, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            var forces = kart.ComputeForces(body, controls, Surface.Asphalt, Dt);
            KartIntegrator.Step(ref body, forces, kart.Mass, kart.YawInertia, Dt);
        }
    }
}
