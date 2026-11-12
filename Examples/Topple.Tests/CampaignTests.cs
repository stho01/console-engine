using NUnit.Framework;

namespace Topple.Tests;

public class CampaignTests
{
    [Test]
    public void CompleteLevel_BeforeTheLast_BanksScoreAndMovesOn()
    {
        var campaign = new Campaign(levelCount: 3);

        campaign.CompleteLevel(1200);

        Assert.Multiple(() =>
        {
            Assert.That(campaign.LevelIndex, Is.EqualTo(1));
            Assert.That(campaign.TotalScore, Is.EqualTo(1200));
        });
    }

    [Test]
    public void CompleteLevel_TheLast_StartsOverWithNoScore()
    {
        var campaign = new Campaign(levelCount: 2);
        campaign.CompleteLevel(500);

        campaign.CompleteLevel(700);

        Assert.Multiple(() =>
        {
            Assert.That(campaign.LevelIndex, Is.Zero);
            Assert.That(campaign.TotalScore, Is.Zero);
        });
    }
}
