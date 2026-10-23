using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Notifications;
using Xunit;
using static Aetherphone.Tests.CampaignFixtures;

namespace Aetherphone.Tests;

public sealed class CampaignStoreTests
{
    private readonly Configuration configuration = new();
    private readonly List<PhoneNotification> raised = new();
    private int saves;

    private CampaignLedger NewLedger() =>
        new(CampaignDefinition.LoveIsland, configuration, () => saves++);

    [Fact]
    public void TheEarnedTransitionNotifiesExactlyOnce()
    {
        var ledger = NewLedger();
        ledger.ApplyCampaign(Campaign(), raised);
        raised.Clear();

        ledger.ApplyMe(new CampaignMeDto(false, 0L, 0, true), raised);
        Assert.Empty(raised);

        ledger.ApplyMe(new CampaignMeDto(true, Start + Hour, 1, true), raised);
        ledger.ApplyMe(new CampaignMeDto(true, Start + Hour, 2, true), raised);
        ledger.ApplyMe(new CampaignMeDto(true, Start + Hour, 2, true), raised);

        var award = Assert.Single(raised);
        Assert.Equal("loveisland", award.AppId);
        Assert.Equal(CampaignLauncher.PerksKey, award.GroupKey);
        Assert.True(configuration.LoveIslandEarned);
    }

    [Fact]
    public void AnEarnedAwardDoesNotNotifyAgainAfterARestart()
    {
        NewLedger().ApplyMe(new CampaignMeDto(true, Start, 1, true), raised);
        raised.Clear();

        NewLedger().ApplyMe(new CampaignMeDto(true, Start, 1, true), raised);

        Assert.Empty(raised);
    }

    [Fact]
    public void RetiringHidesTheApp()
    {
        var ledger = NewLedger();
        Assert.True(ledger.IsAvailable(true, Retire + 10L));
        Assert.False(ledger.IsAvailable(false, Start));

        ledger.ApplyCampaign(Campaign(), raised);

        Assert.True(ledger.IsAvailable(true, Retire - 1L));
        Assert.False(ledger.IsAvailable(true, Retire));
        Assert.False(ledger.IsAvailable(false, Start));
        Assert.True(ledger.IsRetired(Retire));
    }

    [Fact]
    public void TheFirstPostsPageSeedsTheMarksWithoutBanners()
    {
        var ledger = NewLedger();
        ledger.ApplyPosts(Page(Post("a", Start - 2L * Hour), Post("b", Start - Hour)), true, raised);

        Assert.Empty(raised);
        Assert.Equal(0, ledger.UnreadCount);
        Assert.Equal("b", ledger.Posts[0].Id);
    }

    [Fact]
    public void ANewAnnouncementRaisesOneBannerAndCountsAsUnread()
    {
        var ledger = NewLedger();
        ledger.ApplyPosts(Page(Post("a", Start - Hour)), true, raised);

        ledger.ApplyPosts(Page(Post("b", Start), Post("a", Start - Hour)), true, raised);
        ledger.ApplyPosts(Page(Post("b", Start), Post("a", Start - Hour)), true, raised);

        var banner = Assert.Single(raised);
        Assert.Equal("post:b", banner.GroupKey);
        Assert.Equal(1, ledger.UnreadCount);
        Assert.True(ledger.IsUnread(ledger.Posts[0]));

        ledger.MarkPostsSeen();

        Assert.Equal(0, ledger.UnreadCount);
    }

    [Fact]
    public void ANewExperienceRaisesOneBanner()
    {
        var ledger = NewLedger();
        ledger.ApplyCampaign(Campaign(), raised);
        Assert.Empty(raised);

        var experiences = new[]
        {
            Experience("doors", Start, Start + Hour),
            Experience("afterglow", Start + 24L * Hour, Start + 27L * Hour),
        };
        ledger.ApplyCampaign(Campaign(experiences), raised);
        ledger.ApplyCampaign(Campaign(experiences), raised);

        var banner = Assert.Single(raised);
        Assert.Equal("experience:afterglow", banner.GroupKey);
    }

    [Fact]
    public void AReminderFiresOnceInsideTheLeadAndIsThenForgotten()
    {
        var ledger = NewLedger();
        ledger.ApplyCampaign(Campaign(), raised);
        ledger.SetReminder("opening", true);
        Assert.True(ledger.HasReminder("opening"));

        ledger.FireDueReminders(Start + 2L * Hour - 31L * 60L, raised);
        Assert.Empty(raised);

        ledger.FireDueReminders(Start + 2L * Hour - 29L * 60L, raised);
        ledger.FireDueReminders(Start + 2L * Hour - 28L * 60L, raised);

        var reminder = Assert.Single(raised);
        Assert.Equal("experience:opening", reminder.GroupKey);
        Assert.False(ledger.HasReminder("opening"));
    }

    [Fact]
    public void TheTileGlowsWithAnAuraUntilOpenedAfterAnUpdate()
    {
        var ledger = NewLedger();
        Assert.Equal(CampaignLedger.TileEffectNone, ledger.TileEffectLevel(Start));

        ledger.ApplyCampaign(Campaign(updatedAt: Start - Hour), raised);
        Assert.Equal(CampaignLedger.TileEffectAura, ledger.TileEffectLevel(Start - 30L * Hour));

        ledger.MarkOpened();
        Assert.Equal(CampaignLedger.TileEffectGlow, ledger.TileEffectLevel(Start - 30L * Hour));

        ledger.ApplyCampaign(Campaign(updatedAt: Start), raised);
        Assert.Equal(CampaignLedger.TileEffectAura, ledger.TileEffectLevel(Start));

        ledger.MarkOpened();
        Assert.Equal(CampaignLedger.TileEffectNone, ledger.TileEffectLevel(End));
    }

    [Fact]
    public void LaunchKeysRoundTripThroughTheLauncher()
    {
        var launcher = new CampaignLauncher();
        launcher.Request("post:abc");
        Assert.True(launcher.TryConsume(out var post));
        Assert.Equal(new CampaignLaunch(CampaignLaunchKind.Post, "abc"), post);
        Assert.False(launcher.TryConsume(out _));

        Assert.Equal(CampaignLaunchKind.Experience, CampaignLauncher.Parse("experience:x").Kind);
        Assert.Equal(CampaignLaunchKind.Perks, CampaignLauncher.Parse("perks").Kind);
        Assert.Equal(CampaignLaunchKind.None, CampaignLauncher.Parse("unknown").Kind);
    }
}
