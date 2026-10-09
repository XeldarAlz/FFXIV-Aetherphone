using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class HostDraft
{
    private const long DefaultGilBank = 10_000_000;
    private const long DefaultGilMaxBet = 500_000;
    private const long DefaultGilMinBet = 1_000;
    private const int DefaultDecks = 6;

    public HostGame Game;
    public int Currency = CasinoCurrencies.Chips;
    public int Seats = CasinoHostingRules.DefaultSeats;
    public int Listing = CasinoListings.Private;
    public bool Spectators = true;
    public bool FaceUp;
    public string Name = string.Empty;
    public int MinBet;
    public int MaxBet;
    public int BigBlind;
    public int Ante;
    public int Stack;
    public bool PracticeRebuy = true;
    public int Bank;
    public int MaxPayout;
    public bool PayoutCapped;
    public int TurnSeconds = CasinoHostingRules.DefaultTurnSeconds;
    public bool TimeBank = true;
    public int DealerMode = CasinoDealerModes.House;
    public bool AutoDeal = true;
    public int Pays = CasinoRuleSheet.PaysThreeToTwo;
    public bool HitsSoft17;
    public int Decks = DefaultDecks;
    public int Splits = CasinoRuleSheet.SplitsToFour;
    public int Doubles = CasinoRuleSheet.DoublesAny;
    public bool Charlie;
    public bool Peek = true;
    public int Sides;
    public bool HighestWins = true;
    public int RoundSeconds;
    public int StartAt;
    public int Stake;

    public HostDraft()
    {
        Reset(HostGame.Blackjack);
    }

    public bool SeatBanked => CasinoCurrencies.SeatBanked(Currency);

    public bool Gil => Currency == CasinoCurrencies.Gil;

    public bool Practice => Currency == CasinoCurrencies.Practice;

    public long[] BetRungs => Gil ? HostLadders.Gil : HostLadders.Bets;

    public long StackValue => HostLadders.PracticeStacks[Stack];

    public long BankValue => HostLadders.Gil[Bank];

    public long MinBetValue => BetRungs[MinBet];

    public long MaxBetValue => BetRungs[MaxBet];

    public long BigBlindValue => HostLadders.Bets[BigBlind];

    public long PayoutValue => PayoutCapped ? HostLadders.Gil[MaxPayout] : BankValue;

    public long StakeValue => BetRungs[Stake];

    public int SidesValue => (int)HostLadders.DiceSides[Sides];

    public int StartAtValue => (int)HostLadders.StartNumbers[StartAt];

    public LadderSpan BetSpan => Currency switch
    {
        CasinoCurrencies.Gil => HostLadders.GilBets(BankValue),
        CasinoCurrencies.Practice => HostLadders.PracticeBets(StackValue),
        _ => HostLadders.CoinBets,
    };

    public LadderSpan PayoutSpan => new(MaxBet, Bank);

    public void Reset(HostGame game)
    {
        Game = game;
        Currency = HostGames.IsVenue(game) ? CasinoCurrencies.Practice : CasinoCurrencies.Chips;
        Seats = CasinoHostingRules.DefaultSeats;
        Listing = CasinoListings.Private;
        Spectators = true;
        FaceUp = false;
        Name = string.Empty;
        BigBlind = HostLadders.FloorIndex(HostLadders.Bets, CasinoHostingRules.DefaultHoldemBigBlind);
        Ante = 0;
        Stack = HostLadders.FloorIndex(HostLadders.PracticeStacks, CasinoHostingRules.DefaultPracticeStack);
        PracticeRebuy = true;
        Bank = HostLadders.FloorIndex(HostLadders.Gil, DefaultGilBank);
        PayoutCapped = false;
        TurnSeconds = CasinoHostingRules.DefaultTurnSeconds;
        TimeBank = true;
        DealerMode = CasinoDealerModes.House;
        AutoDeal = true;
        Pays = CasinoRuleSheet.PaysThreeToTwo;
        HitsSoft17 = false;
        Decks = DefaultDecks;
        Splits = CasinoRuleSheet.SplitsToFour;
        Doubles = CasinoRuleSheet.DoublesAny;
        Charlie = false;
        Peek = true;
        Sides = HostLadders.FloorIndex(HostLadders.DiceSides, VenueRules.DefaultSides);
        HighestWins = true;
        RoundSeconds = Math.Max(0, Array.IndexOf(HostLadders.RoundSeconds, VenueRules.DefaultRoundSeconds));
        StartAt = HostLadders.FloorIndex(HostLadders.StartNumbers, VenueRules.DefaultStartAt);
        ResetStakes();
    }

    public void SelectGame(HostGame game, bool gilOpen)
    {
        if (game == Game)
        {
            return;
        }

        var name = Name;
        var listing = Listing;
        var currency = Currency;
        Reset(game);
        Name = name;
        Listing = listing;
        Currency = HostGames.FallbackCurrency(game, currency, gilOpen);
        Seats = Math.Min(Seats, HostGames.MaxSeats(game));
        ResetStakes();
    }

    public void SelectCurrency(int currency, bool gilOpen)
    {
        if (currency == Currency || !HostGames.Accepts(Game, currency, gilOpen))
        {
            return;
        }

        Currency = currency;
        ResetStakes();
    }

    public void Normalize(bool gilOpen)
    {
        var fallback = HostGames.FallbackCurrency(Game, Currency, gilOpen);
        if (fallback != Currency)
        {
            Currency = fallback;
            ResetStakes();
        }

        Seats = Math.Clamp(Seats, CasinoHostingRules.MinSeats, HostGames.MaxSeats(Game));
        var span = BetSpan;
        MaxBet = span.Clamp(MaxBet);
        MinBet = Math.Clamp(MinBet, span.First, MaxBet);
        Stake = span.Clamp(Stake);
        MaxPayout = PayoutSpan.Clamp(MaxPayout);
        BigBlind = HostLadders.BigBlinds.Clamp(BigBlind);
    }

    public CasinoTableConfigDto Build(CasinoTableLocationDto? location)
    {
        if (Game == HostGame.Holdem)
        {
            return BuildHoldem(location);
        }

        return HostGames.IsVenue(Game) ? BuildVenue(location) : BuildBlackjack(location);
    }

    private void ResetStakes()
    {
        var rungs = BetRungs;
        if (Gil)
        {
            MinBet = HostLadders.FloorIndex(rungs, DefaultGilMinBet);
            MaxBet = HostLadders.FloorIndex(rungs, DefaultGilMaxBet);
            Stake = HostLadders.FloorIndex(rungs, VenueRules.DefaultStake);
            MaxPayout = Bank;
            PayoutCapped = false;
            return;
        }

        MinBet = HostLadders.FloorIndex(rungs, BlackjackRules.MinBet);
        MaxBet = HostLadders.FloorIndex(rungs, CasinoHostingRules.DefaultChipMaxBet);
        Stake = HostLadders.FloorIndex(rungs, VenueRules.DefaultStake);
    }

    private CasinoTableConfigDto BuildBlackjack(CasinoTableLocationDto? location)
    {
        var rules = SeatBanked
            ? new CasinoBlackjackRuleSheetDto(Pays, HitsSoft17, Decks, Splits, Doubles, Charlie, Peek)
            : null;
        return new CasinoTableConfigDto(
            GameKind: CasinoWire.BlackjackKind,
            Name: Name.Trim(),
            Seats: Seats,
            MinBet: MinBetValue,
            MaxBet: MaxBetValue,
            MinBuyIn: 0,
            MaxBuyIn: 0,
            Practice: Practice,
            PracticeStack: Practice ? StackValue : CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy: Practice && PracticeRebuy,
            TurnSeconds: TurnSeconds,
            TimeBankUses: TimeBank ? CasinoHostingRules.TimeBankUses : CasinoHostingRules.TimeBankOff,
            Listing: Listing,
            Spectators: Spectators,
            FaceUp: Practice && FaceUp,
            DealerMode: SeatBanked ? DealerMode : CasinoDealerModes.House,
            CoDealers: null,
            AutoDeal: !SeatBanked || DealerMode == CasinoDealerModes.House || AutoDeal,
            HouseRules: rules,
            Location: location,
            Currency: Currency,
            Bank: Gil ? BankValue : 0,
            MaxPayout: Gil ? PayoutValue : 0);
    }

    private CasinoTableConfigDto BuildHoldem(CasinoTableLocationDto? location)
    {
        var bigBlind = BigBlindValue;
        return new CasinoTableConfigDto(
            GameKind: HoldemRules.Kind,
            Name: Name.Trim(),
            Seats: Seats,
            MinBuyIn: 0,
            MaxBuyIn: 0,
            Practice: Practice,
            PracticeStack: Practice ? Math.Max(StackValue, bigBlind * HoldemRules.HostedMinBuyInBigBlinds)
                : CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy: Practice && PracticeRebuy,
            TurnSeconds: TurnSeconds,
            TimeBankUses: TimeBank ? CasinoHostingRules.TimeBankUses : CasinoHostingRules.TimeBankOff,
            Listing: Listing,
            Spectators: Spectators,
            FaceUp: Practice && FaceUp,
            Location: location,
            Poker: new CasinoPokerTableOptionsDto(bigBlind / 2, bigBlind * HostLadders.AnteTenths[Ante] / 10),
            Currency: Practice ? CasinoCurrencies.Practice : CasinoCurrencies.Chips);
    }

    private CasinoTableConfigDto BuildVenue(CasinoTableLocationDto? location)
    {
        var kind = HostGames.Of(Game).Venue;
        return new CasinoTableConfigDto(
            GameKind: VenueKinds.WireKind(kind),
            Name: Name.Trim(),
            Seats: 0,
            Practice: Practice,
            PracticeStack: Practice ? StackValue : CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy: false,
            Listing: Listing,
            Spectators: true,
            Location: location,
            Dice: kind == VenueRoomKind.Dice
                ? new CasinoDiceTableOptionsDto(SidesValue, HighestWins, HostLadders.RoundSeconds[RoundSeconds])
                : null,
            Deathroll: kind == VenueRoomKind.Deathroll ? new CasinoDeathrollOptionsDto(StartAtValue, StakeValue) : null,
            Currency: Gil ? CasinoCurrencies.Gil : CasinoCurrencies.Practice);
    }
}
