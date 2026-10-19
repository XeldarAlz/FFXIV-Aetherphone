namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record PrismEntryDto(
    string Id = "",
    string ImageUrl = "",
    int Width = 0,
    int Height = 0,
    string Status = "",
    string RejectReason = "");

internal sealed record PrismJudgeRewardDto(
    long Amount = 0,
    int PerWeek = 0,
    int LeftThisWeek = 0,
    int MinLooks = 0,
    int MinViewedMs = 0);

internal sealed record PrismWeekDto(
    string Id = "",
    string Title = "",
    string Brief = "",
    string Phase = "",
    long EntriesCloseAtUnix = 0,
    long JudgingCloseAtUnix = 0,
    long FinalCloseAtUnix = 0,
    long ResultsAtUnix = 0,
    PrismEntryDto? MyEntry = null,
    int BallotsCast = 0,
    string PreviousWeekId = "",
    int Issue = 0,
    int EntryCount = 0,
    PrismJudgeRewardDto? JudgeReward = null,
    long BallotCoins = 0);

internal sealed record PrismEntryRequest(string MediaKey, int Width, int Height);

internal sealed record PrismLookDto(string Id = "", string ImageUrl = "", int Width = 0, int Height = 0);

internal sealed record PrismBallotDto(string Id = "", string Stage = "", PrismLookDto[]? Looks = null,
    string HeatLabel = "");

internal sealed record PrismStarDto(string LookId, int Stars, int ViewedMs);

internal sealed record PrismBallotRequest(PrismStarDto[] Stars);

internal sealed record PrismPlacingDto(
    int Rank = 0,
    string ImageUrl = "",
    int Width = 0,
    int Height = 0,
    string DisplayName = "",
    string World = "",
    string Handle = "");

internal sealed record PrismResultsDto(
    string WeekId = "",
    string Title = "",
    PrismPlacingDto[]? HeatPodium = null,
    PrismPlacingDto[]? FinalPodium = null,
    int MyHeatRank = 0,
    int HeatSize = 0,
    int MyFinalRank = 0,
    int Percentile = 0,
    long CoinsAwarded = 0,
    int Issue = 0,
    int EntryCount = 0,
    string MyImageUrl = "",
    string HeatLabel = "");
