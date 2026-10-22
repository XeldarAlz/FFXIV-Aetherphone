using Aetherphone.Core.Campaigns;
using Xunit;
using static Aetherphone.Tests.CampaignFixtures;

namespace Aetherphone.Tests;

public sealed class CampaignCountdownTests
{
    [Fact]
    public void BeforeTheWindowCountsDownToTheDoors()
    {
        var moment = CampaignCountdown.Evaluate(Campaign(), Start - 90L);

        Assert.Equal(CampaignPhase.Before, moment.Phase);
        Assert.Equal(90L, moment.SecondsToStart);
        Assert.False(moment.WindowOpen);
        Assert.Equal(0, moment.NextIndex);
        Assert.Equal(-1, moment.CurrentIndex);
    }

    [Fact]
    public void DuringAnExperienceItIsLive()
    {
        var moment = CampaignCountdown.Evaluate(Campaign(), Start + 2L * Hour + 60L);

        Assert.Equal(CampaignPhase.Live, moment.Phase);
        Assert.Equal(1, moment.CurrentIndex);
        Assert.Equal(2, moment.NextIndex);
        Assert.Equal(1, moment.NextStreamedIndex);
        Assert.True(moment.WindowOpen);
    }

    [Fact]
    public void InAGapBetweenExperiencesItIsBetweenSets()
    {
        var moment = CampaignCountdown.Evaluate(Campaign(), Start + Hour + 600L);

        Assert.Equal(CampaignPhase.Between, moment.Phase);
        Assert.Equal(-1, moment.CurrentIndex);
        Assert.Equal(1, moment.NextIndex);
        Assert.True(moment.WindowOpen);
    }

    [Fact]
    public void AfterTheWindowItHasEnded()
    {
        var moment = CampaignCountdown.Evaluate(Campaign(), End);

        Assert.Equal(CampaignPhase.Ended, moment.Phase);
        Assert.False(moment.WindowOpen);
        Assert.Equal(-1, moment.NextIndex);
    }

    [Fact]
    public void ExperienceStatesFollowTheirOwnTimes()
    {
        var experience = Experience("x", Start, Start + Hour);

        Assert.Equal(ExperienceState.Upcoming, CampaignCountdown.StateOf(experience, Start - 1L));
        Assert.Equal(ExperienceState.Live, CampaignCountdown.StateOf(experience, Start));
        Assert.Equal(ExperienceState.Ended, CampaignCountdown.StateOf(experience, Start + Hour));
    }

    [Fact]
    public void CountdownTextNarrowsAsTheDoorsApproach()
    {
        Assert.Equal("2d 3h 04m", CampaignCountdown.Text(2L * 86400L + 3L * Hour + 4L * 60L + 5L));
        Assert.Equal("5h 06m 07s", CampaignCountdown.Text(5L * Hour + 6L * 60L + 7L));
        Assert.Equal("9:05", CampaignCountdown.Text(9L * 60L + 5L));
        Assert.Equal("0:00", CampaignCountdown.Text(-30L));
    }
}
