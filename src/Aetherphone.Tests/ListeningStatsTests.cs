using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ListeningStatsTests
{
    private static readonly DateOnly Saturday = new(2026, 10, 3);

    private static int Day(DateOnly date) => date.DayNumber;

    private static PlayRecord Played(string id, string author, string channel, int count, long seconds,
        params ListeningDay[] days)
    {
        return new PlayRecord
        {
            Song = new SongRecord { VideoId = id, Title = id, Author = author, ChannelId = channel },
            Count = count,
            ListenedSeconds = seconds,
            Days = new List<ListeningDay>(days),
        };
    }

    private static ListeningDay On(DateOnly date, int plays, int seconds) =>
        new() { Day = date.DayNumber, Plays = plays, Seconds = seconds };

    [Fact]
    public void The_week_starts_on_monday_and_ends_today()
    {
        var (first, last) = ListeningStats.Range(ReplayPeriod.Week, Saturday);

        Assert.Equal(Day(new DateOnly(2026, 9, 28)), first);
        Assert.Equal(Day(Saturday), last);
        Assert.Equal(Day(new DateOnly(2026, 10, 1)), ListeningStats.Range(ReplayPeriod.Month, Saturday).First);
        Assert.Equal(Day(new DateOnly(2026, 1, 1)), ListeningStats.Range(ReplayPeriod.Year, Saturday).First);
    }

    [Fact]
    public void Period_totals_only_count_days_inside_the_period()
    {
        var days = new List<ListeningDay>
        {
            On(new DateOnly(2026, 9, 27), 4, 900),
            On(new DateOnly(2026, 9, 29), 2, 400),
            On(Saturday, 1, 200),
        };

        var summary = ListeningStats.Build(new List<PlayRecord>(), days, 5000, ReplayPeriod.Week, Saturday, 5);

        Assert.Equal(600, summary.TotalSeconds);
        Assert.Equal(3, summary.TotalPlays);
        Assert.Equal(7, summary.Bars.Length);
        Assert.Equal(400, summary.Bars[1].Seconds);
        Assert.Equal(200, summary.Bars[5].Seconds);
        Assert.Equal(0, summary.Bars[6].Seconds);
        Assert.Equal(400, summary.PeakBarSeconds);
        Assert.Equal(ReplayBarUnit.Day, summary.Unit);
    }

    [Fact]
    public void All_time_uses_the_lifetime_totals()
    {
        var plays = new List<PlayRecord> { Played("a", "Artist", "c1", 9, 1800) };

        var summary = ListeningStats.Build(plays, new List<ListeningDay>(), 7200, ReplayPeriod.AllTime, Saturday,
            5);

        Assert.Equal(7200, summary.TotalSeconds);
        Assert.Equal(9, summary.TotalPlays);
        Assert.Equal(9, summary.TopSongs[0].Plays);
        Assert.Equal(ListeningStats.MonthsInYear, summary.Bars.Length);
        Assert.Equal(new DateOnly(2025, 11, 1), summary.Bars[0].Start);
        Assert.Equal(new DateOnly(2026, 10, 1), summary.Bars[^1].Start);
    }

    [Fact]
    public void Top_songs_rank_by_plays_inside_the_period()
    {
        var plays = new List<PlayRecord>
        {
            Played("old", "A", "c1", 50, 9000, On(new DateOnly(2026, 8, 1), 50, 9000)),
            Played("light", "B", "c2", 2, 300, On(Saturday, 2, 300)),
            Played("heavy", "C", "c3", 5, 800, On(new DateOnly(2026, 9, 29), 3, 500), On(Saturday, 2, 300)),
        };

        var summary = ListeningStats.Build(plays, new List<ListeningDay>(), 0, ReplayPeriod.Week, Saturday, 5);

        Assert.Equal(2, summary.TopSongs.Length);
        Assert.Equal("heavy", summary.TopSongs[0].Song.VideoId);
        Assert.Equal(5, summary.TopSongs[0].Plays);
        Assert.Equal("light", summary.TopSongs[1].Song.VideoId);
    }

    [Fact]
    public void Artists_group_by_channel_and_fall_back_to_the_author_name()
    {
        var plays = new List<PlayRecord>
        {
            Played("one", "Band - Topic", "channel", 1, 100, On(Saturday, 1, 100)),
            Played("two", "Band", "channel", 2, 100, On(Saturday, 2, 100)),
            Played("three", "Solo", string.Empty, 1, 50, On(Saturday, 1, 50)),
            Played("four", "solo ", string.Empty, 1, 50, On(Saturday, 1, 50)),
            Played("five", "Other", string.Empty, 1, 10, On(Saturday, 1, 10)),
        };

        var summary = ListeningStats.Build(plays, new List<ListeningDay>(), 0, ReplayPeriod.Week, Saturday, 2);

        Assert.Equal(2, summary.TopArtists.Length);
        Assert.Equal("channel", summary.TopArtists[0].ChannelId);
        Assert.Equal(3, summary.TopArtists[0].Plays);
        Assert.Equal("Solo", summary.TopArtists[1].Name);
        Assert.Equal(2, summary.TopArtists[1].Plays);
    }

    [Fact]
    public void A_year_is_charted_as_twelve_months()
    {
        var days = new List<ListeningDay>
        {
            On(new DateOnly(2026, 2, 3), 1, 60),
            On(new DateOnly(2026, 2, 20), 1, 120),
            On(new DateOnly(2025, 12, 31), 1, 999),
        };

        var bars = ListeningStats.Bars(days, ReplayPeriod.Year, Saturday);

        Assert.Equal(ListeningStats.MonthsInYear, bars.Length);
        Assert.Equal(180, bars[1].Seconds);
        Assert.Equal(0, bars[0].Seconds);
    }

    [Fact]
    public void Adding_merges_the_same_day_and_keeps_days_in_order()
    {
        var days = new List<ListeningDay>();

        ListeningStats.Add(days, 10, 1, 0);
        ListeningStats.Add(days, 12, 0, 30);
        ListeningStats.Add(days, 10, 0, 45);
        ListeningStats.Add(days, 11, 1, 5);

        Assert.Equal(3, days.Count);
        Assert.Equal(10, days[0].Day);
        Assert.Equal(1, days[0].Plays);
        Assert.Equal(45, days[0].Seconds);
        Assert.Equal(11, days[1].Day);
        Assert.Equal(12, days[2].Day);

        ListeningStats.Prune(days, 11);

        Assert.Equal(2, days.Count);
        Assert.Equal(11, days[0].Day);
    }

    [Fact]
    public void The_listening_clock_counts_only_continuous_playback()
    {
        var clock = new ListeningClock();

        clock.Observe(10f, true);
        clock.Observe(10.5f, true);
        clock.Observe(11f, true);
        clock.Observe(95f, true);
        clock.Observe(95.5f, true);
        clock.Observe(96f, false);
        clock.Observe(120f, true);
        clock.Observe(120.25f, true);
        clock.Observe(20f, true);

        Assert.Equal(1.75f, clock.Seconds, 3);
        Assert.Equal(1, clock.Take());
        Assert.Equal(0f, clock.Seconds);
    }
}
