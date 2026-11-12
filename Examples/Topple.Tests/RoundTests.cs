using NUnit.Framework;
using Topple.Components;

namespace Topple.Tests;

public class RoundTests
{
    [Test]
    public void TryUseShot_WithAmmoLeft_TakesOne()
    {
        var round = new Round(stones: 2, bombs: 1);

        var used = round.TryUseShot(ShotKind.Bomb);

        Assert.Multiple(() =>
        {
            Assert.That(used, Is.True);
            Assert.That(round.Bombs, Is.Zero);
            Assert.That(round.Stones, Is.EqualTo(2));
        });
    }

    [Test]
    public void TryUseShot_NoneOfThatKindLeft_ReturnsFalse()
    {
        var round = new Round(stones: 1, bombs: 0);

        Assert.That(round.TryUseShot(ShotKind.Bomb), Is.False);
    }

    [Test]
    public void Update_NoCrystalsLeft_WinsWithBonusForShotsLeft()
    {
        var round = new Round(stones: 2, bombs: 1);
        round.AddWreck(Material.Crystal);

        round.Update(.1f, crystals: 0, shotsInFlight: 0, awakeBlocks: 3);

        Assert.Multiple(() =>
        {
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Won));
            Assert.That(round.Bonus, Is.EqualTo(3 * Round.ShotBonus));
            Assert.That(round.FinalScore, Is.EqualTo(Materials.Get(Material.Crystal).Score + 3 * Round.ShotBonus));
        });
    }

    [Test]
    public void Update_OutOfShotsButShotStillFlying_KeepsPlaying()
    {
        var round = UsedUpRound();

        round.Update(2f, crystals: 1, shotsInFlight: 1, awakeBlocks: 0);

        Assert.That(round.Phase, Is.EqualTo(RoundPhase.Playing));
    }

    [Test]
    public void Update_RightAfterLastShot_KeepsPlaying()
    {
        var round = UsedUpRound();

        round.Update(Round.MinWaitSeconds / 2f, crystals: 1, shotsInFlight: 0, awakeBlocks: 0);

        Assert.That(round.Phase, Is.EqualTo(RoundPhase.Playing));
    }

    [Test]
    public void Update_OutOfShotsAndEverythingAsleep_Loses()
    {
        var round = UsedUpRound();

        round.Update(Round.MinWaitSeconds, crystals: 1, shotsInFlight: 0, awakeBlocks: 0);

        Assert.That(round.Phase, Is.EqualTo(RoundPhase.Lost));
    }

    [Test]
    public void Update_OutOfShotsAndBlocksStillMoving_LosesAfterGivingUp()
    {
        var round = UsedUpRound();

        round.Update(Round.GiveUpSeconds - .1f, crystals: 1, shotsInFlight: 0, awakeBlocks: 2);
        var before = round.Phase;
        round.Update(.2f, crystals: 1, shotsInFlight: 0, awakeBlocks: 2);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo(RoundPhase.Playing));
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Lost));
        });
    }

    [Test]
    public void Update_AfterWin_StaysWonAndStopsShots()
    {
        var round = new Round(stones: 1, bombs: 0);
        round.Update(.1f, crystals: 0, shotsInFlight: 0, awakeBlocks: 0);

        round.Update(10f, crystals: 0, shotsInFlight: 0, awakeBlocks: 0);

        Assert.Multiple(() =>
        {
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Won));
            Assert.That(round.TryUseShot(ShotKind.Stone), Is.False);
        });
    }

    private static Round UsedUpRound()
    {
        var round = new Round(stones: 1, bombs: 0);
        round.TryUseShot(ShotKind.Stone);
        return round;
    }
}
