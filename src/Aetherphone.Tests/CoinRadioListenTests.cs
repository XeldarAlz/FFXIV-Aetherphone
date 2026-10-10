using Aetherphone.Core.Coins;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinRadioListenTests
{
    private const string Station = "station-a";
    private const string OtherStation = "station-b";

    [Fact]
    public void ARefusedStartRetriesUnlessTheStationIsOwnOrGone()
    {
        Assert.Equal(CoinRadioListen.RetryMilliseconds, CoinRadioListen.RetryDelayFor(false, "offline", 0));
        Assert.Equal(CoinRadioListen.RetryMilliseconds, CoinRadioListen.RetryDelayFor(false, "unavailable", 0));
        Assert.Equal(CoinRadioListen.RetryMilliseconds, CoinRadioListen.RetryDelayFor(false, string.Empty, 500));
        Assert.Equal(0, CoinRadioListen.RetryDelayFor(false, "own_station", 0));
        Assert.Equal(0, CoinRadioListen.RetryDelayFor(false, string.Empty, 404));
        Assert.Equal(0, CoinRadioListen.RetryDelayFor(true, string.Empty, 0));
    }

    [Fact]
    public void StartsOnlyOncePlaybackIsActuallyPlaying()
    {
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(string.Empty, RadioPlaybackState.Buffering, Station, true));
        Assert.Equal(RadioListenStep.Start,
            CoinRadioListen.Decide(string.Empty, RadioPlaybackState.Playing, Station, true));
    }

    [Fact]
    public void DirectoryStationsWithoutACommunityIdNeverStart()
    {
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(string.Empty, RadioPlaybackState.Playing, string.Empty, true));
    }

    [Fact]
    public void SignedOutListenersNeverStart()
    {
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(string.Empty, RadioPlaybackState.Playing, Station, false));
    }

    [Fact]
    public void KeepsListeningThroughPlaybackAndShortReconnects()
    {
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Playing, Station, true));
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Reconnecting, Station, true));
        Assert.Equal(RadioListenStep.None,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Buffering, Station, true));
    }

    [Theory]
    [InlineData((byte)RadioPlaybackState.Paused)]
    [InlineData((byte)RadioPlaybackState.Stopped)]
    [InlineData((byte)RadioPlaybackState.Failed)]
    public void EndsWhenPlaybackPausesStopsOrFails(byte state)
    {
        Assert.Equal(RadioListenStep.End,
            CoinRadioListen.Decide(Station, (RadioPlaybackState)state, Station, true));
    }

    [Fact]
    public void EndsWhenTheStopClearsTheStation()
    {
        Assert.Equal(RadioListenStep.End,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Stopped, string.Empty, true));
    }

    [Fact]
    public void EndsWhileTheNextStationIsStillBuffering()
    {
        Assert.Equal(RadioListenStep.End,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Buffering, OtherStation, true));
    }

    [Fact]
    public void SwitchesWhenAnotherStationIsPlaying()
    {
        Assert.Equal(RadioListenStep.Switch,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Playing, OtherStation, true));
    }

    [Fact]
    public void EndsWhenSwitchingToADirectoryStation()
    {
        Assert.Equal(RadioListenStep.End,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Playing, string.Empty, true));
    }

    [Fact]
    public void EndsOnSignOut()
    {
        Assert.Equal(RadioListenStep.End,
            CoinRadioListen.Decide(Station, RadioPlaybackState.Playing, Station, false));
    }
}
