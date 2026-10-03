using System.Collections.Frozen;
using System.Numerics;
using Aetherphone.Apps.Calendar;
using Aetherphone.Core.Calendar;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalendarEventMergerTests
{
    private static readonly DateTime Day = new(2026, 9, 26, 18, 30, 0);
    private static readonly Vector4 Accent = new(1f, 0f, 0f, 1f);
    private static readonly Guid RaidsId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MissingId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void HiddenGameEventsDropOutOfTheRequestedSurfaceOnly()
    {
        var remote = Remote("Moogle Treasure Trove");

        var visible = CalendarEventMerger.Merge(remote, Array.Empty<CalendarCustomEvent>(),
            Array.Empty<CalendarEventGroup>(), false, CalendarSurface.Widget, Accent);

        Assert.Empty(visible);
    }

    [Fact]
    public void GroupHiddenOnTheWidgetStillShowsInTheApp()
    {
        var groups = new[] { new CalendarEventGroup { Id = RaidsId, Name = "Raids", ShowInWidget = false } };
        var custom = new[] { Custom("Savage night", RaidsId) };

        var app = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.App, Accent);
        var widget = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.Widget, Accent);

        Assert.Single(app[Day.Date.Ticks]);
        Assert.Empty(widget);
    }

    [Fact]
    public void GroupHiddenInTheAppStillShowsOnTheWidget()
    {
        var groups = new[] { new CalendarEventGroup { Id = RaidsId, Name = "Raids", ShowInApp = false } };
        var custom = new[] { Custom("Savage night", RaidsId) };

        var app = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.App, Accent);
        var widget = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.Widget, Accent);

        Assert.Empty(app);
        Assert.Single(widget[Day.Date.Ticks]);
    }

    [Fact]
    public void UngroupedAndOrphanedEventsAlwaysShow()
    {
        var groups = new[] { new CalendarEventGroup { Id = RaidsId, Name = "Raids", ShowInApp = false } };
        var custom = new[] { Custom("Ungrouped", Guid.Empty), Custom("Orphan", MissingId) };

        var app = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.App, Accent);

        var dayEvents = app[Day.Date.Ticks];
        Assert.Equal(2, dayEvents.Length);
        Assert.Equal(string.Empty, dayEvents[0].GroupName);
        Assert.Equal(string.Empty, dayEvents[1].GroupName);
    }

    [Fact]
    public void VisibleGroupedEventsCarryTheirGroupName()
    {
        var groups = new[] { new CalendarEventGroup { Id = RaidsId, Name = "Raids" } };
        var custom = new[] { Custom("Savage night", RaidsId) };

        var app = CalendarEventMerger.Merge(Remote(), custom, groups, true, CalendarSurface.App, Accent);

        var parsed = Assert.Single(app[Day.Date.Ticks]);
        Assert.True(parsed.IsCustom);
        Assert.Equal("Raids", parsed.GroupName);
    }

    [Fact]
    public void HiddenGameEventsDoNotLeakBackWhenCustomEventsExist()
    {
        var custom = new[] { Custom("Mine", Guid.Empty) };

        var app = CalendarEventMerger.Merge(Remote("Moogle Treasure Trove"), custom,
            Array.Empty<CalendarEventGroup>(), false, CalendarSurface.App, Accent);

        var parsed = Assert.Single(app[Day.Date.Ticks]);
        Assert.Equal("Mine", parsed.Name);
    }

    private static CalendarCustomEvent Custom(string title, Guid groupId) =>
        new() { Title = title, When = Day, GroupId = groupId };

    private static FrozenDictionary<long, ParsedEvent[]> Remote(params string[] names)
    {
        if (names.Length == 0)
        {
            return FrozenDictionary<long, ParsedEvent[]>.Empty;
        }

        var parsed = new ParsedEvent[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            parsed[index] = new ParsedEvent { Name = names[index], Begin = Day, End = Day, Url = string.Empty };
        }

        return new Dictionary<long, ParsedEvent[]> { [Day.Date.Ticks] = parsed }.ToFrozenDictionary();
    }
}
