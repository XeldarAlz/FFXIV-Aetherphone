using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemHandNames
{
    private const int CacheLimit = 256;

    private static readonly Dictionary<int, string> Cache = new();
    private static readonly string[] Percents = new string[101];
    private static LanguageInfo? cachedLanguage;

    public static string Describe(int strength)
    {
        if (strength < 0)
        {
            return string.Empty;
        }

        Validate();
        if (Cache.TryGetValue(strength, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= CacheLimit)
        {
            Cache.Clear();
        }

        var text = Build(strength);
        Cache[strength] = text;
        return text;
    }

    public static string Percent(int basisPoints)
    {
        Validate();
        var whole = Math.Clamp((basisPoints + 50) / 100, 0, 100);
        return Percents[whole] ??= Loc.T(L.Holdem.PercentValue, Games.Framework.GameNumber.Label(whole));
    }

    public static string RankSymbol(int value)
    {
        var rank = value == HoldemHands.Ace ? 0 : value - 1;
        return PlayingCards.RankLabel(Math.Clamp(rank, 0, PlayingCards.RankCount - 1));
    }

    internal static string Build(int strength)
    {
        var top = RankSymbol(HoldemHands.KickerOf(strength, 0));
        var second = RankSymbol(HoldemHands.KickerOf(strength, 1));
        return HoldemHands.DisplayCategoryOf(strength) switch
        {
            HoldemHands.RoyalFlush => Loc.T(L.Holdem.HandRoyalFlush),
            HoldemHands.StraightFlush => Loc.T(L.Holdem.HandStraightFlush, top),
            HoldemHands.Quads => Loc.T(L.Holdem.HandQuads, top),
            HoldemHands.FullHouse => Loc.T(L.Holdem.HandFullHouse, top, second),
            HoldemHands.Flush => Loc.T(L.Holdem.HandFlush, top),
            HoldemHands.Straight => Loc.T(L.Holdem.HandStraight, top),
            HoldemHands.Trips => Loc.T(L.Holdem.HandTrips, top),
            HoldemHands.TwoPair => Loc.T(L.Holdem.HandTwoPair, top, second),
            HoldemHands.Pair => Loc.T(L.Holdem.HandPair, top),
            _ => Loc.T(L.Holdem.HandHighCard, top),
        };
    }

    private static void Validate()
    {
        if (ReferenceEquals(cachedLanguage, Loc.Current))
        {
            return;
        }

        cachedLanguage = Loc.Current;
        Cache.Clear();
        Array.Clear(Percents);
    }
}
