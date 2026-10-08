using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class CasinoCurrencies
{
    public const int Chips = 0;

    public const int Practice = 1;

    public const int Gil = 2;

    public static readonly int[] All = { Chips, Practice, Gil };

    public static int Of(CasinoTableRowDto row)
    {
        if (row.Currency == Gil || row.Config?.Currency == Gil)
        {
            return Gil;
        }

        return row.Practice || row.Currency == Practice ? Practice : Chips;
    }

    public static int Of(CasinoBlackjackRoomStateDto board)
    {
        if (board.Currency == Gil)
        {
            return Gil;
        }

        return board.Practice || board.Currency == Practice ? Practice : Chips;
    }

    public static bool SeatBanked(int currency)
    {
        return currency == Practice || currency == Gil;
    }
}

internal static class CasinoListings
{
    public const int Private = 0;

    public const int Knock = 1;

    public const int Open = 2;

    public static readonly int[] All = { Private, Knock, Open };
}

internal static class CasinoDealerModes
{
    public const int House = 0;

    public const int Host = 1;
}

internal static class CasinoRuleSheet
{
    public const int PaysThreeToTwo = 0;

    public const int PaysTwoToOne = 1;

    public const int PaysEven = 2;

    public const int SplitsOff = 0;

    public const int SplitsOnce = 1;

    public const int SplitsToFour = 2;

    public const int DoublesAny = 0;

    public const int DoublesNineToEleven = 1;

    public const int DoublesOff = 2;

    public static readonly int[] Pays = { PaysThreeToTwo, PaysTwoToOne, PaysEven };

    public static readonly int[] Decks = { 1, 2, 4, 6, 8 };

    public static readonly int[] Splits = { SplitsOff, SplitsOnce, SplitsToFour };

    public static readonly int[] Doubles = { DoublesAny, DoublesNineToEleven, DoublesOff };

    public static readonly CasinoBlackjackRuleSheetDto Standard = new();

    public static CasinoBlackjackRuleSheetDto Of(CasinoBlackjackRoomStateDto board)
    {
        return board.Rules ?? Standard;
    }

    public static bool IsStandard(CasinoBlackjackRuleSheetDto rules)
    {
        return rules.BlackjackPays == Standard.BlackjackPays
            && rules.DealerHitsSoft17 == Standard.DealerHitsSoft17
            && rules.Decks == Standard.Decks
            && rules.Splits == Standard.Splits
            && rules.Doubles == Standard.Doubles
            && rules.FiveCardCharlie == Standard.FiveCardCharlie
            && rules.DealerPeek == Standard.DealerPeek;
    }

    public static long NaturalPayout(int pays, long bet)
    {
        if (bet <= 0)
        {
            return 0;
        }

        return pays switch
        {
            PaysTwoToOne => bet * 2,
            PaysEven => bet,
            _ => (bet * 3 + 1) / 2,
        };
    }

    public static bool IsDeckCount(int decks)
    {
        return Array.IndexOf(Decks, decks) >= 0;
    }
}

internal static class CasinoHostingRules
{
    public const int MinSeats = 2;

    public const int MaxSeats = 6;

    public const int DefaultSeats = 6;

    public const int NameMaxLength = 24;

    public const long MinPracticeStack = 1_000;

    public const long MaxPracticeStack = 1_000_000_000;

    public const long DefaultPracticeStack = 100_000;

    public const long MinGil = 1;

    public const long MaxGil = 99_999_999_999;

    public const int DefaultTurnSeconds = 20;

    public const long DefaultChipMaxBet = 10_000;

    public const long ChipMinBuyIn = 20 * CasinoChipLots.ChipPerCoin;

    public const long ChipMaxBuyIn = 5_000 * CasinoChipLots.ChipPerCoin;

    public const int TimeBankOff = 0;

    public const int TimeBankUses = 3;

    public const int TimeBankSecondsPerUse = 10;

    public const int MaxCoDealers = 3;

    public const int TournamentMinHands = 10;

    public const int TournamentMaxHands = 50;

    public const int CreateCooldownSeconds = 60;

    public const int GilLedgerDays = 30;

    public const int DisputesToFreeze = 3;

    public const long DefaultHoldemBigBlind = 100;

    public const long MinHoldemBigBlind = 100;

    public static readonly int[] TurnSeconds = { 15, 20, 30, 45 };

    public static bool IsTurnSeconds(int seconds)
    {
        return Array.IndexOf(TurnSeconds, seconds) >= 0;
    }

    public static string Check(CasinoTableConfigDto config)
    {
        if (string.Equals(config.GameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return CheckHoldem(config);
        }

        if (config.Seats < MinSeats || config.Seats > MaxSeats || !IsTurnSeconds(config.TurnSeconds)
            || (config.TimeBankUses != TimeBankOff && config.TimeBankUses != TimeBankUses)
            || config.Name.Trim().Length > NameMaxLength
            || config.Listing < CasinoListings.Private || config.Listing > CasinoListings.Open)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if ((config.CoDealers?.Length ?? 0) > MaxCoDealers)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return config.Currency switch
        {
            CasinoCurrencies.Practice => CheckPractice(config),
            CasinoCurrencies.Gil => CheckGil(config),
            CasinoCurrencies.Chips => CheckChips(config),
            _ => CasinoReasons.ConfigInvalid,
        };
    }

    public static string CheckHoldem(CasinoTableConfigDto config)
    {
        var poker = config.Poker;
        if (poker is null || config.Seats < HoldemRules.MinSeats || config.Seats > HoldemRules.MaxSeats
            || !IsTurnSeconds(config.TurnSeconds)
            || (config.TimeBankUses != TimeBankOff && config.TimeBankUses != TimeBankUses)
            || config.Name.Trim().Length > NameMaxLength
            || config.Listing < CasinoListings.Private || config.Listing > CasinoListings.Open
            || config.Currency is not (CasinoCurrencies.Chips or CasinoCurrencies.Practice)
            || config.DealerMode != CasinoDealerModes.House || (config.CoDealers?.Length ?? 0) > 0 || !config.AutoDeal
            || config.HouseRules is not null)
        {
            return CasinoReasons.ConfigInvalid;
        }

        var bigBlind = poker.SmallBlind * 2;
        if (poker.SmallBlind < MinHoldemBigBlind / 2 || !CasinoLadder.IsRung(bigBlind) || poker.Ante < 0
            || poker.Ante > bigBlind || poker.Straddle != 0 || poker.RunItTwice || poker.BombPotAnteBb != 0
            || poker.BombPotPercent != 0 || poker.SevenDeuceBounty != 0)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (config.Currency == CasinoCurrencies.Practice)
        {
            return config.PracticeStack < bigBlind * HoldemRules.HostedMinBuyInBigBlinds
                || config.PracticeStack > MaxPracticeStack
                ? CasinoReasons.ConfigInvalid
                : string.Empty;
        }

        if (config.FaceUp)
        {
            return CasinoReasons.PracticeOnly;
        }

        var minBuyIn = config.MinBuyIn == 0 ? bigBlind * HoldemRules.MinBuyInBigBlinds : config.MinBuyIn;
        var maxBuyIn = config.MaxBuyIn == 0 ? bigBlind * HoldemRules.MaxBuyInBigBlinds : config.MaxBuyIn;
        if (minBuyIn < bigBlind * HoldemRules.HostedMinBuyInBigBlinds
            || maxBuyIn > bigBlind * HoldemRules.HostedMaxBuyInBigBlinds || minBuyIn > maxBuyIn)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return string.Empty;
    }

    public static long PayoutCeiling(CasinoTableConfigDto config)
    {
        return config.MaxPayout > 0 ? config.MaxPayout : config.Bank;
    }

    private static string CheckChips(CasinoTableConfigDto config)
    {
        if (config.FaceUp || config.DealerMode != CasinoDealerModes.House || (config.CoDealers?.Length ?? 0) > 0
            || !config.AutoDeal
            || (config.HouseRules is not null && !CasinoRuleSheet.IsStandard(config.HouseRules)))
        {
            return CasinoReasons.PracticeOnly;
        }

        var minBet = config.MinBet == 0 ? BlackjackRules.MinBet : config.MinBet;
        var maxBet = config.MaxBet == 0 ? DefaultChipMaxBet : config.MaxBet;
        var minBuyIn = config.MinBuyIn == 0 ? ChipMinBuyIn : config.MinBuyIn;
        var maxBuyIn = config.MaxBuyIn == 0 ? ChipMaxBuyIn : config.MaxBuyIn;
        if (minBet < BlackjackRules.MinBet || minBet > maxBet || !CasinoLadder.IsRung(minBet)
            || !CasinoLadder.IsRung(maxBet) || minBuyIn < ChipMinBuyIn || maxBuyIn > ChipMaxBuyIn
            || minBuyIn > maxBuyIn)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return string.Empty;
    }

    private static string CheckPractice(CasinoTableConfigDto config)
    {
        if (config.PracticeStack < MinPracticeStack || config.PracticeStack > MaxPracticeStack)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (config.MinBet < 0 || config.MaxBet < 0 || config.MaxBet > config.PracticeStack
            || (config.MaxBet > 0 && config.MinBet > config.MaxBet))
        {
            return CasinoReasons.ConfigInvalid;
        }

        return CheckRules(config.HouseRules);
    }

    private static string CheckGil(CasinoTableConfigDto config)
    {
        if (config.FaceUp)
        {
            return CasinoReasons.PracticeOnly;
        }

        if (config.Bank < MinGil || config.Bank > MaxGil || config.MaxBet < MinGil || config.MaxBet > config.Bank
            || config.MinBet < 0 || config.MinBet > config.MaxBet)
        {
            return CasinoReasons.ConfigInvalid;
        }

        var payout = PayoutCeiling(config);
        if (payout < config.MaxBet || payout > config.Bank)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return CheckRules(config.HouseRules);
    }

    private static string CheckRules(CasinoBlackjackRuleSheetDto? rules)
    {
        if (rules is null)
        {
            return string.Empty;
        }

        if (Array.IndexOf(CasinoRuleSheet.Pays, rules.BlackjackPays) < 0
            || !CasinoRuleSheet.IsDeckCount(rules.Decks)
            || Array.IndexOf(CasinoRuleSheet.Splits, rules.Splits) < 0
            || Array.IndexOf(CasinoRuleSheet.Doubles, rules.Doubles) < 0)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return string.Empty;
    }
}

internal static class CasinoLedgerKinds
{
    public const string BuyIn = "buyin";

    public const string Rebuy = "rebuy";

    public const string Payout = "payout";

    public const string CashOut = "cashout";

    public const string Stake = "stake";

    public const string Ticket = "ticket";

    public const string Prize = "prize";

    public const string Manual = "manual";

    public const string Trade = "trade";

    public static bool Proposable(string kind)
    {
        return string.Equals(kind, BuyIn, StringComparison.Ordinal)
            || string.Equals(kind, Rebuy, StringComparison.Ordinal)
            || string.Equals(kind, Payout, StringComparison.Ordinal);
    }
}

internal enum LedgerSide : byte
{
    None,
    Payer,
    Payee,
}

internal static class CasinoGilLedger
{
    public static LedgerSide SideOf(CasinoLedgerEntryDto entry, string userId)
    {
        if (userId.Length == 0)
        {
            return LedgerSide.None;
        }

        if (string.Equals(entry.PayerUserId, userId, StringComparison.Ordinal))
        {
            return LedgerSide.Payer;
        }

        return string.Equals(entry.PayeeUserId, userId, StringComparison.Ordinal) ? LedgerSide.Payee : LedgerSide.None;
    }

    public static bool Unconfirmed(CasinoLedgerEntryDto entry)
    {
        return !entry.Settled;
    }

    public static bool CanConfirm(CasinoLedgerEntryDto entry, string userId)
    {
        if (entry.Settled)
        {
            return false;
        }

        return SideOf(entry, userId) switch
        {
            LedgerSide.Payer => !entry.PayerConfirmed,
            LedgerSide.Payee => !entry.PayeeConfirmed,
            _ => false,
        };
    }

    public static bool CanDispute(CasinoLedgerEntryDto entry, string userId)
    {
        return !entry.Settled && !entry.Disputed && SideOf(entry, userId) == LedgerSide.Payee;
    }

    public static bool WaitsOnOtherSide(CasinoLedgerEntryDto entry, string userId)
    {
        if (entry.Settled)
        {
            return false;
        }

        return SideOf(entry, userId) switch
        {
            LedgerSide.Payer => entry.PayerConfirmed && !entry.PayeeConfirmed,
            LedgerSide.Payee => entry.PayeeConfirmed && !entry.PayerConfirmed,
            _ => false,
        };
    }

    public static int UnsettledCount(CasinoLedgerEntryDto[]? entries)
    {
        if (entries is null)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (!entries[index].Settled)
            {
                count++;
            }
        }

        return count;
    }
}
