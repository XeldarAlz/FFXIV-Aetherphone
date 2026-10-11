using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoTableWireContractTests
{
    [Fact]
    public void TheDirectoryReadsTheShapeTheServerActuallySends()
    {
        const string json = """
        {
          "tables": [
            {
              "tableId": "blackjack-pit",
              "gameKind": "casino.blackjack",
              "kind": 1,
              "stakeTier": 0,
              "ownerUserId": "",
              "ownerName": "",
              "minBet": 5,
              "maxBet": 25,
              "minBuyIn": 100,
              "maxBuyIn": 2000,
              "maxSeats": 6,
              "seatedCount": 3,
              "occupancy": 7,
              "admitted": true,
              "reason": "",
              "inviteToken": "[aep.casino.v1:blackjack-pit]"
            }
          ],
          "serverNowUnixMs": 1749999999000
        }
        """;

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableListDto);
        Assert.NotNull(directory);
        var row = Assert.Single(directory!.Tables!);
        Assert.Equal("blackjack-pit", row.TableId);
        Assert.Equal(CasinoWire.BlackjackKind, row.GameKind);
        Assert.Equal(6, row.MaxSeats);
        Assert.Equal(3, row.SeatedCount);
        Assert.Equal(100, row.MinBuyIn);
        Assert.Equal(2000, row.MaxBuyIn);
        Assert.True(row.Admitted);
        Assert.Equal(1749999999000, directory.ServerNowUnixMs);

        Assert.True(CasinoTableFilters.HasOpenSeat(row));
        Assert.Equal(4, CasinoTableFilters.SpectatorsOf(row));
        Assert.False(CasinoTableFilters.IsPrivate(row));
    }

    [Fact]
    public void AFullTableIsOnlyOneWhoseSeatsAreActuallyTaken()
    {
        var open = new CasinoTableRowDto(TableId: "t", MaxSeats: 6, SeatedCount: 3);
        var full = new CasinoTableRowDto(TableId: "t", MaxSeats: 6, SeatedCount: 6);
        var unseeded = new CasinoTableRowDto(TableId: "t");

        Assert.True(CasinoTableFilters.HasOpenSeat(open));
        Assert.False(CasinoTableFilters.HasOpenSeat(full));
        Assert.False(CasinoTableFilters.HasOpenSeat(unseeded));
    }

    [Fact]
    public void QuickSeatAnswersWithTheTableAndTheBuyIn()
    {
        const string json = """
        {
          "granted": true,
          "roomId": "blackjack-pit",
          "name": "Emerald room",
          "minBuyIn": 100,
          "maxBuyIn": 2000,
          "suggestedBuyIn": 500,
          "minBet": 10,
          "maxBet": 500,
          "seatIndex": 2
        }
        """;

        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoQuickSeatDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal("blackjack-pit", answer.RoomId);
        Assert.Equal(500, answer.SuggestedBuyIn);
        Assert.Equal(2, answer.SeatIndex);
    }

    [Fact]
    public void ARefusedQuickSeatNamesItsReasonAndTheClientKnowsThatOne()
    {
        const string json = """{"granted":false,"reason":"no_tables"}""";
        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoQuickSeatDto);
        Assert.NotNull(answer);
        Assert.False(answer!.Granted);
        Assert.True(CasinoReasons.TryMessage(answer.Reason, out _));
    }

    [Fact]
    public void SittingAnswersWithTheRackTheTableOpened()
    {
        const string json = """
        {
          "granted": true,
          "reason": "",
          "roomId": "blackjack-pit",
          "seatIndex": 2,
          "sitting": {
            "id": "rack-1",
            "tableId": "blackjack-pit",
            "gameKind": "casino.blackjack",
            "state": 1,
            "stack": 480,
            "chipsIn": 500,
            "chipsOut": 0
          },
          "balance": 1480
        }
        """;

        var answer = JsonSerializer.Deserialize(json,
            AethernetJsonContext.Default.CasinoBlackjackSeatResultDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal(2, answer.SeatIndex);
        Assert.Equal("rack-1", answer.Sitting!.Id);
        Assert.Equal(480, answer.Sitting.Stack);
        Assert.Equal(1480, answer.Balance);
    }

    [Fact]
    public void StandingMidHandComesBackQueuedRatherThanRefused()
    {
        const string json =
            """{"granted":true,"reason":"at_hand_end","roomId":"blackjack-pit","seatIndex":2,"balance":0}""";
        var answer = JsonSerializer.Deserialize(json,
            AethernetJsonContext.Default.CasinoBlackjackSeatResultDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal(CasinoReasons.AtHandEnd, answer.Reason);
        Assert.True(CasinoReasons.TryMessage(answer.Reason, out _));
    }

    [Fact]
    public void ABetAndAPlayComeBackWithTheCountTheNextMoveHasToQuote()
    {
        const string json = """
        {
          "granted": true,
          "reason": "",
          "roomId": "blackjack-pit",
          "handId": "hand-12",
          "seatIndex": 2,
          "actionCount": 7,
          "stack": 480
        }
        """;

        var answer = JsonSerializer.Deserialize(json,
            AethernetJsonContext.Default.CasinoBlackjackActionResultDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal("hand-12", answer.HandId);
        Assert.Equal(7, answer.ActionCount);
        Assert.Equal(480, answer.Stack);
    }

    [Fact]
    public void TheDoorCarriesKnocksAndSeatsButNeverACrowd()
    {
        const string json = """
        {
          "roomId": "private-4f2a",
          "owner": true,
          "inviteToken": "[aep.casino.v1:private-4f2a]",
          "knocks": [{"userId":"u1","displayName":"Tataru","createdAtUnixMs":1750000000000}],
          "seated": [{"userId":"u2","displayName":"Hildibrand","seatIndex":4}],
          "serverNowUnixMs": 1750000001000
        }
        """;

        var door = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableDoorDto);
        Assert.NotNull(door);
        Assert.True(door!.Owner);
        Assert.Single(door.Knocks!);
        Assert.Equal("Tataru", door.Knocks![0].DisplayName);
        Assert.Equal(1750000000000, door.Knocks![0].CreatedAtUnixMs);
        Assert.Single(door.Seated!);
        Assert.Equal("u2", door.Seated![0].UserId);
        Assert.Equal(4, door.Seated![0].SeatIndex);
    }

    [Fact]
    public void CreatingATableComesBackWithSomethingShareable()
    {
        const string json = """
        {
          "granted": true,
          "reason": "",
          "table": {
            "tableId": "private-4f2a",
            "gameKind": "casino.blackjack",
            "kind": 1,
            "ownerUserId": "u1",
            "ownerName": "Tataru",
            "maxSeats": 6,
            "inviteToken": "[aep.casino.v1:private-4f2a]"
          }
        }
        """;

        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableResultDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal("private-4f2a", answer.Table!.TableId);
        Assert.True(CasinoShare.TryParse(answer.Table.InviteToken, out var parsed));
        Assert.Equal("private-4f2a", parsed);
    }

    [Fact]
    public void TheBlackjackBlobReadsTheShapeTheTableActuallyWrites()
    {
        const string json = """
        {
          "handId": "hand-12",
          "handIndex": 12,
          "phase": 2,
          "commit": "abc",
          "nextCommit": "def",
          "seed": "",
          "dealerCards": [20, -1],
          "dealerTotal": 10,
          "dealerSoft": false,
          "activeSeat": 1,
          "activeHand": 0,
          "actionCount": 7,
          "deadlineUnixMs": 1750000060000,
          "windowSeconds": 25,
          "seats": [
            {
              "seatIndex": 1,
              "userId": "u2",
              "displayName": "Hildibrand",
              "chips": 480,
              "state": 1,
              "connected": true,
              "joinsNextHand": false,
              "leaveAtHandEnd": false,
              "committed": 20,
              "heldUntilUnixMs": 0,
              "hands": [
                {
                  "cards": [-1, -1],
                  "bet": 20,
                  "total": 0,
                  "soft": false,
                  "doubled": false,
                  "stood": false,
                  "busted": false,
                  "natural": false,
                  "outcome": 0,
                  "delta": 0,
                  "splitAces": false
                }
              ]
            }
          ],
          "minBet": 10,
          "maxBet": 500,
          "minBuyIn": 100,
          "maxBuyIn": 2000,
          "maxWin": 500000
        }
        """;

        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal("hand-12", board!.HandId);
        Assert.Equal(12, board.HandIndex);
        Assert.Equal(BlackjackPhases.PlayerTurns, board.Phase);
        Assert.Equal(1, board.ActiveSeat);
        Assert.Equal(0, board.ActiveHand);
        Assert.Equal(7, board.ActionCount);
        Assert.Equal(25, board.WindowSeconds);
        Assert.Equal(1750000060000, board.DeadlineUnixMs);

        var seat = Assert.Single(board.Seats!);
        Assert.Equal("u2", seat.UserId);
        Assert.Equal(480, seat.Chips);
        Assert.Equal(20, seat.Committed);
        Assert.Equal(BlackjackSeatStates.Seated, seat.State);
        Assert.True(seat.Connected);
        Assert.Equal(new[] { -1, -1 }, Assert.Single(seat.Hands!).Cards);
    }

    [Fact]
    public void ATableWithNoLiveHandStillLoads()
    {
        const string json = """{"handId":"","handIndex":3,"phase":0,"seats":[]}""";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal(string.Empty, board!.HandId);
        Assert.Equal(BlackjackPhases.Betting, board.Phase);
        Assert.Equal(-1, board.ActiveSeat);
        Assert.Empty(board.Seats!);
    }

    [Fact]
    public void ATurnAlertGroupsOnItsTableAndTheRouterReadsItBack()
    {
        var notification = new PhoneNotification(CasinoTurnNotifier.AppId, "Your turn", "The table is waiting",
            System.DateTime.Now, default,
            string.Concat(CasinoTurnNotifier.GroupPrefix, CasinoRoomIds.BlackjackPit));
        Assert.Equal("casino:blackjack-pit", notification.GroupKey);
        Assert.Equal("casino:blackjack-pit", notification.StackKey);
        Assert.Equal("casino", notification.SettingsKey);

        var launcher = new CasinoLauncher();
        launcher.RequestTable(notification.GroupKey![CasinoTurnNotifier.GroupPrefix.Length..]);
        Assert.True(launcher.TryConsume(out var launch));
        Assert.Equal(CasinoLaunchKind.Table, launch.Kind);
        Assert.Equal(CasinoRoomIds.BlackjackPit, launch.TableId);
        Assert.False(launcher.TryConsume(out _));
    }

    [Fact]
    public void AWidgetTapAsksForItsGameAndTheLauncherHandsItOverOnce()
    {
        var launcher = new CasinoLauncher();
        launcher.RequestGame(string.Empty);
        Assert.False(launcher.HasPending);

        launcher.RequestGame("dailyspin");
        Assert.True(launcher.HasPending);
        Assert.True(launcher.TryConsume(out var launch));
        Assert.Equal(CasinoLaunchKind.Game, launch.Kind);
        Assert.Equal("dailyspin", launch.GameId);
        Assert.False(launcher.HasPending);
        Assert.False(launcher.TryConsume(out _));
    }

    [Fact]
    public void OneTurnKeyPerHandPerSplitSoAResyncCannotRingTwice()
    {
        var first = CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 0);
        Assert.Equal(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 0));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 1));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-13", 1, 0));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 2, 0));
    }

    [Fact]
    public void ATurnAlertStaysQuietWhileTheTableIsBeingWatched()
    {
        Assert.True(CasinoTurnNotifier.Watching(1_000, 1_200));
        Assert.False(CasinoTurnNotifier.Watching(1_000, 9_000));
        Assert.False(CasinoTurnNotifier.Watching(0, 9_000));
    }

    [Fact]
    public void TheHeldSeatCountdownRoundsUpSoItNeverShowsZeroWhileItIsStillHeld()
    {
        Assert.Equal(1, ReconnectVeil.SecondsOf(1));
        Assert.Equal(1, ReconnectVeil.SecondsOf(1_000));
        Assert.Equal(2, ReconnectVeil.SecondsOf(1_001));
        Assert.Equal(0, ReconnectVeil.SecondsOf(0));
    }

    [Fact]
    public void TheHandReadCarriesTheSameVersionPairTheSocketFrameDoes()
    {
        const string json = """
        {
          "roomId": "blackjack-pit",
          "epoch": 3,
          "seq": 41,
          "eventKind": "you.cards",
          "payload": "{\"handId\":\"hand-7\",\"seatIndex\":2,\"activeHand\":0,\"actionCount\":5,\"actionsMask\":3,\"deadlineUnixMs\":1750000060000,\"chips\":480,\"hands\":[{\"cards\":[40,41],\"bet\":20,\"total\":19}]}",
          "serverNowUnixMs": 1750000001000
        }
        """;
        var read = JsonSerializer.Deserialize(json,
            AethernetJsonContext.Default.CasinoBlackjackHandStateDto);
        Assert.NotNull(read);
        Assert.Equal("blackjack-pit", read!.RoomId);
        Assert.Equal(3, read.Epoch);
        Assert.Equal(41, read.Seq);
        Assert.Equal(CasinoWire.BlackjackHandEvent, read.EventKind);

        var mine = CasinoRoomSession.BuildPrivate(new CasinoPrivateDto(read.EventKind, read.Payload));
        Assert.NotNull(mine);
        Assert.Equal("hand-7", mine!.HandId);
        Assert.Equal(2, mine.SeatIndex);
        Assert.Equal(0, mine.ActiveHand);
        Assert.Equal(5, mine.ActionCount);
        Assert.Equal(BlackjackRules.ActionHit | BlackjackRules.ActionStand, mine.ActionsMask);
        Assert.Equal(480, mine.Chips);
        Assert.Equal(new[] { 40, 41 }, Assert.Single(mine.Hands!).Cards);

        Assert.Equal("/casino/blackjack/blackjack-pit/hand",
            Aetherphone.Core.Aethernet.Clients.CasinoClient.BlackjackMyHandPath(CasinoRoomIds.BlackjackPit));
        Assert.Equal("/casino/blackjack/a%20b/hand",
            Aetherphone.Core.Aethernet.Clients.CasinoClient.BlackjackMyHandPath("a b"));
    }

    [Fact]
    public void ABurnedSeatIdIsNeverCarriedToADifferentSeatOrBuyIn()
    {
        Assert.True(CasinoTablesStore.ReusesSeat(2, 500, 2, 500));
        Assert.False(CasinoTablesStore.ReusesSeat(2, 500, 4, 500));
        Assert.False(CasinoTablesStore.ReusesSeat(2, 500, 2, 200));
        Assert.False(CasinoTablesStore.ReusesSeat(-1, -1, 0, 0));

        Assert.True(CasinoTablesStore.ReusesCreate(1, 1));
        Assert.False(CasinoTablesStore.ReusesCreate(1, 2));
        Assert.False(CasinoTablesStore.ReusesCreate(int.MinValue, 0));
    }
}
