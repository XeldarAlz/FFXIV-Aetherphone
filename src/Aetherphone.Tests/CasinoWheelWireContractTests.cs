using System.Text.Json;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoWheelWireContractTests
{
    [Fact]
    public void TheMoneyPathsAreOrdinaryHttpRoutes()
    {
        Assert.Equal("/casino/wheel/bet", CasinoClient.WheelBetPath);
        Assert.Equal("/casino/wheel/wheel-floor/bets", CasinoClient.WheelBetsPath(CasinoRoomIds.WheelFloor));
        Assert.Equal("/casino/wheel/a%2Fb/bets", CasinoClient.WheelBetsPath("a/b"));
        Assert.Equal("casino.wheel", CasinoWire.WheelKind);
    }

    [Fact]
    public void TheBetRequestSerializesTheBackendShape()
    {
        var request = new CasinoWheelBetRequest("wheel-floor", 7, "round1", "bet1", 3, 25);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoWheelBetRequest);
        Assert.Equal(
            "{\"roomId\":\"wheel-floor\",\"roundIndex\":7,\"clientRoundId\":\"round1\","
            + "\"clientBetId\":\"bet1\",\"spot\":3,\"amount\":25}",
            json);
    }

    [Fact]
    public void AGrantedBetCarriesTheStackAndTheRoundItLandedIn()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roomId\":\"wheel-floor\",\"roundIndex\":7,"
            + "\"roundId\":\"r7\",\"spot\":3,\"amount\":25,\"myStake\":45,\"stack\":205}";
        var bet = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoWheelBetDto);

        Assert.NotNull(bet);
        Assert.True(bet.Granted);
        Assert.Equal("wheel-floor", bet.RoomId);
        Assert.Equal(7, bet.RoundIndex);
        Assert.Equal("r7", bet.RoundId);
        Assert.Equal(3, bet.Spot);
        Assert.Equal(25, bet.Amount);
        Assert.Equal(45, bet.MyStake);
        Assert.Equal(205, bet.Stack);
    }

    [Fact]
    public void ABetResponseCarriesChipsAndNeverAWalletBalance()
    {
        var properties = typeof(CasinoWheelBetDto).GetProperties();
        var hasStack = false;
        for (var index = 0; index < properties.Length; index++)
        {
            Assert.NotEqual("Balance", properties[index].Name);
            if (string.Equals(properties[index].Name, "Stack", StringComparison.Ordinal))
            {
                hasStack = true;
            }
        }

        Assert.True(hasStack);
    }

    [Fact]
    public void ARefusedBetStillNamesAReasonTheCabinetCanSpeak()
    {
        const string json = "{\"granted\":false,\"reason\":\"closed\",\"roomId\":\"wheel-floor\","
            + "\"roundIndex\":7,\"roundId\":\"\",\"spot\":3,\"amount\":0,\"myStake\":0,\"stack\":205}";
        var bet = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoWheelBetDto);

        Assert.NotNull(bet);
        Assert.False(bet.Granted);
        Assert.Equal(CasinoReasons.Closed, bet.Reason);
        Assert.True(CasinoReasons.TryMessage(bet.Reason, out _));
    }

    [Fact]
    public void EveryWheelReasonTheBackendCanSendHasItsOwnWords()
    {
        var reasons = new[]
        {
            CasinoReasons.Closed,
            CasinoReasons.Locked,
            CasinoReasons.NotRunning,
            CasinoReasons.StakeInvalid,
            CasinoReasons.Pacing,
            CasinoReasons.Unavailable,
            CasinoReasons.Ended,
            CasinoReasons.Restarting,
            CasinoReasons.Insufficient,
            CasinoReasons.StakesPaused,
            CasinoReasons.LossLimit,
            CasinoReasons.Cooldown,
            CasinoReasons.Frozen,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        }

        Assert.Equal("closed", CasinoReasons.Closed);
        Assert.Equal("locked", CasinoReasons.Locked);
        Assert.Equal("not_running", CasinoReasons.NotRunning);
        Assert.Equal("stake_invalid", CasinoReasons.StakeInvalid);
        Assert.Equal("pacing", CasinoReasons.Pacing);
        Assert.Equal("unavailable", CasinoReasons.Unavailable);
        Assert.Equal("ended", CasinoReasons.Ended);
        Assert.Equal("restarting", CasinoReasons.Restarting);
    }

    [Fact]
    public void TheWheelBlobCarriesTheSpotBoardAndTheDrawnSegment()
    {
        const string json = "{\"roundIndex\":7,\"commit\":\"aa\",\"nextCommit\":\"bb\",\"seed\":\"cc\","
            + "\"segment\":31,\"spot\":1,\"spots\":[{\"spot\":0,\"multiplier\":1,\"segments\":24,"
            + "\"returnBasisPoints\":9600,\"capacity\":10000000,\"amount\":22000,\"bettors\":5},{\"spot\":4,"
            + "\"multiplier\":23,\"segments\":2,\"returnBasisPoints\":9600,\"capacity\":434782,\"amount\":2000,"
            + "\"bettors\":1}],\"staked\":64000,\"paid\":30000,\"recent\":[4,1,0],\"minBet\":500,"
            + "\"maxBetPerSpot\":50000,\"maxBetPerRound\":200000,\"maxWin\":0}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoWheelRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(7, board.RoundIndex);
        Assert.Equal(31, board.Segment);
        Assert.Equal(1, board.Spot);
        Assert.NotNull(board.Spots);
        Assert.Equal(2, board.Spots.Length);
        Assert.Equal(22000, board.Spots[0].Amount);
        Assert.Equal(4, board.Spots[1].Spot);
        Assert.Equal(23, board.Spots[1].Multiplier);
        Assert.Equal(WheelRules.ReturnBasisPointsFor(4), board.Spots[1].ReturnBasisPoints);
        Assert.Equal(64000, board.Staked);
        Assert.Equal(new[] { 4, 1, 0 }, board.Recent);
        Assert.Equal(0, board.MaxWin);
        Assert.Equal(WheelRules.MinStakePerSpot, board.MinBet);
        Assert.Equal(WheelRules.MaxStakePerSpot, board.MaxBetPerSpot);
        Assert.Equal(WheelRules.MaxStakePerRound, board.MaxBetPerRound);
    }

    [Fact]
    public void TheRimHoldsUntilTheRoomPublishesADrawnSegment()
    {
        var open = new CasinoWheelRoomStateDto(RoundIndex: 7);
        Assert.Equal(-1, open.Segment);
        Assert.Equal(-1, WheelRoundPlayback.DrawnSegment(open));

        var drawn = open with { Segment = 31 };
        Assert.Equal(31, WheelRoundPlayback.DrawnSegment(drawn));
    }

    [Fact]
    public void ThePersonalReadCarriesOnlyAcceptedBets()
    {
        const string json = "{\"roomId\":\"wheel-floor\",\"roundIndex\":7,\"phase\":0,\"roundId\":\"r7\","
            + "\"bets\":[{\"spot\":0,\"amount\":25},{\"spot\":3,\"amount\":20}],\"myStake\":45,\"stack\":205}";
        var mine = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoWheelBetsDto);

        Assert.NotNull(mine);
        Assert.Equal("wheel-floor", mine.RoomId);
        Assert.Equal(7, mine.RoundIndex);
        Assert.Equal(CasinoRoomPhases.Open, mine.Phase);
        Assert.NotNull(mine.Bets);
        Assert.Equal(2, mine.Bets.Length);
        Assert.Equal(25, mine.Bets[0].Amount);
        Assert.Equal(3, mine.Bets[1].Spot);
        Assert.Equal(45, mine.MyStake);
    }

    [Fact]
    public void TheRimMirrorsTheRatifiedTableSegmentForSegment()
    {
        Assert.Equal(50, WheelRules.SegmentCount);
        Assert.Equal(WheelRules.SegmentCount, WheelRules.Segments.Length);
        Assert.Equal(5, WheelRules.SpotCount);

        Span<int> counted = stackalloc int[WheelRules.SpotCount];
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            counted[WheelRules.SpotAt(segment)]++;
        }

        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            Assert.Equal(WheelRules.SegmentsOn(spot), counted[spot]);
        }
    }

    [Fact]
    public void TheRoundCapIsClampedBeforeTheBetLeaves()
    {
        Assert.Equal(WheelRules.MaxStakePerSpot, WheelRules.Headroom(0));
        Assert.Equal(2000, WheelRules.Headroom(WheelRules.MaxStakePerRound - 2000));
        Assert.Equal(0, WheelRules.Headroom(WheelRules.MaxStakePerRound));

        Assert.Equal(2500, WheelRules.Clamp(2500, 0, 500_000));
        Assert.Equal(WheelRules.MaxStakePerSpot, WheelRules.Clamp(500_000, 0, 500_000));
        Assert.Equal(2000, WheelRules.Clamp(5000, WheelRules.MaxStakePerRound - 2000, 500_000));
        Assert.Equal(0, WheelRules.Clamp(5000, WheelRules.MaxStakePerRound, 500_000));
        Assert.Equal(0, WheelRules.Clamp(1, 0, 500_000));
        Assert.Equal(0, WheelRules.Clamp(2500, 0, 4));
    }

    [Fact]
    public void AWinningSpotReturnsTheStakeBesideTheWinnings()
    {
        Assert.True(WheelRules.Wins(0, WheelRules.SpotAt(0)));
        Assert.Equal(50, WheelRules.Returned(0, WheelRules.SpotAt(0), 25));
        Assert.Equal(0, WheelRules.Returned(0, WheelRules.SpotAt(0) + 1, 25));
        Assert.False(WheelRules.IsSegment(WheelRules.SegmentCount));
        Assert.Equal(-1, WheelRules.SpotAt(WheelRules.SegmentCount));
    }
}
