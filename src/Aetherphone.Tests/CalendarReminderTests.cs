using Aetherphone.Core.Calendar;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CalendarReminderTests
{
    private static readonly DateTime When = new(2026, 9, 26, 18, 30, 0);

    [Fact]
    public void AtEventTimeFiresAtTheEventItself()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = CalendarReminder.AtEventTime };

        Assert.True(CalendarReminder.TryFireTime(calendarEvent, out var fireTime));
        Assert.Equal(When, fireTime);
    }

    [Fact]
    public void LeadTimeMovesTheFireTimeEarlier()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = 30 };

        Assert.True(CalendarReminder.TryFireTime(calendarEvent, out var fireTime));
        Assert.Equal(When.AddMinutes(-30), fireTime);
    }

    [Fact]
    public void NoneNeverFires()
    {
        var calendarEvent = new CalendarCustomEvent { When = When, ReminderMinutesBefore = CalendarReminder.None };

        Assert.False(CalendarReminder.TryFireTime(calendarEvent, out _));
    }

    [Fact]
    public void UnknownLeadValuesFallBackToTheEventTimeOption()
    {
        var index = CalendarReminder.LeadIndexOf(45);

        Assert.Equal(CalendarReminder.AtEventTime, CalendarReminder.LeadOptionsMinutes[index]);
    }

    [Fact]
    public void EveryLeadOptionRoundTripsThroughItsIndex()
    {
        var options = CalendarReminder.LeadOptionsMinutes;
        for (var index = 0; index < options.Length; index++)
        {
            Assert.Equal(index, CalendarReminder.LeadIndexOf(options[index]));
        }
    }
}
