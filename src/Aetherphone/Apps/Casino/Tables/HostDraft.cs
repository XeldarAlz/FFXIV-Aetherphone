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
    public bool AutoDeal;
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

    public LadderSpan PayoutSpan => new(Game == HostGame.Book ? 0 : MaxBet, Bank);

    public void Reset(HostGame game)
    {
        Game = game;
        Currency = HostGames.DefaultCurrency(game);
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
        AutoDeal = false;
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
        if (Gil)
        {
            MaxPayout = PayoutSpan.Clamp(MaxPayout);
        }

        BigBlind = HostLadders.BigBlinds.Clamp(BigBlind);
    }

    public CasinoTableConfigDto Build(CasinoTableLocationDto? location)
    {
        if (Game == HostGame.Holdem)
        {
            return BuildHoldem(location);
        }

        if (Game == HostGame.Book)
        {
            return BuildBook(location);
        }

        return HostGames.IsVenue(Game) ? BuildVenue(location) : BuildBlackjack(location);
    }

    public CasinoHostDraftMemory Snapshot(bool pinLocation) => new()
    {
        Game = (int)Game,
        Currency = Currency,
        Seats = Seats,
        Listing = Listing,
        Spectators = Spectators,
        FaceUp = FaceUp,
        Name = Name,
        MinBet = MinBet,
        MaxBet = MaxBet,
        BigBlind = BigBlind,
        Ante = Ante,
        Stack = Stack,
        PracticeRebuy = PracticeRebuy,
        Bank = Bank,
        MaxPayout = MaxPayout,
        PayoutCapped = PayoutCapped,
        TurnSeconds = TurnSeconds,
        TimeBank = TimeBank,
        DealerMode = DealerMode,
        AutoDeal = AutoDeal,
        Pays = Pays,
        HitsSoft17 = HitsSoft17,
        Decks = Decks,
        Splits = Splits,
        Doubles = Doubles,
        Charlie = Charlie,
        Peek = Peek,
        Sides = Sides,
        HighestWins = HighestWins,
        RoundSeconds = RoundSeconds,
        StartAt = StartAt,
        Stake = Stake,
        PinLocation = pinLocation,
    };

    public static HostGame GameOf(CasinoHostDraftMemory memory) =>
        (HostGame)Math.Clamp(memory.Game, 0, HostGames.All.Length - 1);

    public static bool Reopens(CasinoHostDraftMemory memory, bool bookOpen) =>
        bookOpen || !HostGames.IsBook(GameOf(memory));

    public void Restore(CasinoHostDraftMemory memory)
    {
        Reset(GameOf(memory));
        Currency = Math.Clamp(memory.Currency, CasinoCurrencies.Chips, CasinoCurrencies.Gil);
        Seats = memory.Seats;
        Listing = Math.Clamp(memory.Listing, 0, CasinoListings.All.Length - 1);
        Spectators = memory.Spectators;
        FaceUp = memory.FaceUp;
        Name = memory.Name;
        var rungs = BetRungs;
        MinBet = LastIndexAtMost(memory.MinBet, rungs.Length);
        MaxBet = LastIndexAtMost(memory.MaxBet, rungs.Length);
        BigBlind = LastIndexAtMost(memory.BigBlind, HostLadders.Bets.Length);
        Ante = LastIndexAtMost(memory.Ante, HostLadders.AnteTenths.Length);
        Stack = LastIndexAtMost(memory.Stack, HostLadders.PracticeStacks.Length);
        PracticeRebuy = memory.PracticeRebuy;
        Bank = LastIndexAtMost(memory.Bank, HostLadders.Gil.Length);
        MaxPayout = LastIndexAtMost(memory.MaxPayout, HostLadders.Gil.Length);
        PayoutCapped = memory.PayoutCapped;
        TurnSeconds = memory.TurnSeconds;
        TimeBank = memory.TimeBank;
        DealerMode = Math.Clamp(memory.DealerMode, CasinoDealerModes.House, CasinoDealerModes.Host);
        AutoDeal = memory.AutoDeal;
        Pays = Math.Clamp(memory.Pays, CasinoRuleSheet.PaysThreeToTwo, CasinoRuleSheet.PaysEven);
        HitsSoft17 = memory.HitsSoft17;
        Decks = memory.Decks;
        Splits = Math.Clamp(memory.Splits, CasinoRuleSheet.SplitsOff, CasinoRuleSheet.SplitsToFour);
        Doubles = Math.Clamp(memory.Doubles, CasinoRuleSheet.DoublesAny, CasinoRuleSheet.DoublesOff);
        Charlie = memory.Charlie;
        Peek = memory.Peek;
        Sides = LastIndexAtMost(memory.Sides, HostLadders.DiceSides.Length);
        HighestWins = memory.HighestWins;
        RoundSeconds = LastIndexAtMost(memory.RoundSeconds, HostLadders.RoundSeconds.Length);
        StartAt = LastIndexAtMost(memory.StartAt, HostLadders.StartNumbers.Length);
        Stake = LastIndexAtMost(memory.Stake, rungs.Length);
    }

    private static int LastIndexAtMost(int index, int length) => Math.Clamp(index, 0, length - 1);

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

    private CasinoTableConfigDto BuildBook(CasinoTableLocationDto? location)
    {
        return new CasinoTableConfigDto(
            GameKind: CasinoWire.BookKind,
            Name: Name.Trim(),
            Seats: 0,
            Practice: false,
            PracticeStack: CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy: false,
            TurnSeconds: CasinoHostingRules.DefaultTurnSeconds,
            TimeBankUses: CasinoHostingRules.TimeBankUses,
            Listing: Listing,
            Spectators: false,
            FaceUp: false,
            DealerMode: CasinoDealerModes.House,
            CoDealers: null,
            AutoDeal: false,
            HouseRules: null,
            Location: location,
            Currency: CasinoCurrencies.Gil,
            Bank: BankValue,
            MaxPayout: PayoutValue);
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
