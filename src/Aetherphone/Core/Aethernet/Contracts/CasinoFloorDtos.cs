namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoPlayerRefDto(
    string UserId = "",
    string DisplayName = "",
    string Handle = "",
    string? AvatarUrl = null,
    int Badges = 0);

internal sealed record CasinoMissionDto(
    string Id = "",
    int Slot = 0,
    string Metric = "",
    string GameKind = "",
    long Target = 0,
    long Threshold = 0,
    long Reward = 0,
    long Progress = 0,
    bool Complete = false,
    bool Claimed = false);

internal sealed record CasinoMissionsDto(
    int DayIndex = 0,
    long ResetsAtUnix = 0,
    CasinoMissionDto[]? Missions = null);

internal sealed record CasinoMissionClaimRequest(string ClientActionId = "");

internal sealed record CasinoMissionClaimDto(
    bool Granted = false,
    string Reason = "",
    string MissionId = "",
    long Amount = 0,
    long Stack = 0,
    CasinoSittingDto? Sitting = null);

internal sealed record CasinoChallengeDto(
    string Id = "",
    string Title = "",
    string Kind = "",
    string GameKind = "",
    int ThresholdTenths = 0,
    long MinStake = 0,
    long StartsAtUnix = 0,
    long EndsAtUnix = 0,
    int State = 0,
    long Reward = 0,
    string BadgeId = "",
    CasinoPlayerRefDto? Leader = null,
    long LeaderValue = 0,
    long LeaderAtUnix = 0,
    long MyValue = 0);

internal sealed record CasinoChallengesDto(
    CasinoChallengeDto[]? Challenges = null,
    long ServerNowUnix = 0);

internal sealed record CasinoFameEntryDto(
    int Rank = 0,
    CasinoPlayerRefDto? Player = null,
    long Value = 0,
    string GameKind = "",
    long Stake = 0,
    long AtUnix = 0);

internal sealed record CasinoFameMeDto(
    bool OptedIn = false,
    int Rank = 0,
    long Value = 0);

internal sealed record CasinoFameBoardDto(
    string Board = "",
    string Span = "",
    int WeekIndex = 0,
    long StartsAtUnix = 0,
    long EndsAtUnix = 0,
    CasinoFameEntryDto[]? Entries = null,
    CasinoFameMeDto? Me = null,
    CasinoFameEntryDto? Champion = null);

internal sealed record CasinoFeedItemDto(
    string RoundId = "",
    string GameKind = "",
    CasinoPlayerRefDto? Player = null,
    long Stake = 0,
    long Payout = 0,
    int MultiplierTenths = 0,
    long AtUnixMs = 0,
    long Jackpot = 0);

internal sealed record CasinoFeedDto(
    string Tab = "",
    CasinoFeedItemDto[]? Items = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoFloorTickDto(
    string Kind = "",
    string RoundId = "",
    string GameKind = "",
    CasinoPlayerRefDto? Player = null,
    long Stake = 0,
    long Payout = 0,
    int MultiplierTenths = 0,
    long Amount = 0,
    int Recipients = 0,
    string[]? RoomIds = null,
    string ChallengeId = "",
    long AtUnixMs = 0);

internal sealed record CasinoFloorStateDto(CasinoFloorTickDto[]? Ticker = null);

internal sealed record CasinoRainPrivateDto(
    string RainId = "",
    long Amount = 0,
    long Stack = 0);
