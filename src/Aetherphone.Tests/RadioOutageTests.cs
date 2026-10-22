using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioOutageTests
{
    private const long Start = 1_000_000L;

    [Fact]
    public void ALostConnectionExpiresAfterFifteenMinutes()
    {
        var outage = new RadioOutage();
        outage.Observe(RadioPlaybackState.Failed, Start);

        Assert.False(outage.Expired(Start + RadioOutage.StopAfterMilliseconds - 1));
        Assert.True(outage.Expired(Start + RadioOutage.StopAfterMilliseconds));
    }

    [Fact]
    public void ReconnectingThenFailingCountsAsOneOutage()
    {
        var outage = new RadioOutage();
        outage.Observe(RadioPlaybackState.Reconnecting, Start);
        outage.Observe(RadioPlaybackState.Failed, Start + 60_000L);
        outage.Observe(RadioPlaybackState.Reconnecting, Start + 120_000L);

        Assert.True(outage.Expired(Start + RadioOutage.StopAfterMilliseconds));
    }

    [Fact]
    public void AStationThatComesBackStartsTheClockOver()
    {
        var outage = new RadioOutage();
        outage.Observe(RadioPlaybackState.Reconnecting, Start);
        outage.Observe(RadioPlaybackState.Playing, Start + 600_000L);
        outage.Observe(RadioPlaybackState.Reconnecting, Start + 700_000L);

        Assert.False(outage.Expired(Start + RadioOutage.StopAfterMilliseconds));
        Assert.True(outage.Expired(Start + 700_000L + RadioOutage.StopAfterMilliseconds));
    }

    [Theory]
    [InlineData((byte)RadioPlaybackState.Stopped)]
    [InlineData((byte)RadioPlaybackState.Paused)]
    [InlineData((byte)RadioPlaybackState.Buffering)]
    [InlineData((byte)RadioPlaybackState.Playing)]
    public void StatesThatAreNotLostNeverExpire(byte state)
    {
        var outage = new RadioOutage();
        outage.Observe(RadioPlaybackState.Failed, Start);
        outage.Observe((RadioPlaybackState)state, Start + 1_000L);

        Assert.False(outage.Expired(Start + RadioOutage.StopAfterMilliseconds * 2));
    }

    [Fact]
    public void ANewOutageNeverExpiresBeforeItStarts()
    {
        var outage = new RadioOutage();

        Assert.False(outage.Expired(Start + RadioOutage.StopAfterMilliseconds));
    }
}
