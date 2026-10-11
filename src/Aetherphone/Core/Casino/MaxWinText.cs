using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino;

internal sealed class MaxWinText
{
    private long chips = -1;
    private long rate;
    private LanguageInfo? language;
    private string text = string.Empty;

    public string For(long maxWinChips, long chipsPerCoin)
    {
        if (maxWinChips == chips && chipsPerCoin == rate && ReferenceEquals(language, Loc.Current))
        {
            return text;
        }

        chips = maxWinChips;
        rate = chipsPerCoin;
        language = Loc.Current;
        text = maxWinChips <= 0
            ? string.Empty
            : Loc.T(L.Chips.MaxWinLine, NumberText.Compact(maxWinChips),
                NumberText.Group(ChipValue.Coins(maxWinChips, chipsPerCoin)));
        return text;
    }
}
