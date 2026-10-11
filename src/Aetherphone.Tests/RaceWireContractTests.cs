using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceWireContractTests
{
    [Fact]
    public void TheRoutesAndTheRoomMatchTheBackend()
    {
        Assert.Equal("/casino/race/bet", CasinoClient.RaceBetPath);
        Assert.Equal("/casino/race/race-track/bets", CasinoClient.RaceBetsPath(CasinoRoomIds.RaceTrack));
        Assert.Equal("/casino/race/a%2Fb/bets", CasinoClient.RaceBetsPath("a/b"));
        Assert.Equal("casino.race", CasinoWire.RaceKind);
        Assert.Equal("race-track", CasinoRoomIds.RaceTrack);
        Assert.Equal("race", CasinoGames.Race);
        Assert.Equal(CasinoWire.RaceKind, CasinoWire.Kind(CasinoGames.Race));
    }

    [Fact]
    public void TheBetRequestSerializesTheBackendShape()
    {
        var request = new CasinoRaceBetRequest("race-track", 41, "r-1", "b-1", RaceRules.KindForecast, 3, 5, 2500);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoRaceBetRequest);
        Assert.Equal(
            "{\"roomId\":\"race-track\",\"roundIndex\":41,\"clientRoundId\":\"r-1\",\"clientBetId\":\"b-1\","
            + "\"kind\":2,\"runner\":3,\"runnerB\":5,\"amount\":2500}",
            json);
    }

    [Fact]
    public void AGrantedTicketCarriesTheTotalsAndTheStack()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roomId\":\"race-track\",\"roundIndex\":41,"
            + "\"roundId\":\"r-1\",\"betId\":\"b-1\",\"kind\":0,\"runner\":3,\"runnerB\":-1,\"amount\":2500,"
            + "\"tickets\":2,\"myStake\":5000,\"stack\":145000,\"ceiling\":0}";
        var bet = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRaceBetDto);
        Assert.NotNull(bet);
        Assert.True(bet.Granted);
        Assert.Equal(41, bet.RoundIndex);
        Assert.Equal("b-1", bet.BetId);
        Assert.Equal(RaceRules.NoRunner, bet.RunnerB);
        Assert.Equal(2, bet.Tickets);
        Assert.Equal(5000, bet.MyStake);
        Assert.Equal(145000, bet.Stack);
    }

    [Fact]
    public void ACeilingRefusalNamesTheCapAndHasItsOwnWords()
    {
        const string json = "{\"granted\":false,\"reason\":\"ceiling\",\"roomId\":\"race-track\",\"roundIndex\":41,"
            + "\"ceiling\":250000}";
        var bet = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRaceBetDto);
        Assert.NotNull(bet);
        Assert.False(bet.Granted);
        Assert.Equal(250000, bet.Ceiling);
        Assert.Equal(L.Strip.ReasonCeiling.Key, CasinoReasons.MessageFor(bet.Reason).Key);
    }

    [Fact]
    public void EveryRaceRefusalHasItsOwnWords()
    {
        var reasons = new[]
        {
            CasinoReasons.StakeRange,
            CasinoReasons.Ladder,
            CasinoReasons.Ceiling,
            CasinoReasons.Closed,
            CasinoReasons.CapReached,
            CasinoReasons.Cooldown,
            CasinoReasons.StakesPaused,
            CasinoReasons.Draining,
            CasinoReasons.Frozen,
            CasinoReasons.Expired,
            CasinoReasons.Insufficient,
            CasinoReasons.LossLimit,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(L.Casino.ReasonGeneric.Key, message.Key);
        }
    }

    [Fact]
    public void ThePersonalReadCarriesTicketsPayoutsAndTheRide()
    {
        const string json = "{\"roomId\":\"race-track\",\"roundIndex\":41,\"phase\":1,\"roundId\":\"r-1\","
            + "\"tickets\":[{\"betId\":\"b-1\",\"kind\":0,\"runner\":3,\"runnerB\":-1,\"amount\":2500,\"payout\":19600},"
            + "{\"betId\":\"b-2\",\"kind\":3,\"runner\":3,\"runnerB\":5,\"amount\":500,\"payout\":0}],"
            + "\"myStake\":3000,\"myPayout\":19600,\"stack\":145000,\"previousRoundIndex\":40,\"previousPayout\":13500}";
        var mine = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRaceBetsDto);
        Assert.NotNull(mine);
        Assert.Equal(CasinoRoomPhases.Locked, mine.Phase);
        Assert.NotNull(mine.Tickets);
        Assert.Equal(2, mine.Tickets.Length);
        Assert.Equal(19600, mine.Tickets[0].Payout);
        Assert.Equal(RaceRules.KindReverse, mine.Tickets[1].Kind);
        Assert.Equal(5, mine.Tickets[1].RunnerB);
        Assert.Equal(19600, mine.MyPayout);
        Assert.Equal(40, mine.PreviousRoundIndex);
        Assert.Equal(13500, mine.PreviousPayout);
    }

    [Fact]
    public void AnEmptyPersonalReadDefaultsToNoRide()
    {
        var mine = JsonSerializer.Deserialize("{}", AethernetJsonContext.Default.CasinoRaceBetsDto);
        Assert.NotNull(mine);
        Assert.Equal(-1, mine.PreviousRoundIndex);
        Assert.Equal(0, mine.PreviousPayout);
    }

    [Fact]
    public void TheRoomStateCarriesTheFieldTheDrawAndTheDividends()
    {
        const string json = "{\"roundIndex\":41,\"commit\":\"9f1c\",\"nextCommit\":\"77ab\",\"seed\":\"abcd\","
            + "\"runners\":[{\"slot\":0,\"bird\":23,\"name\":\"Wild Wagtail\",\"colour\":11,\"silk\":0,\"rating\":3,"
            + "\"oddsHundredths\":697,\"placeOddsHundredths\":232,\"form\":[2,1,5,0,0],\"backers\":3,\"pool\":12500}],"
            + "\"order\":[2,4,3,5,7,0,6,1],\"photoFinish\":true,\"raceStartUnixMs\":1700000000000,\"staked\":48000,"
            + "\"paid\":19600,\"results\":[{\"kind\":0,\"runner\":2,\"runnerB\":-1,\"payHundredths\":613},"
            + "{\"kind\":2,\"runner\":2,\"runnerB\":4,\"payHundredths\":2915}],"
            + "\"formBook\":[[2,1,5,0,0],[0,0,0,0,0]],\"minBet\":100,\"maxTickets\":6,"
            + "\"overroundBasisPoints\":10200,\"ticksPerSecond\":25,\"raceTicks\":800}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRaceRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal(41, board.RoundIndex);
        Assert.NotNull(board.Runners);
        var runner = board.Runners[0];
        Assert.Equal("Wild Wagtail", runner.Name);
        Assert.Equal(23, runner.Bird);
        Assert.Equal(11, runner.Colour);
        Assert.Equal(697, runner.OddsHundredths);
        Assert.Equal(232, runner.PlaceOddsHundredths);
        Assert.Equal(new[] { 2, 1, 5, 0, 0 }, runner.Form);
        Assert.Equal(3, runner.Backers);
        Assert.Equal(12500, runner.Pool);
        Assert.Equal(new[] { 2, 4, 3, 5, 7, 0, 6, 1 }, board.Order);
        Assert.True(board.PhotoFinish);
        Assert.Equal(1700000000000, board.RaceStartUnixMs);
        Assert.NotNull(board.Results);
        Assert.Equal(2915, board.Results[1].PayHundredths);
        Assert.Equal(4, board.Results[1].RunnerB);
        Assert.NotNull(board.FormBook);
        Assert.Equal(2, board.FormBook.Length);
        Assert.Equal(RaceRules.MinBet, board.MinBet);
        Assert.Equal(RaceRules.MaxTickets, board.MaxTickets);
        Assert.Equal(RaceRules.OverroundBasisPoints, board.OverroundBasisPoints);
        Assert.Equal(RaceScript.TicksPerSecond, board.TicksPerSecond);
        Assert.Equal(RaceScript.RaceTicks, board.RaceTicks);
    }

    [Fact]
    public void AnOpenRoomHasNoDrawYet()
    {
        var board = JsonSerializer.Deserialize("{\"roundIndex\":41,\"seed\":\"\",\"order\":[]}",
            AethernetJsonContext.Default.CasinoRaceRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal(string.Empty, board.Seed);
        Assert.NotNull(board.Order);
        Assert.Empty(board.Order);
        Assert.Equal(0, board.RaceStartUnixMs);
    }

    [Fact]
    public void TheRoomSessionParsesTheRaceBlob()
    {
        var snapshot = new CasinoRoomSnapshotDto(RoomId: CasinoRoomIds.RaceTrack, GameKind: CasinoWire.RaceKind,
            GameState: "{\"roundIndex\":9,\"order\":[0,1,2,3,4,5,6,7]}");
        var state = CasinoRoomSession.Build(CasinoRoomIds.RaceTrack, 1, 3, snapshot);
        Assert.NotNull(state.Race);
        Assert.Equal(9, state.Race.RoundIndex);
        Assert.Null(state.Wheel);
        Assert.Null(state.Bingo);
        Assert.Null(state.Blackjack);
    }
}
