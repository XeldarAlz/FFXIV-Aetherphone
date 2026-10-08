using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackHosting
{
    public static bool SeatBanked(CasinoBlackjackRoomStateDto board)
    {
        return CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(board));
    }

    public static bool DealerPowers(CasinoBlackjackRoomStateDto board, string me, string ownerId)
    {
        if (me.Length == 0 || !SeatBanked(board))
        {
            return false;
        }

        if (string.Equals(me, ownerId, StringComparison.Ordinal)
            || string.Equals(me, board.DealerUserId, StringComparison.Ordinal))
        {
            return true;
        }

        var coDealers = board.CoDealers;
        if (coDealers is null)
        {
            return false;
        }

        for (var index = 0; index < coDealers.Length; index++)
        {
            if (string.Equals(coDealers[index], me, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HostDeals(CasinoBlackjackRoomStateDto board)
    {
        return board.DealerMode == CasinoDealerModes.Host && SeatBanked(board);
    }

    public static bool AwaitsDeal(CasinoBlackjackRoomStateDto board)
    {
        return HostDeals(board) && board.Phase == BlackjackPhases.Betting && board.DeadlineUnixMs == 0
               && !board.Paused;
    }

    public static bool CanRebuy(CasinoBlackjackRoomStateDto board, long seatChips)
    {
        if (CasinoCurrencies.Of(board) != CasinoCurrencies.Practice || !board.PracticeRebuy)
        {
            return false;
        }

        var floor = Math.Max(1, board.MinBet);
        return seatChips < floor && (board.Phase == BlackjackPhases.Betting || BlackjackPhases.Over(board.Phase));
    }

    public static long NaturalPayout(CasinoBlackjackRoomStateDto board, long bet)
    {
        return CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.Of(board).BlackjackPays, bet);
    }
}

internal sealed class BlackjackHostedText
{
    private static readonly LocString[] PaysLabels = { L.Tables.Pays32, L.Tables.Pays21, L.Tables.Pays11 };

    private readonly CasinoTextCache texts = new();
    private CasinoBlackjackRuleSheetDto? rulesSource;
    private LanguageInfo? rulesLanguage;
    private string rules = string.Empty;

    public string Rules(CasinoBlackjackRoomStateDto board)
    {
        var sheet = board.Rules;
        if (sheet is null)
        {
            return Loc.T(L.Casino.BlackjackRules);
        }

        if (ReferenceEquals(sheet, rulesSource) && ReferenceEquals(rulesLanguage, Loc.Current))
        {
            return rules;
        }

        rulesSource = sheet;
        rulesLanguage = Loc.Current;
        rules = Loc.T(sheet.DealerHitsSoft17 ? L.Tables.RulesLineHits : L.Tables.RulesLineStands,
            Loc.T(PaysLabels[Math.Clamp(sheet.BlackjackPays, 0, PaysLabels.Length - 1)]),
            sheet.Decks.ToString(Loc.Culture));
        return rules;
    }

    public string GilLimits(CasinoBlackjackRoomStateDto board)
    {
        return texts.Numbers(L.Tables.GilLimits, board.BankHeadroom > 0 ? board.BankHeadroom : board.Bank,
            board.MaxPayout);
    }

    public string DealtBy(string name)
    {
        return texts.Named(L.Tables.DealtBy, name);
    }

    public string WaitingDeal(string name)
    {
        return texts.Named(L.Tables.WaitingDeal, name);
    }

    public string Gil(long amount)
    {
        return texts.Number(L.Tables.GilAmount, amount);
    }

    public string Rebuy(long stack)
    {
        return texts.Compact(L.Tables.RebuyTo, stack);
    }
}
