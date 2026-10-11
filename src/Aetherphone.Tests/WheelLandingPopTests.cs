using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelLandingPopTests
{
    [Fact]
    public void ALiveLandingPopsTheHubAndTheNewestChipIn()
    {
        var start = WheelCabinet.LandingPop(true, WheelStage.Settling, 0f);
        var middle = WheelCabinet.LandingPop(true, WheelStage.Settling, 0.15f);
        var done = WheelCabinet.LandingPop(true, WheelStage.Settling, 0.5f);
        Assert.True(start < 0.2f);
        Assert.True(middle > start);
        Assert.Equal(1f, done);
    }

    [Fact]
    public void AJoinAfterTheLandingShowsTheResultAtRest()
    {
        Assert.Equal(1f, WheelCabinet.LandingPop(false, WheelStage.Settling, 0f));
    }

    [Fact]
    public void NothingPopsBeforeTheWheelLands()
    {
        Assert.Equal(1f, WheelCabinet.LandingPop(true, WheelStage.Spinning, 0f));
        Assert.Equal(1f, WheelCabinet.LandingPop(true, WheelStage.Betting, 0f));
    }

    [Fact]
    public void AJoinDuringTheResultWindowSettlesWithoutReplayingTheSpin()
    {
        var playback = new WheelRoundPlayback();
        var snapshot = new CasinoRoomSnapshotDto(RoomId: CasinoRoomIds.WheelFloor, Phase: CasinoRoomPhases.Result,
            RoundIndex: 7);
        var board = new CasinoWheelRoomStateDto(RoundIndex: 7, Segment: 7, Spot: 3);
        playback.Update(snapshot, board, 6_000, 0.016f);
        Assert.Equal(WheelStage.Settling, playback.Stage);
        Assert.False(playback.SpunLive);
        Assert.Equal(1f, WheelCabinet.LandingPop(playback.SpunLive, playback.Stage, playback.StageSeconds));
    }
}
