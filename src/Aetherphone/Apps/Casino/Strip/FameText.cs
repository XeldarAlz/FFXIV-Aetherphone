using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal static class FameText
{
    public static LocString BoardName(string board) => board switch
    {
        CasinoFameBoards.Multiplier => L.Club.FameMultiplier,
        CasinoFameBoards.Win => L.Club.FameWin,
        CasinoFameBoards.Poker => L.Club.FamePoker,
        _ => L.Club.FameProfit,
    };

    public static string Value(string board, long value) =>
        string.Equals(board, CasinoFameBoards.Multiplier, StringComparison.Ordinal)
            ? CasinoMultiples.Label((int)Math.Min(value * 10, int.MaxValue))
            : NumberText.Compact(value);

    public static string Name(CasinoFameEntryDto entry) => WinsTicker.NameOf(entry.Player);
}
