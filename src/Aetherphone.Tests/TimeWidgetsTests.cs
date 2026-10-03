using Aetherphone.Apps.Calendar.Widgets;
using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Core.Clock;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TimeWidgetsTests
{
    [Theory]
    [InlineData(0, "+0")]
    [InlineData(180, "+3")]
    [InlineData(-300, "-5")]
    [InlineData(330, "+5:30")]
    [InlineData(-570, "-9:30")]
    public void OffsetText_FormatsHoursAndMinutes(int offsetMinutes, string expected)
    {
        Assert.Equal(expected, ClockZones.OffsetText(offsetMinutes));
    }

    [Theory]
    [InlineData("", (int)ClockSlotKind.Local)]
    [InlineData("local", (int)ClockSlotKind.Local)]
    [InlineData("eorzea", (int)ClockSlotKind.Eorzea)]
    [InlineData("server", (int)ClockSlotKind.Server)]
    public void FromChoice_ResolvesTheBuiltInClocks(string value, int expected)
    {
        Assert.Equal((ClockSlotKind)expected, ClockZones.FromChoice(value, new List<WorldClockEntry>()).Kind);
    }

    [Fact]
    public void FillFaces_PutsLocalAndEorzeaFirstAndServerLast()
    {
        var slots = new ClockSlot[4];

        var count = ClockZones.FillFaces(slots, new List<WorldClockEntry>());

        Assert.Equal(3, count);
        Assert.Equal(ClockSlotKind.Local, slots[0].Kind);
        Assert.Equal(ClockSlotKind.Eorzea, slots[1].Kind);
        Assert.Equal(ClockSlotKind.Server, slots[2].Kind);
    }

    [Fact]
    public void SampleMask_StaysInsideTheMonth()
    {
        var mask = CalendarWidgetFeed.SampleMask(new DateTime(2026, 10, 31));

        Assert.Equal(1u << 31, mask);
    }

    [Fact]
    public void SampleMask_MarksTodayAndTheNextTwoDays()
    {
        var mask = CalendarWidgetFeed.SampleMask(new DateTime(2026, 10, 3));

        Assert.Equal((1u << 3) | (1u << 4) | (1u << 5), mask);
    }
}
