using System.Text.Json;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HoldemWireContractTests
{
    [Fact]
    public void TheRoutesMatchTheServer()
    {
        Assert.Equal("/casino/holdem/sit", CasinoClient.HoldemSitPath);
        Assert.Equal("/casino/holdem/leave", CasinoClient.HoldemLeavePath);
        Assert.Equal("/casino/holdem/act", CasinoClient.HoldemActPath);
        Assert.Equal("/casino/holdem/topup", CasinoClient.HoldemTopUpPath);
        Assert.Equal("/casino/holdem/timebank", CasinoClient.HoldemTimeBankPath);
        Assert.Equal("/casino/holdem/sitout", CasinoClient.HoldemSitOutPath);
        Assert.Equal("/casino/holdem/holdem-low/hand", CasinoClient.HoldemHandPath("holdem-low"));
        Assert.Equal("/casino/holdem/holdem-low/history", CasinoClient.HoldemHistoryPath("holdem-low"));
        Assert.Equal("casino.holdem", HoldemRules.Kind);
    }

    [Fact]
    public void RequestsSerializeInCamelCase()
    {
        var sit = JsonSerializer.Serialize(new CasinoHoldemSitRequest("holdem-low", 2, "s1", "a1", 10_000, true),
            AethernetJsonContext.Default.CasinoHoldemSitRequest);
        Assert.Equal(
            "{\"roomId\":\"holdem-low\",\"seatIndex\":2,\"clientSittingId\":\"s1\",\"clientActionId\":\"a1\",\"buyIn\":10000,\"postBigBlind\":true}",
            sit);
        var act = JsonSerializer.Serialize(new CasinoHoldemActRequest("holdem-low", "h", 7, "a2", "raise", 300),
            AethernetJsonContext.Default.CasinoHoldemActRequest);
        Assert.Equal(
            "{\"roomId\":\"holdem-low\",\"handId\":\"h\",\"actionCount\":7,\"clientActionId\":\"a2\",\"action\":\"raise\",\"amount\":300}",
            act);
        var sitOut = JsonSerializer.Serialize(new CasinoHoldemSitOutRequest("r", false, true),
            AethernetJsonContext.Default.CasinoHoldemSitOutRequest);
        Assert.Equal("{\"roomId\":\"r\",\"sitOut\":false,\"postBigBlind\":true}", sitOut);
        var bank = JsonSerializer.Serialize(new CasinoHoldemTimeBankRequest("r", "h", 3),
            AethernetJsonContext.Default.CasinoHoldemTimeBankRequest);
        Assert.Equal("{\"roomId\":\"r\",\"handId\":\"h\",\"actionCount\":3}", bank);
    }

    [Fact]
    public void TheGameStateReadsTheShapeTheServerSends()
    {
        const string json = """
        {
          "handId": "h", "handIndex": 12, "phase": 3, "button": 2, "smallBlind": 50, "bigBlind": 100, "ante": 0,
          "seats": [ {
            "seatIndex": 0, "userId": "u1", "displayName": "Mira", "avatarUrl": "", "frameId": "", "title": "shark",
            "stack": 9400, "bet": 200, "committed": 600, "state": 1, "actedThisStreet": true, "lastAction": "raise",
            "timeBankLeft": 3, "connected": true, "leaving": false, "cards": [-1, -1], "shown": false, "handRank": -1,
            "best5": [], "won": 0, "sittingOutEndsAtUnixMs": 0
          } ],
          "board": [12, 25, 38], "pots": [ { "amount": 1800, "eligible": [0, 2, 4] } ], "potTotal": 2200,
          "toCall": 200, "minRaiseTo": 400, "cursorSeat": 4, "actionCount": 17,
          "deadlineUnixMs": 0, "windowSeconds": 20,
          "commit": "hex", "nextCommit": "hex", "seed": "",
          "rake": 0, "rakeBasisPoints": 500, "rakeCap": 300,
          "winners": [ { "seatIndex": 0, "potIndex": 0, "amount": 1710, "strength": 2367488, "best5": [0, 13, 26, 5, 18] } ],
          "minBuyIn": 2000, "maxBuyIn": 10000, "maxSeats": 6, "turnSeconds": 20, "timeBankSeconds": 10,
          "practice": false, "name": "", "stakeTier": 0, "faceUp": false, "practiceRebuy": false,
          "practiceStack": 0, "paused": false
        }
        """;

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoHoldemRoomStateDto)!;
        Assert.Equal(12, state.HandIndex);
        Assert.Equal(HoldemPhases.Turn, state.Phase);
        var seat = Assert.Single(state.Seats!);
        Assert.Equal("shark", seat.Title);
        Assert.Equal(600, seat.Committed);
        Assert.Equal(new[] { -1, -1 }, seat.Cards);
        Assert.Equal(new[] { 0, 2, 4 }, Assert.Single(state.Pots!).Eligible);
        Assert.Equal(1710, Assert.Single(state.Winners!).Amount);
        Assert.Equal(17, state.ActionCount);
        Assert.Equal(300, state.RakeCap);
        Assert.Equal(Apps.Casino.Stage.BalanceTitle.Shark, HoldemTable.TitleOf(seat.Title));
    }

    [Fact]
    public void ThePrivateLaneReadsCardsAndThePrompt()
    {
        const string payload = """
        { "handId": "h", "seatIndex": 0, "cards": [12, 25], "actionCount": 17, "stack": 9400,
          "strength": 1118208, "winChance": 6230,
          "prompt": { "actions": 21, "toCall": 200, "minRaiseTo": 400, "maxRaiseTo": 9600, "potTotal": 2200,
                      "deadlineUnixMs": 0, "timeBankLeft": 3 } }
        """;

        var mine = CasinoRoomSession.BuildHoldemPrivate(new CasinoPrivateDto("you.cards", payload))!;
        Assert.Equal(new[] { 12, 25 }, mine.Cards);
        Assert.Equal(6230, mine.WinChance);
        Assert.Equal(21, mine.Prompt!.Actions);
        Assert.True(HoldemActions.Allows(mine.Prompt.Actions, HoldemActions.Raise));
        Assert.Equal("62%", HoldemHandNames.Percent(mine.WinChance).Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void TheRoomSessionParsesHoldemRooms()
    {
        var snapshot = new CasinoRoomSnapshotDto(RoomId: "holdem-low", GameKind: "casino.holdem",
            GameState: "{\"handId\":\"h\",\"phase\":1,\"seats\":[{\"seatIndex\":3,\"userId\":\"me\",\"state\":1}]}");
        var state = CasinoRoomSession.Build("holdem-low", 1, 1, snapshot);
        Assert.NotNull(state.Holdem);
        Assert.Null(state.Blackjack);
        Assert.Equal(3, HoldemTable.SeatOf(state.Holdem!, "me"));
    }

    [Fact]
    public void HistoryReadsTheServerShape()
    {
        const string json = """
        { "roomId": "holdem-low", "hands": [ {
          "handId": "h", "handIndex": 12, "startedAtUnixMs": 0, "button": 2, "smallBlind": 50, "bigBlind": 100,
          "board": [12, 25, 38, 7, 44], "rake": 110, "potTotal": 2200, "commit": "hex", "seed": "hex",
          "seats": [ { "seatIndex": 0, "userId": "u1", "displayName": "Mira", "cards": [12, 25], "committed": 600,
                       "won": 1710, "folded": false, "shown": true, "strength": 2367488 } ],
          "net": 1110 } ] }
        """;

        var history = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoHoldemHistoryDto)!;
        var hand = Assert.Single(history.Hands!);
        Assert.Equal(1110, hand.Net);
        Assert.Equal(HoldemReplayStep.Showdown, HoldemReplay.LastStepOf(hand));
        Assert.Equal(HoldemReplayStep.River, HoldemReplay.Advance(HoldemReplayStep.Showdown, -1, HoldemReplayStep.Showdown));
    }

    [Fact]
    public void TheHandRouteReusesTheBlackjackEnvelope()
    {
        const string json = """
        { "roomId": "holdem-low", "epoch": 1, "seq": 5, "eventKind": "you.cards",
          "payload": "{\"handId\":\"h\",\"seatIndex\":0,\"cards\":[1,2]}", "serverNowUnixMs": 1 }
        """;

        var envelope = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackHandStateDto)!;
        var mine = CasinoRoomSession.BuildHoldemPrivate(new CasinoPrivateDto(envelope.EventKind, envelope.Payload));
        Assert.Equal(new[] { 1, 2 }, mine!.Cards);
    }

    [Fact]
    public void TurnNotificationsKeyOnTheActionCount()
    {
        var board = new CasinoHoldemRoomStateDto(HandId: "h", CursorSeat: 2, ActionCount: 5);
        var mine = new CasinoHoldemYouDto("h", 2, Prompt: new CasinoHoldemPromptDto(Actions: 1 | 4));
        Assert.NotNull(CasinoTurnNotifier.HoldemTurnKey(board, mine));
        Assert.Null(CasinoTurnNotifier.HoldemTurnKey(board with { CursorSeat = 3 }, mine));
        Assert.Null(CasinoTurnNotifier.HoldemTurnKey(board, mine with { Prompt = new CasinoHoldemPromptDto(64 | 128) }));
    }

    [Fact]
    public void TheStateLineSaysYourTurnOnlyWithALivePrompt()
    {
        var board = new CasinoHoldemRoomStateDto(HandId: "h", Phase: HoldemPhases.Flop, CursorSeat: 2, ActionCount: 5);
        var mine = new CasinoHoldemYouDto("h", 2, ActionCount: 5, Prompt: new CasinoHoldemPromptDto(Actions: 2));
        Assert.Equal(L.Holdem.StateYourTurn.Key, HoldemTable.StateText(board, mine, null).Key);
        Assert.Null(HoldemTable.StateText(board, mine with { ActionCount = 4 }, null).Key);
        Assert.Equal(L.Holdem.StatePickSeat.Key,
            HoldemTable.StateText(new CasinoHoldemRoomStateDto(), null, null).Key);
    }

    [Fact]
    public void TheHoldemRefusalsAreCovered()
    {
        string[] reasons =
        {
            "pair_limited", "pvp_loss_cap", "pvp_win_cap", "in_hand", "title_required", "time_bank_empty",
            "holdem_closed",
        };
        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.Contains(reasons[index], CasinoReasons.All);
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message));
            Assert.StartsWith("casino.holdem.", message.Key, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HostedHoldemConfigsFollowTheServerBands()
    {
        var draft = new HostDraft();
        draft.Reset(HostGame.Holdem);
        draft.Seats = 9;
        var config = draft.Build(null);
        Assert.Equal("casino.holdem", config.GameKind);
        Assert.Equal(50, config.Poker!.SmallBlind);
        Assert.Equal(0, config.MinBuyIn);
        Assert.Equal(0, config.MaxBuyIn);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { Seats = 10 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { Currency = 2 }));
        Assert.Equal(CasinoReasons.PracticeOnly, CasinoHostingRules.Check(config with { FaceUp = true }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(config with { Poker = config.Poker with { SmallBlind = 60 } }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(config with { MaxBuyIn = 100 * 600 }));
        draft.Currency = CasinoCurrencies.Practice;
        draft.FaceUp = true;
        var practice = draft.Build(null);
        Assert.True(practice.FaceUp);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(practice));
    }
}
