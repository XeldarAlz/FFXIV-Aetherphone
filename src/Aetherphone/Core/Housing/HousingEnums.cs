namespace Aetherphone.Core.Housing;

internal enum HousingPlotSize : byte
{
    Unknown,
    Small,
    Medium,
    Large,
}

internal enum HousingLotteryPhase : byte
{
    Unknown,
    Entry,
    Results,
    Unavailable,
    Expired,
}

internal enum HousingPurchaseEligibility : byte
{
    Unknown,
    Private,
    FreeCompany,
    Both,
}

internal enum HousingPurchaseMode : byte
{
    Unknown,
    Lottery,
    FirstComeFirstServed,
}

internal enum HousingDataFreshness : byte
{
    Unknown,
    Live,
    Recent,
    Stale,
    Cached,
}

internal enum HousingProviderKind : byte
{
    None,
    Service,
    China,
    Cache,
}

internal enum HousingLoadState : byte
{
    Idle,
    Loading,
    Ready,
    Empty,
    Failed,
}
