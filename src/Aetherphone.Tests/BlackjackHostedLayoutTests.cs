using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackHostedLayoutTests
{
    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void AnInfoLineSitsBesideTheButtonsOnlyWhenItFitsWithItsPadding(float scale)
    {
        var pad = StageText.CapsulePadX * 2f * scale;

        Assert.True(HostedInfoLayout.FitsLine(100f * scale, 100f * scale + pad, scale));
        Assert.False(HostedInfoLayout.FitsLine(100f * scale, 100f * scale + pad - 4f * scale, scale));
        Assert.False(HostedInfoLayout.FitsLine(10f * scale, 0f, scale));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void WrappedInfoBlocksGrowWithTheirLinesAndKeepTheCapsuleMinimum(float scale)
    {
        var line = 16f * scale;
        var one = HostedInfoLayout.BlockHeight(line, 1, scale);
        var two = HostedInfoLayout.BlockHeight(line, 2, scale);

        Assert.True(one >= StageText.MinimumCapsuleHeight * scale - 0.01f);
        Assert.True(two >= line * 2f);
        Assert.True(two > one);
    }

    [Fact]
    public void TheTournamentStripReservesItsBandOnlyWhileItShows()
    {
        Assert.False(TournamentOverlay.Shows(null));
        Assert.False(TournamentOverlay.Shows(new CasinoBlackjackRoomStateDto()));
        Assert.False(TournamentOverlay.Shows(new CasinoBlackjackRoomStateDto(
            Tournament: new CasinoBlackjackTournamentDto())));
        Assert.True(TournamentOverlay.Shows(new CasinoBlackjackRoomStateDto(
            Tournament: new CasinoBlackjackTournamentDto(Live: true))));
        Assert.True(TournamentOverlay.Shows(new CasinoBlackjackRoomStateDto(
            Tournament: new CasinoBlackjackTournamentDto(WinnerUserId: "winner"))));
        Assert.True(TournamentOverlay.Reserve(1f) >= TournamentOverlay.StripHeight);
    }
}
