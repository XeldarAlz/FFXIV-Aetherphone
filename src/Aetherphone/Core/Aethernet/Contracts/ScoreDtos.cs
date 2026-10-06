namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GameScoreSubmitRequest(string GameId = "", int Value = 0, string? SessionId = null);

internal sealed record GameScoreSubmitDto(
    bool Accepted = false,
    string Reason = "",
    int Best = 0,
    int Rank = 0,
    int Total = 0,
    int FriendsRank = 0,
    int WeekRank = 0);

internal sealed record GameLeaderboardEntryDto(
    int Rank = 0,
    string UserId = "",
    string DisplayName = "",
    string Handle = "",
    string? AvatarUrl = null,
    int Badges = 0,
    int Value = 0,
    long AchievedAtUnix = 0);

internal sealed record GameLeaderboardMeDto(int Rank = 0, int Value = 0, int Total = 0);

internal sealed record GameLeaderboardDto(
    string GameId = "",
    string Scope = "",
    string Span = "",
    GameLeaderboardEntryDto[]? Entries = null,
    GameLeaderboardMeDto? Me = null);

internal sealed record GameScoreRankDto(
    string GameId = "",
    int Value = 0,
    int Rank = 0,
    int Total = 0,
    int WeekRank = 0);

internal sealed record GameScoreRanksDto(GameScoreRankDto[]? Ranks = null);

internal sealed record UpdateGamesPrivacyRequest(bool? ShowOnLeaderboards = null);
