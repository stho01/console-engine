using Microsoft.Xna.Framework;
using NUnit.Framework;
using Starguard.Components;

namespace Starguard.Tests;

public class CollideTests
{
    private static readonly Hitbox Box = new(5, 2);
    private static readonly Vector2 BoxPosition = new(10, 10);

    [Test]
    public void ShotHits_ShotPassedThroughBoxThisFrame_ReturnsTrue() =>
        Assert.That(Collide.ShotHits(new Vector2(12, 8), previousY: 13, BoxPosition, Box), Is.True);

    [Test]
    public void ShotHits_ShotEndsInsideBox_ReturnsTrue() =>
        Assert.That(Collide.ShotHits(new Vector2(14.9f, 11.5f), previousY: 12.5f, BoxPosition, Box), Is.True);

    [Test]
    public void ShotHits_ShotInColumnRightOfBox_ReturnsFalse() =>
        Assert.That(Collide.ShotHits(new Vector2(15, 10), previousY: 11, BoxPosition, Box), Is.False);

    [Test]
    public void ShotHits_ShotStillBelowBox_ReturnsFalse() =>
        Assert.That(Collide.ShotHits(new Vector2(12, 12), previousY: 13, BoxPosition, Box), Is.False);

    [Test]
    public void Overlaps_BoxesShareACell_ReturnsTrue() =>
        Assert.That(Collide.Overlaps(new Vector2(14, 11), new Hitbox(1, 1), BoxPosition, Box), Is.True);

    [Test]
    public void Overlaps_BoxesOnlyTouch_ReturnsFalse() =>
        Assert.That(Collide.Overlaps(new Vector2(15, 10), new Hitbox(1, 1), BoxPosition, Box), Is.False);
}
