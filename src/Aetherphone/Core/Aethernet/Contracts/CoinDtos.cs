namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CoinWalletDto(
    long Balance,
    long LifetimeEarned,
    long LifetimeSpent,
    long EarnedToday,
    long DailyCap,
    long ResetsAtUnix,
    int StreakDays,
    bool CheckInAvailable,
    bool Paused,
    string FeaturedGameId,
    int GameMinSeconds,
    int GameDeepSeconds,
    CoinRuleStatusDto[] Rules,
    long? FrozenUntilUnix = null,
    string FreezeReason = "");

internal sealed record CoinRuleStatusDto(
    string RuleId,
    string App,
    long Amount,
    long PeriodCap,
    long EarnedThisPeriod,
    int Awards,
    int PeriodLimit,
    bool Weekly);

internal sealed record CoinLedgerEntryDto(
    string Id,
    string RuleId,
    long Amount,
    long BalanceAfter,
    string App,
    string SourceId,
    string Detail,
    long CreatedAtUnix);

internal sealed record CoinLedgerPage(CoinLedgerEntryDto[] Items, string? NextCursor = null);

internal sealed record CoinSkuDto(
    string Id,
    string Kind,
    string Payload,
    string Name,
    long Price,
    int SortOrder,
    bool Owned,
    long? AvailableUntilUnix = null,
    CoinTranslationDto[]? Translations = null,
    string CategoryId = "");

internal sealed record CoinTranslationDto(string Language, string Name);

internal sealed record CoinCatalogDto(CoinSkuDto[] Skus, long Balance);

internal sealed record CoinShopCategoryDto(
    string Id,
    string ParentId,
    string Name,
    long Icon,
    int SortOrder,
    int ItemCount,
    long? SoonestLeavingUnix = null,
    CoinTranslationDto[]? Translations = null,
    string ImageUrl = "");

internal sealed record CoinShopDto(
    CoinShopCategoryDto[] Categories,
    CoinSkuDto[] Items,
    bool ItemsComplete,
    int UnfiledCount = 0,
    long? UnfiledSoonestLeavingUnix = null);

internal sealed record CoinShopShelfPage(CoinSkuDto[] Items, string? NextCursor = null);

internal sealed record CoinEntitlementsDto(string[] OwnedSkuIds, long Balance);

internal sealed record CoinAwardDto(bool Granted, long Amount, long Balance, string Reason = "");

internal sealed record CoinGameSessionRequest(string GameId);

internal sealed record CoinGameSessionDto(
    string SessionId,
    string GameId,
    bool Featured,
    int MinSeconds,
    int DeepSeconds,
    long ExpiresAtUnix,
    string Reason = "",
    long StartedAtUnix = 0,
    int CooldownSeconds = 0);

internal sealed record CoinQuestDto(
    string Id,
    string Group,
    string App,
    int Progress,
    int Target,
    long Amount,
    bool Claimed);

internal sealed record CoinQuestBoardDto(int DayIndex, long ResetsAtUnix, CoinQuestDto[] Quests);

internal sealed record HalloweenTreatsReport(int Found);

internal sealed record HalloweenTreatsDto(int Found, bool Granted);

internal sealed record CoinListenStartRequest(string StationId);

internal sealed record CoinListenStartDto(bool Started, string Reason = "", string SessionId = "");

internal sealed record CoinListenEndDto(bool Credited, int Seconds, int MinutesToday);

internal sealed record CoinPurchaseRequest(string SkuId, long ExpectedPrice);

internal sealed record CoinPurchaseResult(
    bool Purchased,
    long Balance,
    string SkuId,
    long Price,
    string Reason = "");
