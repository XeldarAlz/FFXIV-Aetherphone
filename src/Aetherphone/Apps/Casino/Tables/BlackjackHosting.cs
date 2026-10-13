using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

internal enum DealerDeck : byte
{
    Deal,
    WaitingBets,
    HandLive,
    HandOver,
    Paused,
}

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
        return HostDeals(board) && !board.AutoDeal && board.Phase == BlackjackPhases.Betting
               && board.DeadlineUnixMs == 0 && !board.Paused;
    }

    public static bool IsDealingHost(CasinoBlackjackRoomStateDto board, string me, string ownerId)
    {
        if (me.Length == 0 || !HostDeals(board))
        {
            return false;
        }

        return string.Equals(me, ownerId, StringComparison.Ordinal)
               || string.Equals(me, board.DealerUserId, StringComparison.Ordinal);
    }

    public static bool DealsFromDeck(CasinoBlackjackRoomStateDto board, string me, string ownerId, bool seated)
    {
        return !seated && IsDealingHost(board, me, ownerId);
    }

    public static DealerDeck DealerDeckOf(CasinoBlackjackRoomStateDto board)
    {
        if (board.Paused)
        {
            return DealerDeck.Paused;
        }

        if (BlackjackPhases.Over(board.Phase))
        {
            return DealerDeck.HandOver;
        }

        if (board.Phase != BlackjackPhases.Betting)
        {
            return DealerDeck.HandLive;
        }

        return AnyBet(board) ? DealerDeck.Deal : DealerDeck.WaitingBets;
    }

    public static LocString DealLabel(CasinoBlackjackRoomStateDto board)
    {
        return board.AutoDeal ? L.Tables.DealNow : L.Tables.Deal;
    }

    public static bool TryDealerStatus(CasinoBlackjackRoomStateDto board, DealerDeck deck, out LocString status)
    {
        if (deck == DealerDeck.WaitingBets)
        {
            status = L.Tables.DealerWaitingBets;
            return true;
        }

        if (deck == DealerDeck.Deal && board.AutoDeal)
        {
            status = L.Tables.DealerClockDeals;
            return true;
        }

        status = default;
        return false;
    }

    public static bool AnyBet(CasinoBlackjackRoomStateDto board)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return false;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].Committed > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanRebuy(CasinoBlackjackRoomStateDto board, long seatChips, long committed)
    {
        return CasinoCurrencies.Of(board) == CasinoCurrencies.Practice && board.PracticeRebuy
            && board.Tournament is not { Live: true } && Broke(board, seatChips, committed);
    }

    public static bool Broke(CasinoBlackjackRoomStateDto board, long seatChips, long committed)
    {
        var currency = CasinoCurrencies.Of(board);
        if (currency != CasinoCurrencies.Practice && currency != CasinoCurrencies.Gil)
        {
            return false;
        }

        return seatChips < Math.Max(1, board.MinBet) && committed == 0 && board.Phase == BlackjackPhases.Betting;
    }

    public static LocString BrokeStatus(CasinoBlackjackRoomStateDto board, string me)
    {
        if (board.Tournament is { Live: true } tournament && IsEntrant(tournament, me))
        {
            return L.Venue.TournamentYouAreOut;
        }

        return board.PracticeRebuy ? L.Tables.RebuysTournamentNote : L.Tables.ReasonRebuyOff;
    }

    private static bool IsEntrant(CasinoBlackjackTournamentDto tournament, string me)
    {
        var standings = tournament.Standings;
        if (standings is null || me.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < standings.Length; index++)
        {
            if (string.Equals(standings[index].UserId, me, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static long NaturalPayout(CasinoBlackjackRoomStateDto board, long bet)
    {
        return CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.Of(board).BlackjackPays, bet);
    }
}

internal sealed class BlackjackHostedText
{
    private const string InfoSeparator = " · ";

    private static readonly LocString[] PaysLabels = { L.Tables.Pays32, L.Tables.Pays21, L.Tables.Pays11 };

    private readonly CasinoTextCache texts = new();
    private CasinoBlackjackRuleSheetDto? rulesSource;
    private LanguageInfo? rulesLanguage;
    private string rules = string.Empty;
    private string infoFirst = string.Empty;
    private string infoSecond = string.Empty;
    private string info = string.Empty;

    public string Info(string first, string second)
    {
        if (second.Length == 0)
        {
            return first;
        }

        if (ReferenceEquals(first, infoFirst) && ReferenceEquals(second, infoSecond))
        {
            return info;
        }

        infoFirst = first;
        infoSecond = second;
        info = string.Concat(first, InfoSeparator, second);
        return info;
    }

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

    public string GilCompact(long amount)
    {
        return texts.Compact(L.Tables.GilAmount, amount);
    }

    public string Rebuy(long stack)
    {
        return texts.Compact(L.Tables.RebuyTo, stack);
    }
}
