using System.Numerics;
using System.Text.Json;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.MiniGolf;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineMiniGolfWireContractTests
{
    private const string Room = "room-green";
    private const uint CourseFingerprint = 0x1520921Bu;

    private const string RoundState = """
    {
      "roundIndex": 3,
      "hostUserId": "host",
      "players": [
        {
          "userId": "host", "displayName": "Ama", "seat": 0, "away": false, "left": false, "inRound": true,
          "wins": 2, "missed": 0, "strokes": [2, 3, 0, 0, 0, 0, 0, 0, 0], "total": 5, "place": 0
        },
        {
          "userId": "guest", "displayName": "Bo", "seat": 1, "away": true, "left": true, "inRound": true,
          "wins": 0, "missed": 1, "strokes": [4, 0, 0, 0, 0, 0, 0, 0, 0], "total": 4, "place": 0
        },
        {
          "userId": "late", "displayName": "Cy", "seat": 2, "away": false, "left": false, "inRound": false,
          "wins": 0, "missed": 0, "strokes": [], "total": 0, "place": 0
        }
      ],
      "holes": 9,
      "hole": 1,
      "turnSeat": 1,
      "holeStrokes": 0,
      "ballX": 3,
      "ballY": 10,
      "millEpochUnixMs": 1800000000000,
      "lastShot": {
        "seat": 0, "hole": 1, "strokes": 3, "clockMs": 61250,
        "path": [3000, 10000, 3000, 9800, 3000, 9600, 3000, 9500],
        "marks": [2, 2, 64, 3, 9, 0],
        "result": "holed", "pickedUp": false, "timed": true, "restX": 3, "restY": 2
      },
      "shotCount": 5,
      "turnSeconds": 30,
      "actionCount": 17,
      "lastSeat": 0,
      "lastKind": "minigolf.timeout",
      "endKind": "",
      "winnerSeat": -1
    }
    """;

    [Fact]
    public void TheWireNamesMatchTheServer()
    {
        Assert.Equal("games.minigolf", GameRoomWire.MiniGolfKind);
        Assert.Equal("shoot", GameRoomWire.ActionShoot);
        Assert.Equal("start", GameRoomWire.ActionStart);
        Assert.Equal("course", GameRoomWire.MiniGolfEndCourse);
        Assert.Equal("desertion", GameRoomWire.MiniGolfEndDesertion);
        Assert.Equal("rest", GameRoomWire.MiniGolfResultRest);
        Assert.Equal("holed", GameRoomWire.MiniGolfResultHoled);
        Assert.Equal("water", GameRoomWire.MiniGolfResultWater);
        Assert.Equal("out", GameRoomWire.MiniGolfResultOut);
        Assert.Equal("picked", GameRoomWire.MiniGolfResultPicked);
        Assert.Equal(1, GameRoomWire.MiniGolfMarkWall);
        Assert.Equal(2, GameRoomWire.MiniGolfMarkPost);
        Assert.Equal(3, GameRoomWire.MiniGolfMarkMill);
        Assert.Equal(4, GameRoomWire.MiniGolfMarkSand);
        Assert.Equal(5, GameRoomWire.MiniGolfMarkTunnel);
        Assert.Equal(6, GameRoomWire.MiniGolfMarkSplash);
        Assert.Equal(7, GameRoomWire.MiniGolfMarkOut);
        Assert.Equal(8, GameRoomWire.MiniGolfMarkLipOut);
        Assert.Equal(9, GameRoomWire.MiniGolfMarkDrop);
        Assert.Equal(1000, GameRoomWire.MiniGolfPathScale);
        Assert.Equal(30, GameRoomWire.MiniGolfSamplesPerSecond);
        Assert.Equal(MiniGolfBoard.MaxStrokes, GameRoomWire.MiniGolfMaxStrokes);
    }

    [Fact]
    public void TheCourseMatchesTheServerCopy()
    {
        var hash = 2166136261u;
        for (var index = 0; index < MiniGolfCourse.Sources.Length; index++)
        {
            var source = MiniGolfCourse.Sources[index];
            for (var character = 0; character < source.Length; character++)
            {
                hash = (hash ^ source[character]) * 16777619u;
            }

            hash = (hash ^ '\n') * 16777619u;
        }

        Assert.Equal(CourseFingerprint, hash);
        Assert.Equal(MiniGolfCourse.HoleCount, MiniGolfCourse.Sources.Length);
    }

    [Fact]
    public void TheBroadcastReadsTheShapeTheServerSends()
    {
        var board = JsonSerializer.Deserialize(RoundState, AethernetJsonContext.Default.MiniGolfRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(3, board!.RoundIndex);
        Assert.Equal(9, board.Holes);
        Assert.Equal(1, board.Hole);
        Assert.Equal(1, board.TurnSeat);
        Assert.Equal(new Vector2(3f, 10f), new Vector2(board.BallX, board.BallY));
        Assert.Equal(1_800_000_000_000, board.MillEpochUnixMs);
        Assert.Equal(5, board.ShotCount);
        Assert.Equal(30, board.TurnSeconds);
        Assert.Equal(17, board.ActionCount);
        var guest = board.Players![1];
        Assert.True(guest.Left);
        Assert.True(guest.Away);
        Assert.Equal(4, guest.Total);
        Assert.False(board.Players[2].InRound);
        Assert.Equal(9, board.Players[0].Strokes!.Length);
        var shot = board.LastShot!;
        Assert.Equal(GameRoomWire.MiniGolfResultHoled, shot.Result);
        Assert.Equal(61_250, shot.ClockMs);
        Assert.Equal(8, shot.Path!.Length);
        Assert.Equal(new[] { 2, 2, 64, 3, 9, 0 }, shot.Marks);
        Assert.True(shot.Timed);
        Assert.False(shot.PickedUp);
        Assert.True(OnlineMiniGolfTable.IsLive(board));
    }

    [Fact]
    public void ASnapshotBuildsTheBoardAndTheRoster()
    {
        var snapshot = new GameRoomSnapshotDto(Room, GameRoomWire.MiniGolfKind, 0, GameRoomWire.PhasePlaying, 0, 3,
            RoundState);

        var state = GameRoomSession.Build(Room, 2, 30, snapshot);

        Assert.NotNull(state.MiniGolf);
        Assert.Null(state.LuckyDraw);
        Assert.Null(state.Pool);
        var roster = state.Roster!;
        Assert.Equal("host", roster.HostUserId);
        Assert.Equal(17, roster.ActionCount);
        Assert.Equal(3, roster.Players.Length);
        Assert.True(roster.Players[1].Away);
        Assert.Equal(2, roster.Players[0].Wins);
    }

    [Fact]
    public void TheStartCarriesTheHoleCountAndAStrokeItsAngleAndPower()
    {
        var start = new GameRoomActionRequest(GameRoomWire.ActionStart, 4, MiniGolfCourse.FrontNine, -1, "a");
        var startJson = JsonSerializer.Serialize(start, AethernetJsonContext.Default.GameRoomActionRequest);
        Assert.Contains("\"action\":\"start\"", startJson);
        Assert.Contains("\"card\":9", startJson);

        var shoot = new GameRoomActionRequest(GameRoomWire.ActionShoot, 17, -1, -1, "b", Angle: -1.5f, Power: 0.25f);
        var shootJson = JsonSerializer.Serialize(shoot, AethernetJsonContext.Default.GameRoomActionRequest);
        Assert.Contains("\"action\":\"shoot\"", shootJson);
        Assert.Contains("\"actionCount\":17", shootJson);
        Assert.Contains("\"angle\":-1.5", shootJson);
        Assert.Contains("\"power\":0.25", shootJson);
    }

    [Fact]
    public void TheReplayWalksTheSamplesAndFiresEveryMarkOnce()
    {
        var board = JsonSerializer.Deserialize(RoundState, AethernetJsonContext.Default.MiniGolfRoomStateDto)!;
        var replay = new OnlineMiniGolfReplay();
        replay.Begin(board.LastShot!);
        var hole = MiniGolfCourse.Get(1);

        Assert.True(replay.Active);
        Assert.Equal(4, replay.SampleCount);
        Assert.Equal(new Vector2(3f, 10f), replay.Position(hole));
        Assert.Equal(61.25d, replay.ClockSeconds, 3);
        Assert.False(replay.TakeMark(out _, out _, out _));

        replay.Advance(1.5f / GameRoomWire.MiniGolfSamplesPerSecond);
        Assert.Equal(9.7f, replay.Position(hole).Y, 3);
        Assert.Equal(6f, replay.Speed(), 3);
        Assert.False(replay.TakeMark(out _, out _, out _));

        replay.Advance(1f / GameRoomWire.MiniGolfSamplesPerSecond);
        Assert.True(replay.TakeMark(out var kind, out var value, out var sample));
        Assert.Equal(GameRoomWire.MiniGolfMarkPost, kind);
        Assert.Equal(64, value);
        Assert.Equal(2, sample);
        Assert.False(replay.TakeMark(out _, out _, out _));
        Assert.False(replay.Finished);

        replay.Advance(1f);
        Assert.True(replay.Finished);
        Assert.True(replay.TakeMark(out kind, out _, out _));
        Assert.Equal(GameRoomWire.MiniGolfMarkDrop, kind);
        Assert.Equal(new Vector2(3f, 9.5f), replay.Last);
        Assert.Equal(61.25d + replay.DurationSeconds, replay.ClockSeconds, 3);
    }

    [Fact]
    public void ATunnelReplaysThroughItsMouthsInsteadOfAcrossTheGreen()
    {
        var hole = MiniGolfCourse.Get(7);
        var tunnel = hole.Tunnels[0];
        var shot = new MiniGolfShotDto(0, 7, 1, 0, new[] { 3000, 7700, 3000, 4400 },
            new[] { 1, GameRoomWire.MiniGolfMarkTunnel, 0 }, GameRoomWire.MiniGolfResultRest);
        var replay = new OnlineMiniGolfReplay();
        replay.Begin(shot);

        replay.Advance(0.25f / GameRoomWire.MiniGolfSamplesPerSecond);
        var inbound = replay.Position(hole);
        Assert.True(Vector2.Distance(inbound, tunnel.Entry) < Vector2.Distance(replay.Sample(0), tunnel.Entry));
        Assert.Equal(0f, replay.Speed());

        replay.Advance(0.5f / GameRoomWire.MiniGolfSamplesPerSecond);
        var outbound = replay.Position(hole);
        Assert.True(Vector2.Distance(outbound, tunnel.Exit) < 0.2f);
    }

    [Fact]
    public void MarksMapOntoTheOfflineJuiceEvents()
    {
        Assert.Equal(GolfEvents.Wall, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkWall));
        Assert.Equal(GolfEvents.Post, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkPost));
        Assert.Equal(GolfEvents.Mill, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkMill));
        Assert.Equal(GolfEvents.Sand, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkSand));
        Assert.Equal(GolfEvents.Tunnel, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkTunnel));
        Assert.Equal(GolfEvents.Splash, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkSplash));
        Assert.Equal(GolfEvents.Reset, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkOut));
        Assert.Equal(GolfEvents.LipOut, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkLipOut));
        Assert.Equal(GolfEvents.Drop, OnlineMiniGolfReplay.EventOf(GameRoomWire.MiniGolfMarkDrop));
        Assert.Equal(GolfEvents.None, OnlineMiniGolfReplay.EventOf(0));
    }

    [Fact]
    public void AnEmptyStrokeIsALiftWithNothingToReplay()
    {
        var replay = new OnlineMiniGolfReplay();
        replay.Begin(new MiniGolfShotDto(1, 2, GameRoomWire.MiniGolfMaxStrokes, 0, null, null,
            GameRoomWire.MiniGolfResultPicked, true, true));

        Assert.False(replay.Active);
        Assert.Equal(Vector2.Zero, replay.Position(MiniGolfCourse.Get(2)));
        Assert.False(replay.TakeMark(out _, out _, out _));
    }

    [Fact]
    public void OnlyTheNextStrokeOfTheRoundReplays()
    {
        var board = JsonSerializer.Deserialize(RoundState, AethernetJsonContext.Default.MiniGolfRoomStateDto)!;

        Assert.True(OnlineMiniGolfTable.FollowsSeen(4, board));
        Assert.False(OnlineMiniGolfTable.FollowsSeen(3, board));
        Assert.False(OnlineMiniGolfTable.FollowsSeen(5, board));
        Assert.False(OnlineMiniGolfTable.FollowsSeen(4, board with { LastShot = null }));
        Assert.False(OnlineMiniGolfTable.FollowsSeen(4, board with { LastShot = board.LastShot! with { Seat = -1 } }));
        Assert.False(OnlineMiniGolfTable.IsLive(board with { EndKind = GameRoomWire.MiniGolfEndCourse }));
        Assert.False(OnlineMiniGolfTable.IsLive(board with { TurnSeat = -1 }));
    }

    [Fact]
    public void TheScorecardRowsAreTheSeatsDealtIntoTheRound()
    {
        var board = JsonSerializer.Deserialize(RoundState, AethernetJsonContext.Default.MiniGolfRoomStateDto)!;
        Span<int> seats = stackalloc int[MiniGolfRound.MaxPlayers];

        var rows = OnlineMiniGolfTable.Rows(board.Players!, seats);

        Assert.Equal(2, rows);
        Assert.Equal(0, seats[0]);
        Assert.Equal(1, seats[1]);
    }

    [Fact]
    public void TheMillsFollowTheServerClock()
    {
        var mill = MiniGolfCourse.Get(6).Mills[0];

        Assert.Equal(0f, OnlineMiniGolfTable.MillAngle(mill, 0d));
        Assert.Equal(mill.Speed * 2f, OnlineMiniGolfTable.MillAngle(mill, 2d), 4);
        Assert.InRange(OnlineMiniGolfTable.MillAngle(mill, 10_000d), -MathF.PI * 2f, MathF.PI * 2f);
    }

    [Fact]
    public void TheHubListsMiniGolfAsAFourSeatRoom()
    {
        Assert.Contains(GameRoomWire.MiniGolfKind, OnlineGameArt.Kinds);
        Assert.Equal("minigolf", OnlineGameArt.AccentId(GameRoomWire.MiniGolfKind));
        Assert.Equal(4, OnlineGameArt.MaxPlayers(GameRoomWire.MiniGolfKind));
        Assert.Equal("online.minigolf", GamesLibrary.OnlineEntryId(GameRoomWire.MiniGolfKind));
        Assert.Equal(L.MiniGolf.Title.Key, GamesOnlineText.GameName(GameRoomWire.MiniGolfKind).Key);
    }
}
