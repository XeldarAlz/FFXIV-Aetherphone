using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostDraftSummary
{
    private static readonly LocString[] CurrencyNames =
        { L.Tables.CurrencyChips, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    public static string Of(CasinoHostDraftMemory memory, bool gilOpen)
    {
        var draft = new HostDraft();
        draft.Restore(memory);
        draft.Normalize(gilOpen);
        return Of(draft);
    }

    public static string Of(HostDraft draft)
    {
        var game = Loc.T(HostGames.Of(draft.Game).Title);
        var currency = Loc.T(CurrencyNames[Math.Clamp(draft.Currency, 0, CurrencyNames.Length - 1)]);
        switch (draft.Game)
        {
            case HostGame.Blackjack when draft.Gil:
            case HostGame.Book:
                return Loc.T(L.VenueHub.DraftSummaryGil, game, currency, NumberText.Compact(draft.BankValue));
            case HostGame.Blackjack:
                return Loc.T(L.VenueHub.DraftSummaryBets, game, currency, NumberText.Compact(draft.MinBetValue),
                    NumberText.Compact(draft.MaxBetValue));
            case HostGame.Holdem:
                return Loc.T(L.VenueHub.DraftSummaryBets, game, currency, NumberText.Compact(draft.BigBlindValue / 2),
                    NumberText.Compact(draft.BigBlindValue));
            case HostGame.Deathroll:
                return Loc.T(L.VenueHub.DraftSummaryStake, game, currency, NumberText.Compact(draft.StakeValue));
            default:
                return Loc.T(L.VenueHub.DraftSummaryPlain, game, currency);
        }
    }
}
