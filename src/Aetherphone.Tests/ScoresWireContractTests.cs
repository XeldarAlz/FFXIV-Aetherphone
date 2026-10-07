using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScoresWireContractTests
{
    private static readonly string[] ServerPluginStatIds =
    {
        "2048", "beat", "blade", "breakout", "bubbles", "capman", "coil", "crystaldrop", "flap", "hop",
        "invaders", "match3", "match3.blitz", "simon", "skyfall", "snake", "squadron", "stack", "swoop",
        "tetris", "tetris.modern", "trivia", "updraft", "updraft.height", "whack", "wordrun", "watersort",
        "flow.easy", "flow.medium", "flow.hard", "solitaire", "memory", "memory.attempts",
        "minesweeper.easy", "minesweeper.medium", "minesweeper.hard", "sudoku.easy", "sudoku.medium",
        "sudoku.hard", "nonogram.easy", "nonogram.medium", "nonogram.hard", "chess", "reversi",
        "casino.barkeep",
        "slice", "slice.arcade", "spiral",
        "pinball",
    };

    [Fact]
    public void RoutesMatchTheBackendScoreEndpoints()
    {
        Assert.Equal("/games/scores", ScoresClient.SubmitPath);
        Assert.Equal("/games/scores/me", ScoresClient.MyRanksPath);
        Assert.Equal("/me/games-privacy", ScoresClient.PrivacyPath);
        Assert.Equal("/games/scores/tetris?scope=global&span=all&limit=50",
            ScoresClient.BoardPath("tetris", ScoresClient.ScopeGlobal, ScoresClient.SpanAll, ScoresClient.DefaultLimit));
        Assert.Equal("/games/scores/sudoku.easy?scope=friends&span=week&limit=100",
            ScoresClient.BoardPath("sudoku.easy", ScoresClient.ScopeFriends, ScoresClient.SpanWeek, 100));
        Assert.Equal("global", ScoresClient.ScopeGlobal);
        Assert.Equal("friends", ScoresClient.ScopeFriends);
        Assert.Equal("all", ScoresClient.SpanAll);
        Assert.Equal("week", ScoresClient.SpanWeek);
    }

    [Fact]
    public void SubmitRequestSerializesTheBackendShape()
    {
        var json = JsonSerializer.Serialize(new GameScoreSubmitRequest("tetris", 5000),
            AethernetJsonContext.Default.GameScoreSubmitRequest);
        Assert.Equal("{\"gameId\":\"tetris\",\"value\":5000,\"sessionId\":null}", json);
    }

    [Fact]
    public void PrivacyRequestSerializesBothAValueAndAClear()
    {
        var hidden = JsonSerializer.Serialize(new UpdateGamesPrivacyRequest(false),
            AethernetJsonContext.Default.UpdateGamesPrivacyRequest);
        Assert.Equal("{\"showOnLeaderboards\":false}", hidden);

        var unchanged = JsonSerializer.Serialize(new UpdateGamesPrivacyRequest(null),
            AethernetJsonContext.Default.UpdateGamesPrivacyRequest);
        Assert.Equal("{\"showOnLeaderboards\":null}", unchanged);
    }

    [Fact]
    public void SubmitReplyDeserializesAcceptedAndRefused()
    {
        const string acceptedJson = "{\"accepted\":true,\"reason\":\"\",\"best\":5000,\"rank\":1,\"total\":1,"
            + "\"friendsRank\":1,\"weekRank\":1}";
        var accepted = JsonSerializer.Deserialize(acceptedJson, AethernetJsonContext.Default.GameScoreSubmitDto);
        Assert.NotNull(accepted);
        Assert.True(accepted.Accepted);
        Assert.Equal(ScoreReasons.Accepted, accepted.Reason);
        Assert.Equal(5000, accepted.Best);
        Assert.Equal(1, accepted.Rank);
        Assert.Equal(1, accepted.Total);
        Assert.Equal(1, accepted.FriendsRank);
        Assert.Equal(1, accepted.WeekRank);

        const string refusedJson = "{\"accepted\":false,\"reason\":\"too_soon\",\"best\":0,\"rank\":0,\"total\":0,"
            + "\"friendsRank\":0,\"weekRank\":0}";
        var refused = JsonSerializer.Deserialize(refusedJson, AethernetJsonContext.Default.GameScoreSubmitDto);
        Assert.NotNull(refused);
        Assert.False(refused.Accepted);
        Assert.Equal(ScoreReasons.TooSoon, refused.Reason);
        Assert.Equal(0, refused.Rank);
    }

    [Fact]
    public void ReasonStringsMatchTheBackend()
    {
        Assert.Equal("", ScoreReasons.Accepted);
        Assert.Equal("unknown_game", ScoreReasons.UnknownGame);
        Assert.Equal("implausible", ScoreReasons.Implausible);
        Assert.Equal("too_soon", ScoreReasons.TooSoon);
        Assert.Equal("not_better", ScoreReasons.NotBetter);
        Assert.Equal("hidden", ScoreReasons.Hidden);
    }

    [Fact]
    public void BoardPayloadDeserializesWithEntriesAndMe()
    {
        const string json = "{\"gameId\":\"tetris\",\"scope\":\"global\",\"span\":\"all\","
            + "\"entries\":[{\"rank\":1,\"userId\":\"u1\",\"displayName\":\"Ada\",\"handle\":\"ada\","
            + "\"avatarUrl\":null,\"badges\":3,\"value\":5000,\"achievedAtUnix\":1791234567}],"
            + "\"me\":{\"rank\":1,\"value\":5000,\"total\":1}}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.GameLeaderboardDto);
        Assert.NotNull(board);
        Assert.Equal("tetris", board.GameId);
        Assert.Equal("global", board.Scope);
        Assert.Equal("all", board.Span);
        Assert.NotNull(board.Entries);
        Assert.Single(board.Entries);
        var entry = board.Entries[0];
        Assert.Equal(1, entry.Rank);
        Assert.Equal("u1", entry.UserId);
        Assert.Equal("Ada", entry.DisplayName);
        Assert.Equal("ada", entry.Handle);
        Assert.Null(entry.AvatarUrl);
        Assert.Equal(3, entry.Badges);
        Assert.Equal(5000, entry.Value);
        Assert.Equal(1791234567L, entry.AchievedAtUnix);
        Assert.NotNull(board.Me);
        Assert.Equal(1, board.Me.Rank);
        Assert.Equal(5000, board.Me.Value);
        Assert.Equal(1, board.Me.Total);
    }

    [Fact]
    public void BoardPayloadDeserializesWithoutMe()
    {
        const string json = "{\"gameId\":\"snake\",\"scope\":\"friends\",\"span\":\"week\",\"entries\":[],\"me\":null}";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.GameLeaderboardDto);
        Assert.NotNull(board);
        Assert.NotNull(board.Entries);
        Assert.Empty(board.Entries);
        Assert.Null(board.Me);
    }

    [Fact]
    public void MyRanksPayloadDeserializes()
    {
        const string json = "{\"ranks\":[{\"gameId\":\"snake\",\"value\":40,\"rank\":1,\"total\":1,\"weekRank\":0}]}";
        var ranks = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.GameScoreRanksDto);
        Assert.NotNull(ranks);
        Assert.NotNull(ranks.Ranks);
        Assert.Single(ranks.Ranks);
        Assert.Equal("snake", ranks.Ranks[0].GameId);
        Assert.Equal(40, ranks.Ranks[0].Value);
        Assert.Equal(1, ranks.Ranks[0].Rank);
        Assert.Equal(1, ranks.Ranks[0].Total);
        Assert.Equal(0, ranks.Ranks[0].WeekRank);
    }

    [Fact]
    public void UserPayloadCarriesTheLeaderboardFlagAndDefaultsToShown()
    {
        var hidden = JsonSerializer.Deserialize("{\"id\":\"u1\",\"showOnLeaderboards\":false}",
            AethernetJsonContext.Default.UserDto);
        Assert.NotNull(hidden);
        Assert.False(hidden.ShowOnLeaderboards);

        var legacy = JsonSerializer.Deserialize("{\"id\":\"u1\"}", AethernetJsonContext.Default.UserDto);
        Assert.NotNull(legacy);
        Assert.True(legacy.ShowOnLeaderboards);
    }

    [Fact]
    public void StatIdsMirrorTheServerCatalog()
    {
        Assert.Equal(ServerPluginStatIds, ScoreStatIds.All);
        Assert.Equal(ScoreStatIds.Catalog.Length, ScoreStatIds.All.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < ScoreStatIds.All.Length; index++)
        {
            Assert.True(seen.Add(ScoreStatIds.All[index]), ScoreStatIds.All[index]);
        }
    }

    [Fact]
    public void StatKindsFollowTheServerCatalog()
    {
        Assert.Equal(ScoreKind.Score, ScoreStatIds.KindOf("tetris"));
        Assert.Equal(ScoreKind.Level, ScoreStatIds.KindOf("watersort"));
        Assert.Equal(ScoreKind.Level, ScoreStatIds.KindOf("flow.hard"));
        Assert.Equal(ScoreKind.Time, ScoreStatIds.KindOf("sudoku.easy"));
        Assert.Equal(ScoreKind.Time, ScoreStatIds.KindOf("memory"));
        Assert.Equal(ScoreKind.Streak, ScoreStatIds.KindOf("chess"));
        Assert.True(ScoreStatIds.TryFind("memory.attempts", out var attempts));
        Assert.Equal(ScoreKind.Score, attempts.Kind);
        Assert.True(attempts.LowerIsBetter);
        Assert.True(attempts.IsBetter(4, 6));
        Assert.True(ScoreStatIds.TryFind("snake", out var snake));
        Assert.False(snake.LowerIsBetter);
        Assert.True(snake.IsBetter(6, 4));
        Assert.False(ScoreStatIds.Contains("doom"));
    }

    [Fact]
    public void DottedStatIdsBelongToTheirRootGame()
    {
        Assert.True(ScoreStatIds.BelongsTo("tetris.modern", "tetris"));
        Assert.True(ScoreStatIds.BelongsTo("tetris", "tetris"));
        Assert.False(ScoreStatIds.BelongsTo("tetrisx", "tetris"));
        Assert.False(ScoreStatIds.BelongsTo("tetris", "tetris.modern"));
        Assert.Equal(3, ScoreStatIds.CountFor("sudoku"));
        Assert.Equal(2, ScoreStatIds.CountFor("memory"));
        Assert.Equal(1, ScoreStatIds.CountFor("snake"));
        Assert.Equal(0, ScoreStatIds.CountFor("doom"));
        Assert.Equal("easy", ScoreStatIds.SuffixOf("sudoku.easy").ToString());
        Assert.Equal(0, ScoreStatIds.SuffixOf("snake").Length);
    }
}
