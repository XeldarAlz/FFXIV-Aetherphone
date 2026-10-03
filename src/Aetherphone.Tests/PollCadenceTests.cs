using Aetherphone.Core;
using Aetherphone.Core.Runtime;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PollCadenceTests
{
    private static readonly TimeSpan Foreground = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Background = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan PushCovered = TimeSpan.FromMinutes(15);

    [Fact]
    public void WithoutASocketTheCadenceFollowsPhoneVisibility()
    {
        var phoneOpen = true;
        var cadence = Cadence(() => phoneOpen, new RealtimeSignalBus());

        Assert.Equal(Foreground, cadence.CurrentInterval);
        phoneOpen = false;
        Assert.Equal(Background, cadence.CurrentInterval);
    }

    [Fact]
    public void ALiveSocketStretchesAnUnwatchedListToTheSafetyPoll()
    {
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        var phoneOpen = true;
        var cadence = Cadence(() => phoneOpen, signals);

        Assert.Equal(PushCovered, cadence.CurrentInterval);
        phoneOpen = false;
        Assert.Equal(PushCovered, cadence.CurrentInterval);
    }

    [Fact]
    public void AListOnScreenKeepsItsNormalRefreshWhileTheSocketIsLive()
    {
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        var cadence = Cadence(() => true, signals);

        cadence.NoteWatched();

        Assert.True(cadence.IsWatched);
        Assert.Equal(Foreground, cadence.CurrentInterval);
    }

    [Fact]
    public void AWatchedListBehindAClosedPhoneStillUsesTheSafetyPoll()
    {
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        var cadence = Cadence(() => false, signals);

        cadence.NoteWatched();

        Assert.Equal(PushCovered, cadence.CurrentInterval);
    }

    [Fact]
    public void LosingTheSocketFallsBackToTheNormalRefresh()
    {
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        var cadence = Cadence(() => false, signals);
        var start = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(cadence.Due(start));
        Assert.False(cadence.Due(start + Background));

        signals.SetActive(false);

        Assert.True(cadence.Due(start + Background));
    }

    [Fact]
    public void APingStillFetchesImmediatelyUnderTheSafetyPoll()
    {
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        var cadence = Cadence(() => false, signals);
        var start = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(cadence.Due(start));
        Assert.False(cadence.Due(start.AddSeconds(1)));

        cadence.RequestImmediate();

        Assert.True(cadence.Due(start.AddSeconds(2)));
        Assert.False(cadence.Due(start.AddSeconds(3)));
    }

    private static PollCadence Cadence(Func<bool> phoneOpen, RealtimeSignalBus signals)
    {
        var visibility = new PhoneVisibility();
        visibility.Bind(phoneOpen);
        return new PollCadence(visibility, Foreground, Background, signals);
    }
}
