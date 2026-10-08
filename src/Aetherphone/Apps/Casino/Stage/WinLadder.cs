using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Casino.Stage;

internal enum WinTier : byte
{
    None,
    Win,
    Nice,
    Big,
    Mega,
    Epic,
    Legendary,
}

internal readonly record struct WinTierSpec(
    WinTier Tier,
    int MinimumNetMultiple,
    int Confetti,
    int Sparkles,
    float ShowerSeconds,
    float Sweep,
    float CountUpSeconds,
    UiSound Sound,
    bool Fanfare,
    bool Banner,
    bool BulbChase,
    bool FullCard);

internal static class WinLadder
{
    public const float SkipAfterSeconds = 0.5f;
    public const float InstantPopSeconds = 0.3f;
    public const float BannerHoldSeconds = 1.6f;

    private static readonly WinTierSpec[] Specs =
    {
        new(WinTier.None, 0, 0, 0, 0f, 0f, 0f, UiSound.WinSmall, false, false, false, false),
        new(WinTier.Win, 0, 0, 12, 0f, 0f, 0.4f, UiSound.WinSmall, false, false, false, false),
        new(WinTier.Nice, 3, 36, 0, 0f, 0.4f, 0.8f, UiSound.WinSmall, false, true, false, false),
        new(WinTier.Big, 10, 90, 0, 1f, 0.7f, 1.6f, UiSound.WinBig, false, true, true, false),
        new(WinTier.Mega, 25, 160, 0, 2f, 0.85f, 2.4f, UiSound.WinBig, true, true, true, false),
        new(WinTier.Epic, 50, 240, 0, 3f, 1f, 3.2f, UiSound.WinEpic, false, true, true, true),
        new(WinTier.Legendary, 100, 320, 0, 4f, 1f, 4f, UiSound.WinEpic, true, true, true, true),
    };

    public static WinTierSpec Spec(WinTier tier) => Specs[(int)tier];

    public static WinTier TierFor(long stake, long payout, bool jackpot = false)
    {
        if (jackpot && payout > 0)
        {
            return WinTier.Legendary;
        }

        if (payout <= stake || payout <= 0)
        {
            return WinTier.None;
        }

        if (stake <= 0)
        {
            return WinTier.Win;
        }

        var net = payout - stake;
        for (var index = Specs.Length - 1; index > (int)WinTier.Win; index--)
        {
            if (net >= stake * (long)Specs[index].MinimumNetMultiple)
            {
                return Specs[index].Tier;
            }
        }

        return WinTier.Win;
    }

    public static LocString Banner(WinTier tier, bool jackpot) => tier switch
    {
        WinTier.Nice => L.Strip.NiceWin,
        WinTier.Big => L.Strip.BigWin,
        WinTier.Mega => L.Strip.MegaWin,
        WinTier.Epic => L.Strip.EpicWin,
        WinTier.Legendary => jackpot ? L.Casino.JackpotWon : L.Strip.Legendary,
        _ => default,
    };
}
