using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Kartster.Tests;

public class KartModelTests
{
    // Kartster draws parts starting with these in the player colour (lacquer, glow) or has no word on them.
    private static readonly string[] PlayerColorWords = ["lacquer", "glow", "body", "rim_", "upholstery"];

    private static readonly Vector3 Player = new(-1f);

    [Test]
    public void Load_ShippedKart_SkipsMarkerParts()
    {
        var mesh = KartModel.Load(Path.Combine("Assets", "Karts", "olabil"), Vector3.One);

        Assert.That(mesh.TriangleCount, Is.GreaterThan(1000));
        Assert.That(mesh.TriangleColors, Has.Length.EqualTo(mesh.TriangleCount));
    }

    [TestCaseSource(nameof(ShippedKarts))]
    public void Load_EveryShippedKart_HasTrianglesAndAColourForEveryPart(string folder)
    {
        var mesh = KartModel.Load(folder, Player);

        Assert.That(mesh.TriangleCount, Is.GreaterThan(0));
        foreach (var file in Directory.GetFiles(folder, "*.obj"))
        {
            var part = Path.GetFileNameWithoutExtension(file);
            if (!KartModel.IsDrawn(part))
                continue;

            var hasColorWord = KartModel.PartColor(part, Player) != Player;
            Assert.That(hasColorWord || StartsWithAny(part, PlayerColorWords), Is.True, $"no colour word: {part}");
        }
    }

    [TestCase("pose_lemon_back")]
    [TestCase("joint_knee_left")]
    [TestCase("gray_robot_arm_left")]
    [TestCase("glow_tube_left_on_boost")]
    [TestCase("muffler_left")]
    [TestCase("thrust_jet")]
    [TestCase("radar_pivot")]
    [TestCase("light_long_left")]
    public void IsDrawn_MarkerOrBoostOnlyPart_IsFalse(string part) =>
        Assert.That(KartModel.IsDrawn(part), Is.False);

    [Test]
    public void Recolor_MutedPart_FollowsThePlayerColour()
    {
        var from = KartModel.PartColor("muted_body", Player);
        var mesh = new ConsoleEngine.Rendering3D.Mesh([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2], [from]);

        var recolored = KartModel.Recolor(mesh, Player, Vector3.UnitX);

        Assert.That(recolored.TriangleColors[0], Is.EqualTo(KartModel.PartColor("muted_body", Vector3.UnitX)));
    }

    private static IEnumerable<string> ShippedKarts() => Directory.GetDirectories(Path.Combine("Assets", "Karts"));

    private static bool StartsWithAny(string part, string[] words) =>
        Array.Exists(words, word => part.StartsWith(word, StringComparison.OrdinalIgnoreCase));
}
