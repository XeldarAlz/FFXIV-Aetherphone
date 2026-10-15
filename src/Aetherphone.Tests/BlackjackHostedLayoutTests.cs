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

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheHostButtonsShareANarrowRowInsteadOfRunningOffIt(float scale)
    {
        var gap = 8f * scale;
        var natural = new[] { 96f * scale, 84f * scale, 110f * scale };
        var total = natural[0] + natural[1] + natural[2];

        Assert.Equal(1f, HostedInfoLayout.ButtonSqueeze(total, natural.Length, total + gap * 2f, gap));
        Assert.Equal(1f, HostedInfoLayout.ButtonSqueeze(0f, 0, 10f, gap));
        var narrow = 240f * scale;
        var squeeze = HostedInfoLayout.ButtonSqueeze(total, natural.Length, narrow, gap);
        Assert.InRange(squeeze, 0f, 1f);
        Assert.Equal(narrow, total * squeeze + gap * 2f, 3);
    }

    [Fact]
    public void ARailPlateShortensANameToTheForename()
    {
        Assert.Equal("Chea", PitText.Forename("Chea Thorne"));
        Assert.Equal("Ciri", PitText.Forename("Ciri"));
        Assert.Equal(" Lead", PitText.Forename(" Lead"));
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

    [Fact]
    public void OnlyAnEntrantStillStandingInALiveTournamentForfeitsByLeaving()
    {
        const string me = "me";
        var standing = new CasinoBlackjackStandingDto(UserId: me, Chips: 5000);
        var running = new CasinoBlackjackTournamentDto(Live: true, Standings: new[] { standing });
        var live = new CasinoBlackjackRoomStateDto(Tournament: running);

        Assert.True(BlackjackHosting.StillInTournament(live, me));
        Assert.False(BlackjackHosting.StillInTournament(live, "spectator"));
        Assert.False(BlackjackHosting.StillInTournament(live, string.Empty));

        var eliminated = live with
        {
            Tournament = running with { Standings = new[] { standing with { Eliminated = true } } },
        };
        Assert.False(BlackjackHosting.StillInTournament(eliminated, me));

        var finished = live with { Tournament = running with { Live = false } };
        Assert.False(BlackjackHosting.StillInTournament(finished, me));
        Assert.False(BlackjackHosting.StillInTournament(new CasinoBlackjackRoomStateDto(), me));
    }

    [Fact]
    public void AFinishedStripGoesAwayOnceItsBoardWasClosedAndAClearedRecordNeverShows()
    {
        var overlay = new TournamentOverlay();
        var finished = new CasinoBlackjackTournamentDto(WinnerUserId: "winner");
        var over = new CasinoBlackjackRoomStateDto(Tournament: finished);
        var running = new CasinoBlackjackTournamentDto(Live: true);
        var live = new CasinoBlackjackRoomStateDto(Tournament: running);

        Assert.True(overlay.Showing(over));
        Assert.True(overlay.Showing(live));
        Assert.False(overlay.Showing(new CasinoBlackjackRoomStateDto()));
        Assert.False(overlay.Showing(null));

        overlay.CloseBoard(running);
        Assert.True(overlay.Showing(over));

        overlay.CloseBoard(finished);
        Assert.False(overlay.Showing(over));
        Assert.True(overlay.Showing(live));

        overlay.Reset();
        Assert.True(overlay.Showing(over));
    }
}
