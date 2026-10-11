using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino;

internal static class ChipValue
{
    public static long Coins(long chips, long rate) => rate <= 0 ? 0 : chips / rate;
}

internal sealed class ChipValueText
{
    private long chips = long.MinValue;
    private long rate;
    private LanguageInfo? language;
    private string full = string.Empty;
    private string compact = string.Empty;

    public string Full(long chipCount, long chipsPerCoin)
    {
        Refresh(chipCount, chipsPerCoin);
        return full;
    }

    public string Compact(long chipCount, long chipsPerCoin)
    {
        Refresh(chipCount, chipsPerCoin);
        return compact;
    }

    private void Refresh(long chipCount, long chipsPerCoin)
    {
        if (chipCount == chips && chipsPerCoin == rate && ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        chips = chipCount;
        rate = chipsPerCoin;
        language = Loc.Current;
        var coins = ChipValue.Coins(chipCount, chipsPerCoin);
        full = Loc.T(L.Strip.CoinsAmount, NumberText.Group(coins));
        compact = Loc.T(L.Strip.CoinsAmount, NumberText.Compact(coins));
    }
}
