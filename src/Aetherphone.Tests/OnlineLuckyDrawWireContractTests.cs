using System.Text.Json;
using Aetherphone.Apps.Games.LuckyDraw;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineLuckyDrawWireContractTests
{
    private const string BoardJson =
        "{\"roundIndex\":2,\"hostUserId\":\"host\",\"players\":["
        + "{\"userId\":\"host\",\"displayName\":\"Ada\",\"seat\":0,\"away\":false,\"wins\":1,\"missed\":0,"
        + "\"state\":0,\"total\":40,\"roundScore\":12,\"bestRound\":28,\"sevens\":1,\"cards\":[5,3]},"
        + "{\"userId\":\"guest\",\"displayName\":\"Bex\",\"seat\":1,\"away\":true,\"wins\":0,\"missed\":2,"
        + "\"state\":1,\"total\":30,\"roundScore\":8,\"bestRound\":15,\"sevens\":0,\"cards\":[8]}],"
        + "\"phase\":1,\"round\":3,\"dealer\":1,\"turnSeat\":0,\"dealing\":false,\"actor\":-1,\"pendingFace\":-1,"
        + "\"forcedSeat\":-1,\"forcedLeft\":0,\"sevenSeat\":-1,\"deckCount\":80,\"discard\":[1,2,13],\"step\":9,"
        + "\"events\":[{\"kind\":\"drew\",\"seat\":0,\"target\":-1,\"face\":3,\"slot\":1,\"otherSlot\":-1,"
        + "\"reshuffled\":false},{\"kind\":\"kept\",\"seat\":0,\"target\":-1,\"face\":3,\"slot\":1,"
        + "\"otherSlot\":-1,\"reshuffled\":false},{\"kind\":\"turn\",\"seat\":0,\"target\":-1,\"face\":-1,"
        + "\"slot\":-1,\"otherSlot\":-1,\"reshuffled\":false}],\"turnSeconds\":30,\"breakSeconds\":8,"
        + "\"targetScore\":200,\"actionCount\":41,\"lastSeat\":1,\"lastKind\":\"luckydraw.hit\",\"endKind\":\"\","
        + "\"winnerSeat\":-1}";

    [Fact]
    public void TheKindActionsAndEndingsMatchTheServer()
    {
        Assert.Equal("games.luckydraw", GameRoomWire.LuckyDrawKind);
        Assert.Equal("hit", GameRoomWire.ActionHit);
        Assert.Equal("stay", GameRoomWire.ActionStay);
        Assert.Equal("target", GameRoomWire.ActionTarget);
        Assert.Equal("target", GameRoomWire.LuckyDrawEndTarget);
        Assert.Equal("desertion", GameRoomWire.LuckyDrawEndDesertion);
        Assert.Contains(GameRoomWire.LuckyDrawKind, OnlineGameArt.Kinds);
        Assert.Equal(6, OnlineGameArt.MaxPlayers(GameRoomWire.LuckyDrawKind));
        Assert.Equal("luckydraw", OnlineGameArt.AccentId(GameRoomWire.LuckyDrawKind));
    }

    [Fact]
    public void ATargetActionCarriesTheChosenSeat()
    {
        var request = new GameRoomActionRequest(GameRoomWire.ActionTarget, 41, -1, -1, "client", Target: 2);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.GameRoomActionRequest);

        Assert.Contains("\"action\":\"target\"", json, StringComparison.Ordinal);
        Assert.Contains("\"actionCount\":41", json, StringComparison.Ordinal);
        Assert.Contains("\"target\":2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheServerBoardParsesFieldForField()
    {
        var board = JsonSerializer.Deserialize(BoardJson, AethernetJsonContext.Default.LuckyDrawRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(2, board.RoundIndex);
        Assert.Equal("host", board.HostUserId);
        Assert.Equal(2, board.Players!.Length);
        Assert.Equal(new[] { 5, 3 }, board.Players[0].Cards);
        Assert.Equal(40, board.Players[0].Total);
        Assert.Equal(28, board.Players[0].BestRound);
        Assert.True(board.Players[1].Away);
        Assert.Equal(LuckyDrawWire.SeatStayed, board.Players[1].State);
        Assert.Equal(LuckyDrawWire.PhaseTurn, board.Phase);
        Assert.Equal(3, board.Round);
        Assert.Equal(80, board.DeckCount);
        Assert.Equal(new[] { 1, 2, LuckyCards.Freeze }, board.Discard);
        Assert.Equal(9, board.Step);
        Assert.Equal(3, board.Events!.Length);
        Assert.Equal(LuckyDrawWire.EventDrew, board.Events[0].Kind);
        Assert.Equal(1, board.Events[0].Slot);
        Assert.Equal(30, board.TurnSeconds);
        Assert.Equal(8, board.BreakSeconds);
        Assert.Equal(LuckyDrawBoard.WinTarget, board.TargetScore);
        Assert.Equal(41, board.ActionCount);
        Assert.Equal(-1, board.WinnerSeat);
    }

    [Fact]
    public void TheBoardShapeIsPinned()
    {
        Assert.Equal(new[]
        {
            "RoundIndex", "HostUserId", "Players", "Phase", "Round", "Dealer", "TurnSeat", "Dealing", "Actor",
            "PendingFace", "ForcedSeat", "ForcedLeft", "SevenSeat", "DeckCount", "Discard", "Step", "Events",
            "TurnSeconds", "BreakSeconds", "TargetScore", "ActionCount", "LastSeat", "LastKind", "EndKind",
            "WinnerSeat",
        }, PropertyNames(typeof(LuckyDrawRoomStateDto)));
        Assert.Equal(new[]
        {
            "UserId", "DisplayName", "Seat", "Away", "Wins", "Missed", "State", "Total", "RoundScore", "BestRound",
            "Sevens", "Cards",
        }, PropertyNames(typeof(LuckyDrawPlayerDto)));
        Assert.Equal(new[] { "Kind", "Seat", "Target", "Face", "Slot", "OtherSlot", "Reshuffled" },
            PropertyNames(typeof(LuckyDrawEventDto)));
    }

    [Fact]
    public void TheEventVocabularyMatchesTheServer()
    {
        Assert.Equal(new[]
        {
            "round", "drew", "kept", "bust", "swept", "saved", "seven", "chanceKept", "chanceGiven",
            "chanceDiscarded", "chooseTarget", "frozen", "flipThree", "deferred", "discarded", "stayed", "turn",
            "timeout", "left", "roundOver", "matchOver",
        }, new[]
        {
            LuckyDrawWire.EventRound, LuckyDrawWire.EventDrew, LuckyDrawWire.EventKept, LuckyDrawWire.EventBust,
            LuckyDrawWire.EventSwept, LuckyDrawWire.EventSaved, LuckyDrawWire.EventSeven,
            LuckyDrawWire.EventChanceKept, LuckyDrawWire.EventChanceGiven, LuckyDrawWire.EventChanceDiscarded,
            LuckyDrawWire.EventChooseTarget, LuckyDrawWire.EventFrozen, LuckyDrawWire.EventFlipThree,
            LuckyDrawWire.EventDeferred, LuckyDrawWire.EventDiscarded, LuckyDrawWire.EventStayed,
            LuckyDrawWire.EventTurn, LuckyDrawWire.EventTimeout, LuckyDrawWire.EventLeft,
            LuckyDrawWire.EventRoundOver, LuckyDrawWire.EventMatchOver,
        });
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, new[]
        {
            LuckyDrawWire.PhaseIdle, LuckyDrawWire.PhaseTurn, LuckyDrawWire.PhaseTarget,
            LuckyDrawWire.PhaseRoundOver, LuckyDrawWire.PhaseMatchOver,
        });
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, new[]
        {
            LuckyDrawWire.SeatActive, LuckyDrawWire.SeatStayed, LuckyDrawWire.SeatFrozen, LuckyDrawWire.SeatBusted,
            LuckyDrawWire.SeatOut,
        });
    }

    [Fact]
    public void TheSessionBuildsTheBoardAndItsRoster()
    {
        var snapshot = new GameRoomSnapshotDto("room", GameRoomWire.LuckyDrawKind, 0, GameRoomWire.PhasePlaying, 0,
            2, BoardJson);

        var state = GameRoomSession.Build("room", 1, 4, snapshot);

        Assert.NotNull(state.LuckyDraw);
        Assert.NotNull(state.Roster);
        Assert.Equal("host", state.Roster!.HostUserId);
        Assert.Equal(41, state.Roster.ActionCount);
        Assert.Equal(2, state.Roster.Players.Length);
        Assert.True(state.Roster.Players[1].Away);
        Assert.Equal(1, state.Roster.Players[0].Wins);
    }

    [Fact]
    public void OnlyTheNextStepReplaysAndAnythingElseSnaps()
    {
        Assert.Equal(LuckyReplay.Snap, OnlineLuckyDrawTable.ReplayFor(OnlineLuckyDrawTable.NotObserved, 4));
        Assert.Equal(LuckyReplay.Keep, OnlineLuckyDrawTable.ReplayFor(4, 4));
        Assert.Equal(LuckyReplay.Replay, OnlineLuckyDrawTable.ReplayFor(4, 5));
        Assert.Equal(LuckyReplay.Snap, OnlineLuckyDrawTable.ReplayFor(4, 7));
        Assert.Equal(LuckyReplay.Snap, OnlineLuckyDrawTable.ReplayFor(4, 1));
    }

    [Fact]
    public void ReplayingABustLandsOnTheServerBoard()
    {
        var before = Board(1, 90, Array.Empty<int>(), Player(0, 5, 3), Player(0, 8));
        var after = Board(2, 89, new[] { 5, 3, 5 }, Player(LuckyDrawWire.SeatBusted), Player(0, 8)) with
        {
            Events = new[]
            {
                Event(LuckyDrawWire.EventDrew, 0, face: 5, slot: 2),
                Event(LuckyDrawWire.EventBust, 0, face: 5, slot: 2, otherSlot: 0),
                Event(LuckyDrawWire.EventSwept, 0),
                Event(LuckyDrawWire.EventTurn, 1),
            },
            TurnSeat = 1,
        };

        var model = Replayed(before, after);

        Assert.True(model.Matches(after));
        Assert.Equal(LuckyDrawWire.SeatBusted, model.SeatState(0));
        Assert.Equal(0, model.HandScore(0));
        Assert.Equal(1, model.TurnSeat);
    }

    [Fact]
    public void ReplayingAFlipThreeAndASecondChanceLandsOnTheServerBoard()
    {
        var before = Board(1, 60, Array.Empty<int>(), Player(0, 2, LuckyCards.FlipThree),
            Player(0, 3, LuckyCards.SecondChance), Player(0, 4));
        var after = Board(2, 57, new[] { LuckyCards.FlipThree, 3, LuckyCards.SecondChance },
            Player(0, 2), Player(0, 3, 9, LuckyCards.Freeze), Player(0, 4)) with
        {
            Events = new[]
            {
                Event(LuckyDrawWire.EventFlipThree, 0, target: 1, face: LuckyCards.FlipThree, slot: 1),
                Event(LuckyDrawWire.EventDrew, 1, face: 9, slot: 2),
                Event(LuckyDrawWire.EventKept, 1, face: 9, slot: 2),
                Event(LuckyDrawWire.EventDrew, 1, face: 3, slot: 3),
                Event(LuckyDrawWire.EventSaved, 1, face: 3, slot: 3, otherSlot: 1),
                Event(LuckyDrawWire.EventDrew, 1, face: LuckyCards.Freeze, slot: 2),
                Event(LuckyDrawWire.EventDeferred, 1, face: LuckyCards.Freeze, slot: 2),
                Event(LuckyDrawWire.EventChooseTarget, 1, face: LuckyCards.Freeze, slot: 2),
            },
            Phase = LuckyDrawWire.PhaseTarget,
            Actor = 1,
            PendingFace = LuckyCards.Freeze,
        };

        var model = Replayed(before, after);

        Assert.True(model.Matches(after));
        Assert.Equal(LuckyDrawWire.PhaseTarget, model.Phase);
        Assert.Equal(1, model.Actor);
        Assert.True(model.IsValidTarget(2));
        Assert.Equal(0, model.ForcedLeft);
    }

    [Fact]
    public void ANewDealCollectsTheTableAndAReshuffleRefillsTheDeck()
    {
        var before = Board(5, 0, new[] { 1, 2 }, Player(LuckyDrawWire.SeatStayed, 7), Player(0, 6)) with
        {
            Phase = LuckyDrawWire.PhaseRoundOver,
        };
        var after = Board(6, 3, Array.Empty<int>(), Player(0, 4), Player(0)) with
        {
            Events = new[]
            {
                new LuckyDrawEventDto(LuckyDrawWire.EventRound, 1, 4),
                new LuckyDrawEventDto(LuckyDrawWire.EventDrew, 0, -1, 4, 0, -1, true),
            },
            Round = 4,
            Dealer = 1,
            Dealing = true,
            Phase = LuckyDrawWire.PhaseIdle,
        };

        var model = Replayed(before, after);

        Assert.True(model.Matches(after));
        Assert.Equal(4, model.Round);
        Assert.Equal(1, model.Dealer);
    }

    private static OnlineLuckyDrawModel Replayed(LuckyDrawRoomStateDto before, LuckyDrawRoomStateDto after)
    {
        var model = new OnlineLuckyDrawModel();
        model.Load(before);
        var events = after.Events!;
        for (var index = 0; index < events.Length; index++)
        {
            model.Apply(events[index], after);
        }

        return model;
    }

    private static LuckyDrawRoomStateDto Board(int step, int deckCount, int[] discard,
        params LuckyDrawPlayerDto[] players)
    {
        for (var seat = 0; seat < players.Length; seat++)
        {
            players[seat] = players[seat] with { Seat = seat, UserId = "player" + seat, DisplayName = "P" + seat };
        }

        return new LuckyDrawRoomStateDto(RoundIndex: 1, HostUserId: "player0", Players: players,
            Phase: LuckyDrawWire.PhaseTurn, Round: 1, Dealer: players.Length - 1, TurnSeat: 0, DeckCount: deckCount,
            Discard: discard, Step: step, Events: Array.Empty<LuckyDrawEventDto>());
    }

    private static LuckyDrawPlayerDto Player(int state, params int[] cards)
    {
        return new LuckyDrawPlayerDto(State: state, Cards: cards);
    }

    private static LuckyDrawEventDto Event(string kind, int seat, int target = -1, int face = -1, int slot = -1,
        int otherSlot = -1)
    {
        return new LuckyDrawEventDto(kind, seat, target, face, slot, otherSlot);
    }

    private static string[] PropertyNames(Type type)
    {
        var properties = type.GetProperties();
        var names = new string[properties.Length];
        for (var index = 0; index < properties.Length; index++)
        {
            names[index] = properties[index].Name;
        }

        return names;
    }
}
