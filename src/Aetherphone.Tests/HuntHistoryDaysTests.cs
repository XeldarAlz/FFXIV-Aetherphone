using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntHistoryDaysTests
{
    private static readonly Func<DateTimeOffset, DateTime> Utc = static moment => moment.UtcDateTime;

    private static HuntLogEntryDto Entry(long id, DateTimeOffset? spawned, DateTimeOffset? killed) => new()
    {
        Id = id,
        MobId = "mob",
        WorldId = "cerberus",
        SpawnedAt = spawned,
        KilledAt = killed,
    };

    [Fact]
    public void GroupsNewestFirstByDay()
    {
        var dayOne = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var dayTwo = dayOne.AddDays(1);
        var entries = new[]
        {
            Entry(1, null, dayOne),
            Entry(2, null, dayTwo.AddHours(2)),
            Entry(3, null, dayTwo),
            Entry(4, null, null),
            Entry(5, dayOne.AddHours(5), null),
        };
        var sorted = new List<HuntLogEntryDto>();
        var days = new List<HuntHistoryDay>();

        HuntHistoryDays.Group(entries, Utc, sorted, days);

        Assert.Equal(new long[] { 2, 3, 5, 1 }, sorted.ConvertAll(entry => entry.Id).ToArray());
        Assert.Equal(2, days.Count);
        Assert.Equal(new HuntHistoryDay(dayTwo.UtcDateTime.Date, 0, 2), days[0]);
        Assert.Equal(new HuntHistoryDay(dayOne.UtcDateTime.Date, 2, 2), days[1]);
    }

    [Fact]
    public void LifetimeNeedsBothStamps()
    {
        var spawned = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMinutes(7), HuntHistoryDays.Lifetime(Entry(1, spawned, spawned.AddMinutes(7))));
        Assert.Null(HuntHistoryDays.Lifetime(Entry(2, spawned, null)));
        Assert.Null(HuntHistoryDays.Lifetime(Entry(3, spawned, spawned.AddMinutes(-1))));
    }

    [Fact]
    public void EmptyInputBuildsNoDays()
    {
        var sorted = new List<HuntLogEntryDto>();
        var days = new List<HuntHistoryDay>();

        HuntHistoryDays.Group(Array.Empty<HuntLogEntryDto>(), Utc, sorted, days);

        Assert.Empty(sorted);
        Assert.Empty(days);
    }
}
