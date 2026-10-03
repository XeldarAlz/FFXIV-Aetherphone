using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NotificationSectionsTests
{
    private static readonly DateTime Today = new(2026, 10, 3, 14, 30, 0, DateTimeKind.Local);

    private static PhoneNotification Make(string appId, string? groupKey = null) =>
        new(appId, "Title", "Body", Today, default, groupKey);

    [Fact]
    public void SameDayIsToday()
    {
        Assert.Equal(NotificationSection.Today,
            NotificationSections.Of(new DateTime(2026, 10, 3, 0, 5, 0, DateTimeKind.Local), Today));
    }

    [Fact]
    public void FutureStampFallsIntoToday()
    {
        Assert.Equal(NotificationSection.Today, NotificationSections.Of(Today.AddHours(12), Today));
    }

    [Fact]
    public void PreviousCalendarDayIsYesterday()
    {
        Assert.Equal(NotificationSection.Yesterday,
            NotificationSections.Of(new DateTime(2026, 10, 2, 23, 59, 0, DateTimeKind.Local), Today));
    }

    [Fact]
    public void TwoDaysBackIsEarlier()
    {
        Assert.Equal(NotificationSection.Earlier,
            NotificationSections.Of(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local), Today));
    }

    [Fact]
    public void TallyCountsPerAppNewestFirst()
    {
        var recent = new List<PhoneNotification>
        {
            Make("chirper"),
            Make("message"),
            Make("chirper"),
            Make("timers"),
        };
        var output = new List<NotificationAppCount>();
        NotificationSections.Tally(recent, output);
        Assert.Equal(3, output.Count);
        Assert.Equal(new NotificationAppCount("timers", 1), output[0]);
        Assert.Equal(new NotificationAppCount("chirper", 2), output[1]);
        Assert.Equal(new NotificationAppCount("message", 1), output[2]);
    }

    [Fact]
    public void TallyClearsPreviousOutput()
    {
        var output = new List<NotificationAppCount> { new("stale", 9) };
        NotificationSections.Tally(new List<PhoneNotification>(), output);
        Assert.Empty(output);
    }

    [Fact]
    public void FilterMatchesOnlyTheChosenApp()
    {
        var groups = new NotificationGroups();
        groups.Rebuild(new List<PhoneNotification> { Make("chirper", "post:1"), Make("message", "thread:2") });
        var chirper = groups.Groups[1];
        Assert.True(NotificationSections.Matches(chirper, null));
        Assert.True(NotificationSections.Matches(chirper, "chirper"));
        Assert.False(NotificationSections.Matches(chirper, "message"));
    }

    [Fact]
    public void MuteExpiresAtItsDeadline()
    {
        var setting = new AppNotificationSetting { MutedUntilUnix = 1000 };
        Assert.True(NotificationMutes.IsMuted(setting, 999));
        Assert.False(NotificationMutes.IsMuted(setting, 1000));
        Assert.False(NotificationMutes.IsMuted(new AppNotificationSetting(), 0));
    }

    [Fact]
    public void UnknownAppIsNeverMuted()
    {
        var settings = new Dictionary<string, AppNotificationSetting>
        {
            ["chirper"] = new() { MutedUntilUnix = 5000 },
        };
        Assert.True(NotificationMutes.IsMuted(settings, "chirper", 10));
        Assert.False(NotificationMutes.IsMuted(settings, "message", 10));
    }

    [Fact]
    public void HourMuteLastsSixtyMinutes()
    {
        Assert.Equal(1000 + 3600, NotificationMutes.ForAnHour(1000));
    }

    [Fact]
    public void TodayMuteEndsAtLocalMidnight()
    {
        var offset = TimeSpan.FromHours(2);
        var now = new DateTimeOffset(2026, 10, 3, 21, 15, 0, offset);
        var expected = new DateTimeOffset(2026, 10, 4, 0, 0, 0, offset).ToUnixTimeSeconds();
        Assert.Equal(expected, NotificationMutes.UntilTomorrow(now));
    }
}
