using System.Text;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

[Flags]
internal enum HostRuleSection : ushort
{
    None = 0,
    Dealing = 1,
    Turns = 2,
    Blackjack = 4,
    Holdem = 8,
    Dice = 16,
    FreePlay = 32,
    Gil = 64,
    Room = 128,
}

internal readonly record struct HostRulesKey(
    HostGame Game,
    int Currency,
    int DealerMode,
    int TurnSeconds,
    int Pays,
    int Decks,
    int Ante,
    bool HighestWins,
    int RoundSeconds,
    long DiceStake,
    long Stack,
    long Payout,
    bool PayoutCapped,
    bool Pinned,
    LanguageInfo? Language)
{
    public static HostRulesKey Of(HostDraft draft, bool pinned) => new(draft.Game, draft.Currency,
        draft.DealerMode, draft.TurnSeconds, draft.Pays, draft.Decks, draft.Ante, draft.HighestWins,
        draft.RoundSeconds, draft.DiceStakeValue, draft.TableStackValue, draft.PayoutValue, draft.PayoutCapped, pinned,
        Loc.Current);
}

internal static class HostRules
{
    private const int SecondsPerMinute = 60;
    private const int PercentPerTenth = 10;

    public static readonly LocString[] PaysLabels = { L.Tables.Pays32, L.Tables.Pays21, L.Tables.Pays11 };

    public static HostRuleSection Sections(HostGame game, int currency)
    {
        var sections = HostRuleSection.Room;
        if (game == HostGame.Blackjack)
        {
            sections |= HostRuleSection.Dealing;
            if (CasinoCurrencies.SeatBanked(currency))
            {
                sections |= HostRuleSection.Blackjack;
            }
        }

        if (HostGames.HasSeats(game))
        {
            sections |= HostRuleSection.Turns;
        }

        if (game == HostGame.Holdem)
        {
            sections |= HostRuleSection.Holdem;
        }

        if (game == HostGame.DiceTable)
        {
            sections |= HostRuleSection.Dice;
        }

        if (currency == CasinoCurrencies.Practice)
        {
            sections |= HostRuleSection.FreePlay;
        }

        if (currency == CasinoCurrencies.Gil && game is HostGame.Blackjack or HostGame.Book)
        {
            sections |= HostRuleSection.Gil;
        }

        return sections;
    }

    public static bool StakesEachRound(HostDraft draft) =>
        draft.Game == HostGame.DiceTable && draft.Gil && draft.HighestWins;

    public static LocString Refusal(HostGame game, int currency, bool gilOpen)
    {
        if (currency == CasinoCurrencies.Gil && !gilOpen && game != HostGame.Holdem)
        {
            return L.Tables.PlayGilClosed;
        }

        if (HostGames.IsBook(game))
        {
            return L.HostSetup.BookGilOnly;
        }

        return currency == CasinoCurrencies.Chips ? L.Tables.PlayRoomsNoCoins : L.Tables.PlayHoldemNoGil;
    }

    public static LocString CurrencyHint(int currency) => currency switch
    {
        CasinoCurrencies.Practice => L.Tables.PlayPracticeLine,
        CasinoCurrencies.Gil => L.Tables.PlayGilLine,
        _ => L.HostSetup.ChipsHint,
    };

    public static LocString ListingHint(int listing) => listing switch
    {
        CasinoListings.Open => L.Tables.JoinOpenLine,
        CasinoListings.Knock => L.Tables.JoinKnockLine,
        _ => L.Tables.JoinInviteLine,
    };

    public static LocString ListingSummary(int listing) => listing switch
    {
        CasinoListings.Open => L.HostSetup.SummaryAnyone,
        CasinoListings.Knock => L.HostSetup.SummaryKnock,
        _ => L.Tables.SummaryInvite,
    };

    public static string RoundText(int seconds) => seconds < SecondsPerMinute
        ? Loc.T(L.Tables.SecondsValue, seconds.ToString(Loc.Culture))
        : Loc.T(L.HostSetup.Minutes, (seconds / SecondsPerMinute).ToString(Loc.Culture));

    public static string AnteText(int ante) =>
        Loc.T(L.HostSetup.Percent, (HostLadders.AnteTenths[ante] * PercentPerTenth).ToString(Loc.Culture));

    public static string Summary(HostDraft draft, bool pinned)
    {
        var builder = new StringBuilder();
        switch (draft.Game)
        {
            case HostGame.Blackjack:
                Append(builder, Loc.T(draft.HostDeals ? L.HostSetup.PartYouDeal : L.HostSetup.PartClockDeals));
                Append(builder, Turns(draft));
                if (draft.SeatBanked)
                {
                    Append(builder, Loc.T(L.HostSetup.PartPaysDecks, Loc.T(PaysLabels[draft.Pays]),
                        draft.Decks.ToString(Loc.Culture)));
                }

                break;
            case HostGame.Holdem:
                Append(builder, Turns(draft));
                Append(builder, draft.Ante == 0
                    ? Loc.T(L.HostSetup.PartNoAnte)
                    : Loc.T(L.HostSetup.PartAnte, AnteText(draft.Ante)));
                break;
            case HostGame.DiceTable:
                Append(builder, Loc.T(draft.HighestWins ? L.HostSetup.PartHighest : L.HostSetup.PartLowest));
                Append(builder, Loc.T(L.HostSetup.PartRounds, RoundText(HostLadders.RoundSeconds[draft.RoundSeconds])));
                if (draft.DiceStakeValue > 0)
                {
                    Append(builder, Loc.T(L.HostSetup.PartRoundStake, Gil(draft.DiceStakeValue)));
                }

                break;
        }

        if (draft.Practice)
        {
            Append(builder, Loc.T(L.HostSetup.PartStack, NumberText.Compact(draft.TableStackValue)));
        }

        if ((Sections(draft.Game, draft.Currency) & HostRuleSection.Gil) != 0 && draft.PayoutCapped)
        {
            Append(builder, Loc.T(L.HostSetup.PartPayoutCap, Gil(draft.PayoutValue)));
        }

        Append(builder, Loc.T(pinned ? L.HostSetup.PartPinned : L.HostSetup.PartNotPinned));
        builder[0] = char.ToUpper(builder[0], Loc.Culture);
        return builder.ToString();
    }

    private static string Turns(HostDraft draft) =>
        Loc.T(L.HostSetup.PartTurns, draft.TurnSeconds.ToString(Loc.Culture));

    private static string Gil(long amount) => Loc.T(L.Tables.GilValue, NumberText.Compact(amount));

    private static void Append(StringBuilder builder, string part)
    {
        if (builder.Length > 0)
        {
            builder.Append(Loc.T(L.HostSetup.ListSeparator));
        }

        builder.Append(part);
    }
}
