using System.Text.Json;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoBingoWireContractTests
{
    [Fact]
    public void TheHallHangsOffTheSameRoomRoutesAsTheWheel()
    {
        Assert.Equal("bingo-hall", CasinoRoomIds.BingoHall);
        Assert.Equal("casino.bingo", CasinoWire.BingoKind);
        Assert.Equal("/casino/rooms/bingo-hall", CasinoClient.RoomPath(CasinoRoomIds.BingoHall));
        Assert.Equal("/casino/bingo/cards", CasinoClient.BingoCardsPath);
        Assert.Equal("/casino/bingo/bingo-hall/cards", CasinoClient.BingoMyCardsPath(CasinoRoomIds.BingoHall));
        Assert.Equal("/casino/bingo/a%2Fb/cards", CasinoClient.BingoMyCardsPath("a/b"));
    }

    [Fact]
    public void TheCardPurchaseSerializesTheBackendShape()
    {
        var request = new CasinoBingoCardsRequest("bingo-hall", 7, "round1", 3);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoBingoCardsRequest);
        Assert.Equal(
            "{\"roomId\":\"bingo-hall\",\"roundIndex\":7,\"clientRoundId\":\"round1\",\"cardCount\":3}",
            json);
        Assert.Equal(3000, BingoRules.StakeFor(3));
    }

    [Fact]
    public void OneShapeAnswersBothTheBuyAndTheRead()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roomId\":\"bingo-hall\",\"roundIndex\":7,"
            + "\"roundId\":\"entry-1\",\"cards\":[[1,2,3,4,5,16,17,18,19,20,31,32,33,34,46,47,48,49,50,61,62,"
            + "63,64,65],[6,7,8,9,10,21,22,23,24,25,36,37,38,39,51,52,53,54,55,66,67,68,69,70]],\"stake\":2000,"
            + "\"payout\":11200,\"roundState\":1,\"seedCommitHash\":\"aa\",\"nextSeedHash\":\"bb\",\"stack\":20500}";
        var mine = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBingoCardsDto);

        Assert.NotNull(mine);
        Assert.True(mine.Granted);
        Assert.Equal("bingo-hall", mine.RoomId);
        Assert.Equal(7, mine.RoundIndex);
        Assert.Equal("entry-1", mine.RoundId);
        Assert.NotNull(mine.Cards);
        Assert.Equal(2, mine.Cards.Length);
        Assert.Equal(BingoRules.CardNumbers, mine.Cards[0].Length);
        Assert.Equal(1, mine.Cards[0][0]);
        Assert.Equal(BingoRules.StakeFor(2), mine.Stake);
        Assert.Equal(11200, mine.Payout);
        Assert.Equal(CasinoRoundStates.Settled, mine.RoundState);
        Assert.Equal(20500, mine.Stack);
        Assert.Equal(2, BingoCabinet.HeldCards(mine));
        Assert.True(BingoCabinet.Settled(mine));
    }

    [Fact]
    public void ACardPurchaseCarriesChipsAndNeverAWalletBalance()
    {
        var properties = typeof(CasinoBingoCardsDto).GetProperties();
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
    public void ARefusedPurchaseNamesAReasonTheHallCanSpeak()
    {
        const string json = "{\"granted\":false,\"reason\":\"cards_full\",\"roomId\":\"bingo-hall\","
            + "\"roundIndex\":7,\"roundId\":\"\",\"cards\":[],\"stake\":0,\"payout\":0,\"roundState\":0,"
            + "\"seedCommitHash\":\"\",\"nextSeedHash\":\"\",\"stack\":205}";
        var mine = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBingoCardsDto);

        Assert.NotNull(mine);
        Assert.False(mine.Granted);
        Assert.Equal(CasinoReasons.CardsFull, mine.Reason);
        Assert.True(CasinoReasons.TryMessage(mine.Reason, out var message));
        Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        Assert.Equal(0, BingoCabinet.HeldCards(mine));
        Assert.False(BingoCabinet.Settled(mine));
    }

    [Fact]
    public void EveryHallReasonTheBackendCanSendHasItsOwnWords()
    {
        var reasons = new[]
        {
            CasinoReasons.CardsFull,
            CasinoReasons.Closed,
            CasinoReasons.Locked,
            CasinoReasons.NotRunning,
            CasinoReasons.Expired,
            CasinoReasons.Pacing,
            CasinoReasons.Insufficient,
            CasinoReasons.StakesPaused,
            CasinoReasons.LossLimit,
            CasinoReasons.Draining,
            CasinoReasons.Frozen,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        }

        Assert.Equal("cards_full", CasinoReasons.CardsFull);
    }

    [Fact]
    public void TheHallBlobCarriesTheLadderTheCapAndTheCalls()
    {
        const string json = "{\"roundIndex\":7,\"commit\":\"aa\",\"nextCommit\":\"bb\",\"seed\":\"cc\","
            + "\"cards\":40,\"players\":11,\"prizes\":[11388,15120,32596],\"prizeCardCap\":125,\"ballIndex\":3,"
            + "\"balls\":[42,7,55],\"nextBallAtUnixMs\":1754784003000,\"stages\":[{\"stage\":0,\"ball\":41,"
            + "\"prize\":11388,\"winners\":2,\"paid\":22776}],\"ended\":false,\"cancelled\":false,"
            + "\"cardPrice\":1000,\"maxCards\":6,\"maxWin\":0,\"ballIntervalMs\":1400,\"earlyBird\":true}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBingoRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(7, board.RoundIndex);
        Assert.Equal(40, board.Cards);
        Assert.Equal(11, board.Players);
        Assert.NotNull(board.Prizes);
        Assert.Equal(BingoRules.StageCount, board.Prizes.Length);
        Assert.Equal(BingoRules.PrizeCardCap, board.PrizeCardCap);
        Assert.Equal(BingoRules.CardPrice, board.CardPrice);
        Assert.Equal(BingoRules.MaxCards, board.MaxCards);
        Assert.Equal(0, board.MaxWin);
        Assert.Equal(BingoRules.BallIntervalMs, board.BallIntervalMs);
        Assert.Equal(BingoRules.BallIntervalMs, BingoCabinet.BallIntervalOf(board));
        Assert.True(board.EarlyBird);
        Assert.Equal(new[] { 42, 7, 55 }, board.Balls);
        Assert.Equal(1754784003000, board.NextBallAtUnixMs);
        Assert.False(board.Ended);

        Assert.Equal(40, BingoCabinet.CardsInPlay(board));
        var awarded = BingoCabinet.StageAwarded(board, BingoRules.StageLine);
        Assert.NotNull(awarded);
        Assert.Equal(41, awarded.Ball);
        Assert.Equal(2, awarded.Winners);
        Assert.Equal(22776, awarded.Paid);
    }

    [Fact]
    public void ThePublishedLadderAgreesWithTheMirroredRates()
    {
        const string json = "{\"roundIndex\":7,\"cards\":40,\"prizes\":[5916,7856,16940],\"prizeCardCap\":125}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBingoRoomStateDto);

        Assert.NotNull(board);
        Assert.NotNull(board.Prizes);
        Span<long> mirrored = stackalloc long[BingoRules.StageCount];
        BingoRules.PrizeLadder(board.Cards, mirrored);
        for (var stage = 0; stage < BingoRules.StageCount; stage++)
        {
            Assert.Equal(board.Prizes[stage], mirrored[stage]);
        }
    }

    [Fact]
    public void TheCallsAreAListRatherThanACountTheClientTicksUp()
    {
        var board = new CasinoBingoRoomStateDto(RoundIndex: 7, Balls: new[] { 42, 7, 55 }, BallIndex: 3);
        Assert.Equal(new[] { 42, 7, 55 }, BingoRoundPlayback.CalledBalls(board));
        Assert.Empty(BingoRoundPlayback.CalledBalls(null));
        Assert.Empty(BingoRoundPlayback.CalledBalls(new CasinoBingoRoomStateDto(RoundIndex: 7)));
    }

    [Fact]
    public void TheCardsReadFromThePersonalHalfRatherThanTheRoom()
    {
        var mine = new CasinoBingoCardsDto(
            Granted: true,
            RoomId: CasinoRoomIds.BingoHall,
            RoundIndex: 7,
            Cards: new[] { new[] { 1, 2 }, new[] { 3, 4 } });

        Assert.Equal(new[] { 1, 2 }, BingoRoundPlayback.CardAt(mine, 0));
        Assert.Equal(new[] { 3, 4 }, BingoRoundPlayback.CardAt(mine, 1));
        Assert.Null(BingoRoundPlayback.CardAt(mine, 2));
        Assert.Null(BingoRoundPlayback.CardAt(mine, -1));
        Assert.Null(BingoRoundPlayback.CardAt(null, 0));
    }

    [Fact]
    public void TheBallIntervalMirrorsTheRoomClock()
    {
        Assert.Equal(1400, BingoRules.BallIntervalMs);
        Assert.Equal(75, BingoRules.Balls);
        Assert.Equal(6, BingoRules.MaxCards);
        Assert.Equal(1000, BingoRules.CardPrice);
        Assert.Equal(BingoRules.BallIntervalMs, BingoCabinet.BallIntervalOf(null));
    }

    [Fact]
    public void TheLadderIsQuotedFromTheRoomWhenTheRoomPublishesOne()
    {
        var board = new CasinoBingoRoomStateDto(RoundIndex: 7, Cards: 40,
            Prizes: new long[] { 136, 176, 384 }, PrizeCardCap: 500);
        Span<long> ladder = stackalloc long[BingoRules.StageCount];

        BingoCabinet.LadderFor(board, ladder);

        Assert.Equal(136, ladder[BingoRules.StageLine]);
        Assert.Equal(176, ladder[BingoRules.StageTwoLines]);
        Assert.Equal(384, ladder[BingoRules.StageFullHouse]);
        Assert.Equal(384, BingoCabinet.PrizeAt(board, BingoRules.StageFullHouse));
        Assert.Equal(500, BingoCabinet.PrizeCardCapOf(board));
    }

    [Fact]
    public void TheMirroredRatesOnlyAnswerARoomThatPublishedNoTable()
    {
        var board = new CasinoBingoRoomStateDto(RoundIndex: 7, Cards: 40);
        Span<long> ladder = stackalloc long[BingoRules.StageCount];

        BingoCabinet.LadderFor(board, ladder);

        for (var stage = 0; stage < BingoRules.StageCount; stage++)
        {
            Assert.Equal(BingoRules.PrizeFor(stage, 40), ladder[stage]);
        }

        Assert.Equal(BingoRules.PrizeCardCap, BingoCabinet.PrizeCardCapOf(board));
        Assert.Equal(BingoRules.PrizeCardCap, BingoCabinet.PrizeCardCapOf(null));
        Assert.Equal(0, BingoCabinet.PrizeAt(board, BingoRules.StageCount));
    }

    [Fact]
    public void ACancelledRoomAndAVoidedRoundBothReadAsCalledOff()
    {
        var running = new CasinoBingoRoomStateDto(RoundIndex: 7, Cards: 40);
        var cancelled = new CasinoBingoRoomStateDto(RoundIndex: 7, Cards: 0, Cancelled: true);
        var settled = new CasinoBingoCardsDto(Granted: true, RoomId: CasinoRoomIds.BingoHall, RoundIndex: 7,
            RoundState: CasinoRoundStates.Settled);
        var voided = new CasinoBingoCardsDto(Granted: true, RoomId: CasinoRoomIds.BingoHall, RoundIndex: 7,
            RoundState: CasinoRoundStates.Voided);

        Assert.False(BingoCabinet.CalledOff(running, settled));
        Assert.False(BingoCabinet.CalledOff(null, null));
        Assert.True(BingoCabinet.CalledOff(cancelled, null));
        Assert.True(BingoCabinet.CalledOff(cancelled, settled));
        Assert.True(BingoCabinet.CalledOff(running, voided));
        Assert.False(BingoCabinet.Settled(voided));
    }
}
