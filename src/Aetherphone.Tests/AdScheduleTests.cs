using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.YellowPages;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AdScheduleTests
{
    private const long SecondsPerWeek = 7L * 86400L;
    private const int Duration = 180;

    private static readonly long FirstFriday = new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static AdScheduleSlot Biweekly() => new((int)DayOfWeek.Friday, 20 * 60, Duration, 2, FirstFriday);

    private static AdDto PlaceAd(params AdScheduleSlot[] schedule)
    {
        return new AdDto("ad", "owner", "Owner", "owner", string.Empty, AdArchetypes.Place, AdCategories.VenueNight,
            "Night", "Body", Array.Empty<string>(), 0, 0, 0, 0, 0, 0f, 0f, 0, 0, string.Empty, schedule, 0L, 0, 0L,
            string.Empty, string.Empty, string.Empty, string.Empty, false, null, Array.Empty<string>(), 0, false,
            AdStatuses.Live, 0L, 0L, long.MaxValue);
    }

    [Fact]
    public void BiweeklySlotIsOpenOnItsFirstNight()
    {
        var state = AdText.OpenState(PlaceAd(Biweekly()), FirstFriday + 3600);

        Assert.True(state.IsOpen);
        Assert.Equal(FirstFriday + Duration * 60L, state.ClosesAtUnix);
    }

    [Fact]
    public void BiweeklySlotSkipsTheWeekBetween()
    {
        var state = AdText.OpenState(PlaceAd(Biweekly()), FirstFriday + SecondsPerWeek + 3600);

        Assert.False(state.IsOpen);
        Assert.Equal(FirstFriday + 2 * SecondsPerWeek, state.NextOpeningUnix);
    }

    [Fact]
    public void BiweeklySlotWaitsForItsFirstNight()
    {
        var state = AdText.OpenState(PlaceAd(Biweekly()), FirstFriday - 5 * 86400L);

        Assert.False(state.IsOpen);
        Assert.Equal(FirstFriday, state.NextOpeningUnix);
    }

    [Fact]
    public void WeeklySlotStillOpensEveryWeek()
    {
        var weekly = new AdScheduleSlot((int)DayOfWeek.Friday, 20 * 60, Duration);

        var state = AdText.OpenState(PlaceAd(weekly), FirstFriday + SecondsPerWeek + 3600);

        Assert.True(state.IsOpen);
        Assert.Equal(FirstFriday + SecondsPerWeek + Duration * 60L, state.ClosesAtUnix);
    }

    [Fact]
    public void UpcomingStartStaysOnTheCurrentNightUntilItCloses()
    {
        var closingSoon = FirstFriday + 2 * SecondsPerWeek + Duration * 60L - 60;

        Assert.Equal(FirstFriday + 2 * SecondsPerWeek, AdText.UpcomingStartUnix(Biweekly(), closingSoon));
        Assert.Equal(FirstFriday + 4 * SecondsPerWeek, AdText.UpcomingStartUnix(Biweekly(), closingSoon + 120));
    }

    [Fact]
    public void FirstWeekOffsetRecoversWhichWeekComesFirst()
    {
        var nowUnix = FirstFriday - 3 * 86400L;
        var startsThisWeek = Biweekly();
        var startsNextWeek = startsThisWeek with { FirstUnix = FirstFriday + SecondsPerWeek };
        var everyThirdWeek = startsThisWeek with { EveryWeeks = 3, FirstUnix = FirstFriday + 2 * SecondsPerWeek };

        Assert.Equal(0, AdText.FirstWeekOffset(startsThisWeek, nowUnix));
        Assert.Equal(1, AdText.FirstWeekOffset(startsNextWeek, nowUnix));
        Assert.Equal(2, AdText.FirstWeekOffset(everyThirdWeek, nowUnix));
    }

    [Fact]
    public void RepeatingSlotDerivesItsDayAndMinuteFromTheFirstNight()
    {
        var nowLocal = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local);

        var slot = AdText.ToRepeatingSlot((int)DayOfWeek.Friday, 20 * 60, Duration, 2, 1, nowLocal);

        var first = DateTimeOffset.FromUnixTimeSeconds(slot.FirstUnix).UtcDateTime;
        Assert.Equal(2, slot.EveryWeeks);
        Assert.Equal((int)first.DayOfWeek, slot.Day);
        Assert.Equal(first.Hour * 60 + first.Minute, slot.StartMinute);
        Assert.Equal(new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Local), first.ToLocalTime());
    }

    [Fact]
    public void FirstLocalStartKeepsTonightWhileTheDoorsAreStillOpen()
    {
        var friday = new DateTime(2026, 10, 2, 21, 0, 0);

        Assert.Equal(friday.Date.AddMinutes(20 * 60), AdText.FirstLocalStart((int)DayOfWeek.Friday, 20 * 60, Duration, friday));
        Assert.Equal(friday.Date.AddDays(7).AddMinutes(20 * 60),
            AdText.FirstLocalStart((int)DayOfWeek.Friday, 20 * 60, Duration, friday.AddHours(2)));
    }
}
