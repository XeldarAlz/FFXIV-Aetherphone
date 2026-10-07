using System.Text.Json;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineBroadsideWireContractTests
{
    private const string Room = "room-sky";

    private const string BattleState = """
    {
      "roundIndex": 2,
      "hostUserId": "host",
      "players": [
        {
          "userId": "host", "displayName": "Ama", "seat": 0, "away": false, "wins": 1, "missed": 0,
          "ready": true, "marks": [2, 1, 0, 0, 0, 0, 0, 0, 0, 0], "sunk": []
        },
        {
          "userId": "guest", "displayName": "Bo", "seat": 1, "away": true, "wins": 0, "missed": 1,
          "ready": true, "marks": [], "sunk": [{ "ship": 4, "cell": 5, "across": false }]
        }
      ],
      "placing": false,
      "turnSeat": 1,
      "turnSeconds": 30,
      "lastSeat": 0,
      "lastCell": 15,
      "lastResult": "sunk",
      "lastShip": 4,
      "shotCount": 9,
      "actionCount": 14,
      "lastKind": "broadside.fire",
      "endKind": "",
      "winnerSeat": -1
    }
    """;

    [Fact]
    public void TheWireNamesMatchTheServer()
    {
        Assert.Equal("games.broadside", GameRoomWire.BroadsideKind);
        Assert.Equal("place", GameRoomWire.ActionPlace);
        Assert.Equal("fire", GameRoomWire.ActionFire);
        Assert.Equal("resign", GameRoomWire.ActionResign);
        Assert.Equal("start", GameRoomWire.ActionStart);
        Assert.Equal("broadside.fleet", GameRoomWire.BroadsideFleetEvent);
        Assert.Equal("miss", GameRoomWire.BroadsideResultMiss);
        Assert.Equal("hit", GameRoomWire.BroadsideResultHit);
        Assert.Equal("sunk", GameRoomWire.BroadsideResultSunk);
        Assert.Equal("fleet", GameRoomWire.BroadsideEndFleet);
        Assert.Equal("resign", GameRoomWire.BroadsideEndResign);
        Assert.Equal("desertion", GameRoomWire.BroadsideEndDesertion);
        Assert.Equal("timeout", GameRoomWire.BroadsideEndTimeout);
        Assert.Equal(1, GameRoomWire.BroadsideMarkMiss);
        Assert.Equal(2, GameRoomWire.BroadsideMarkHit);
        Assert.Equal(100, GameRoomWire.BroadsideCellCount);
        Assert.Equal(5, GameRoomWire.BroadsideShipCount);
    }

    [Fact]
    public void TheBroadcastReadsTheShapeTheServerSends()
    {
        var board = JsonSerializer.Deserialize(BattleState, AethernetJsonContext.Default.BroadsideRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(2, board!.RoundIndex);
        Assert.False(board.Placing);
        Assert.Equal(1, board.TurnSeat);
        Assert.Equal(30, board.TurnSeconds);
        Assert.Equal(15, board.LastCell);
        Assert.Equal(GameRoomWire.BroadsideResultSunk, board.LastResult);
        Assert.Equal(4, board.LastShip);
        Assert.Equal(9, board.ShotCount);
        Assert.Equal(14, board.ActionCount);
        var guest = board.Players![1];
        Assert.True(guest.Away);
        Assert.Equal(1, guest.Missed);
        Assert.True(guest.Ready);
        Assert.Equal(new BroadsideShipDto(4, 5, false), Assert.Single(guest.Sunk!));
        Assert.Equal(GameRoomWire.BroadsideMarkHit, board.Players[0].Marks![0]);
    }

    [Fact]
    public void ASnapshotBuildsTheBoardAndTheRoster()
    {
        var snapshot = new GameRoomSnapshotDto(Room, GameRoomWire.BroadsideKind, 0, GameRoomWire.PhasePlaying,
            0, 2, BattleState);

        var state = GameRoomSession.Build(Room, 3, 40, snapshot);

        Assert.NotNull(state.Broadside);
        Assert.Null(state.Uno);
        Assert.Null(state.ConnectFour);
        var roster = state.Roster;
        Assert.NotNull(roster);
        Assert.Equal("host", roster!.HostUserId);
        Assert.Equal(14, roster.ActionCount);
        Assert.Equal(-1, roster.WinnerSeat);
        Assert.Equal(2, roster.Players.Length);
        Assert.Equal("Bo", roster.Players[1].DisplayName);
        Assert.True(roster.Players[1].Away);
    }

    [Fact]
    public void TheFleetRidesOnlyItsOwnPrivateLane()
    {
        const string payload = """
        {"seat":1,"ships":[{"ship":0,"cell":9,"across":false},{"ship":4,"cell":40,"across":true}],"actionCount":14}
        """;

        var fleet = GameRoomSession.BuildBroadsidePrivate(new GamePrivateDto(GameRoomWire.BroadsideFleetEvent, payload));

        Assert.NotNull(fleet);
        Assert.Equal(1, fleet!.Seat);
        Assert.Equal(14, fleet.ActionCount);
        Assert.Equal(new BroadsideShipDto(0, 9, false), fleet.Ships![0]);
        Assert.Equal(new BroadsideShipDto(4, 40, true), fleet.Ships[1]);
        Assert.Null(GameRoomSession.BuildPrivate(new GamePrivateDto(GameRoomWire.BroadsideFleetEvent, payload)));
        Assert.Null(GameRoomSession.BuildBroadsidePrivate(new GamePrivateDto(GameRoomWire.UnoHandEvent, payload)));
        Assert.Equal(14, new GameRoomPrivate(Room, 3, 40, null, fleet).ActionCount);
    }

    [Fact]
    public void ActionsCarryTheFieldsTheServerReads()
    {
        var fire = new GameRoomActionRequest(GameRoomWire.ActionFire, 14, -1, -1, "a", Cell: 42);
        var fireJson = JsonSerializer.Serialize(fire, AethernetJsonContext.Default.GameRoomActionRequest);
        Assert.Contains("\"action\":\"fire\"", fireJson);
        Assert.Contains("\"actionCount\":14", fireJson);
        Assert.Contains("\"cell\":42", fireJson);

        var place = new GameRoomActionRequest(GameRoomWire.ActionPlace, 3, -1, -1, "b",
            Fleet: new[] { new BroadsideShipDto(0, 0, true), new BroadsideShipDto(1, 10, false) });
        var placeJson = JsonSerializer.Serialize(place, AethernetJsonContext.Default.GameRoomActionRequest);
        Assert.Contains("\"action\":\"place\"", placeJson);
        Assert.Contains("\"fleet\":[{\"ship\":0,\"cell\":0,\"across\":true},{\"ship\":1,\"cell\":10,\"across\":false}]",
            placeJson);
    }

    [Fact]
    public void OnlyTheNextShotOfTheSameRoundAnimates()
    {
        var board = new BroadsideRoomStateDto(RoundIndex: 2, ShotCount: 9, LastSeat: 0, LastCell: 15);

        Assert.True(OnlineBroadsideTable.IsNextShot(2, 8, board));
        Assert.False(OnlineBroadsideTable.IsNextShot(2, 7, board));
        Assert.False(OnlineBroadsideTable.IsNextShot(2, 9, board));
        Assert.False(OnlineBroadsideTable.IsNextShot(1, 8, board));
        Assert.False(OnlineBroadsideTable.IsNextShot(2, 8, board with { LastCell = 100 }));
        Assert.False(OnlineBroadsideTable.IsNextShot(2, 8, board with { LastSeat = -1 }));
    }

    [Fact]
    public void ARoundIsLiveWhilePlacingOrFiringAndNotOnceItEnds()
    {
        Assert.True(OnlineBroadsideTable.IsLive(new BroadsideRoomStateDto(Placing: true)));
        Assert.True(OnlineBroadsideTable.IsLive(new BroadsideRoomStateDto(TurnSeat: 1)));
        Assert.False(OnlineBroadsideTable.IsLive(new BroadsideRoomStateDto()));
        Assert.False(OnlineBroadsideTable.IsLive(new BroadsideRoomStateDto(TurnSeat: 1,
            EndKind: GameRoomWire.BroadsideEndFleet)));
    }

    [Fact]
    public void MarksReadSafelyFromShortOrMissingArrays()
    {
        var board = JsonSerializer.Deserialize(BattleState, AethernetJsonContext.Default.BroadsideRoomStateDto)!;
        var players = board.Players!;

        Assert.Equal(0, OnlineBroadsideTable.MarkOf(players, 0, 0));
        Assert.Equal(0, OnlineBroadsideTable.MarkOf(players, 1, 0));
        Assert.Equal(0, OnlineBroadsideTable.MarkOf(players, 2, 0));
        Assert.Equal(0, OnlineBroadsideTable.MarkOf(players, 0, 100));

        var full = new int[GameRoomWire.BroadsideCellCount];
        full[42] = GameRoomWire.BroadsideMarkMiss;
        var filled = new[] { players[0] with { Marks = full } };
        Assert.Equal(GameRoomWire.BroadsideMarkMiss, OnlineBroadsideTable.MarkOf(filled, 0, 42));
    }

    [Fact]
    public void TheHubListsBroadsideAsATwoSeatRoom()
    {
        Assert.Contains(GameRoomWire.BroadsideKind, OnlineGameArt.Kinds);
        Assert.Equal("broadside", OnlineGameArt.AccentId(GameRoomWire.BroadsideKind));
        Assert.Equal(2, OnlineGameArt.MaxPlayers(GameRoomWire.BroadsideKind));
        Assert.Equal("online.broadside", GamesLibrary.OnlineEntryId(GameRoomWire.BroadsideKind));
        Assert.Equal(L.Broadside.Title.Key, GamesOnlineText.GameName(GameRoomWire.BroadsideKind).Key);
    }
}
