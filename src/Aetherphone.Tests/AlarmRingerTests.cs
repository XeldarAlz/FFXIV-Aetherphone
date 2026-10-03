using Aetherphone.Core.Clock;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AlarmRingerTests
{
    private static readonly DateTime Start = new(2026, 10, 3, 7, 0, 0, DateTimeKind.Utc);

    private int starts;
    private int stops;
    private int presented;
    private AlarmRingKind lastKind;

    private AlarmRinger Create()
    {
        var ringer = new AlarmRinger(kind =>
        {
            starts++;
            lastKind = kind;
        }, () => stops++);
        ringer.Presented += () => presented++;
        return ringer;
    }

    [Fact]
    public void RingStartsTheToneAndPresentsThePhone()
    {
        var ringer = Create();

        ringer.Ring(AlarmRingKind.Timer, string.Empty, Start);

        Assert.True(ringer.IsRinging);
        Assert.Equal(1, starts);
        Assert.Equal(1, presented);
        Assert.Equal(AlarmRingKind.Timer, lastKind);
        Assert.False(ringer.CanSnooze);
    }

    [Fact]
    public void StopSilencesOnce()
    {
        var ringer = Create();
        ringer.Ring(AlarmRingKind.Alarm, "Raid", Start);

        ringer.Stop();
        ringer.Stop();

        Assert.False(ringer.IsRinging);
        Assert.Equal(1, stops);
    }

    [Fact]
    public void SnoozeRingsAgainWithTheSameLabelAfterNineMinutes()
    {
        var ringer = Create();
        ringer.Ring(AlarmRingKind.Alarm, "Raid", Start);

        ringer.Snooze(Start);
        ringer.Tick(Start + AlarmRinger.SnoozeLength - TimeSpan.FromSeconds(1));

        Assert.False(ringer.IsRinging);
        Assert.True(ringer.IsSnoozed);

        ringer.Tick(Start + AlarmRinger.SnoozeLength);

        Assert.True(ringer.IsRinging);
        Assert.False(ringer.IsSnoozed);
        Assert.Equal("Raid", ringer.Label);
        Assert.Equal(2, starts);
    }

    [Fact]
    public void TimersCannotSnooze()
    {
        var ringer = Create();
        ringer.Ring(AlarmRingKind.Timer, string.Empty, Start);

        ringer.Snooze(Start);

        Assert.True(ringer.IsRinging);
        Assert.False(ringer.IsSnoozed);
    }

    [Fact]
    public void RingingStopsByItselfAtTheLimit()
    {
        var ringer = Create();
        ringer.Ring(AlarmRingKind.Alarm, string.Empty, Start);

        ringer.Tick(Start + AlarmRinger.RingLimit - TimeSpan.FromSeconds(1));
        Assert.True(ringer.IsRinging);

        ringer.Tick(Start + AlarmRinger.RingLimit);
        Assert.False(ringer.IsRinging);
        Assert.Equal(1, stops);
    }

    [Fact]
    public void ANewRingReplacesTheCurrentTone()
    {
        var ringer = Create();
        ringer.Ring(AlarmRingKind.Alarm, "First", Start);

        ringer.Ring(AlarmRingKind.Timer, string.Empty, Start + TimeSpan.FromMinutes(1));

        Assert.Equal(1, stops);
        Assert.Equal(2, starts);
        Assert.Equal(AlarmRingKind.Timer, ringer.Kind);
    }
}
