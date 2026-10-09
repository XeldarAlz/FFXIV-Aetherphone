using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TableAmounts
{
    public static string Amount(CasinoTextCache texts, long amount, int currency) =>
        currency == CasinoCurrencies.Gil ? texts.Compact(L.Tables.GilValue, amount) : NumberText.Compact(amount);

    public static string Range(CasinoTextCache texts, long low, long high, int currency) =>
        texts.Compacts(currency == CasinoCurrencies.Gil ? L.Tables.GilRange : L.Tables.BetsRange, low, high);

    public static string Full(long amount, int currency) =>
        currency == CasinoCurrencies.Gil
            ? Loc.T(L.Tables.GilAmount, NumberText.Group(amount))
            : NumberText.Group(amount);
}
