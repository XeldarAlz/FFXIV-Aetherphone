namespace Aetherphone.Core.Casino;

internal static class CasinoFloorRules
{
    public const string FloorRoomId = "casino-floor";
    public const string FloorKind = "casino.floor";
    public const string TickEvent = "floor.tick";
    public const string RainEvent = "floor.rain";
    public const int TickerLength = 20;
    public const int FeedLength = 50;
    public const int BigWinTenths = 1000;
    public const long HighRollerStake = 100_000;
    public const int FameDefaultLimit = 50;
    public const int FameMaxLimit = 100;
    public const int PodiumPlaces = 3;
    public const int SlotPlay = 0;
    public const int SlotVariety = 1;
    public const int SlotStretch = 2;
}

internal static class CasinoTickKinds
{
    public const string Win = "win";
    public const string Jackpot = "jackpot";
    public const string Rain = "rain";
    public const string Challenge = "challenge";
}

internal static class CasinoFeedTabs
{
    public const string All = "all";
    public const string High = "high";
}

internal static class CasinoFameBoards
{
    public const string Profit = "profit";
    public const string Multiplier = "multiplier";
    public const string Win = "win";
    public const string Poker = "poker";

    public static readonly string[] All = { Profit, Multiplier, Win, Poker };
}

internal static class CasinoFameSpans
{
    public const string Week = "week";
    public const string Last = "last";
}

internal static class CasinoMissionMetrics
{
    public const string Rounds = "rounds";
    public const string Games = "games";
    public const string Wins = "wins";
    public const string Hits = "hits";
    public const string MaxBet = "maxbet";
    public const string Hosted = "hosted";
}

internal static class CasinoChallengeStates
{
    public const int Scheduled = 0;
    public const int Live = 1;
    public const int Settled = 2;
    public const int Cancelled = 3;
    public const int Ended = 4;
}

internal static class CasinoChallengeKinds
{
    public const string First = "first";
    public const string Most = "most";
}
