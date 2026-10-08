using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum VenueRoomKind : byte
{
    None,
    Dice,
    Deathroll,
    Raffle,
}

internal static class VenueKinds
{
    public const string DiceTable = "casino.dice-table";

    public const string Deathroll = "casino.deathroll";

    public const string Raffle = "casino.raffle";

    public static readonly string[] All = { DiceTable, Deathroll, Raffle };

    public static VenueRoomKind Of(string gameKind)
    {
        if (string.Equals(gameKind, DiceTable, StringComparison.Ordinal))
        {
            return VenueRoomKind.Dice;
        }

        if (string.Equals(gameKind, Deathroll, StringComparison.Ordinal))
        {
            return VenueRoomKind.Deathroll;
        }

        return string.Equals(gameKind, Raffle, StringComparison.Ordinal) ? VenueRoomKind.Raffle : VenueRoomKind.None;
    }

    public static bool IsVenue(string gameKind)
    {
        return Of(gameKind) != VenueRoomKind.None;
    }

    public static string WireKind(VenueRoomKind kind) => kind switch
    {
        VenueRoomKind.Dice => DiceTable,
        VenueRoomKind.Deathroll => Deathroll,
        VenueRoomKind.Raffle => Raffle,
        _ => string.Empty,
    };
}

internal static class VenueActions
{
    public const string Roll = "roll";

    public const string RoundOpen = "round.open";

    public const string DuelOpen = "duel.open";

    public const string DuelAccept = "duel.accept";

    public const string DuelRoll = "duel.roll";

    public const string DuelCancel = "duel.cancel";

    public const string RaffleOpen = "raffle.open";

    public const string RaffleTicket = "raffle.ticket";

    public const string RaffleDraw = "raffle.draw";
}

internal static class DuelPhases
{
    public const int Open = 0;

    public const int Live = 1;

    public const int Finished = 2;

    public const int Cancelled = 3;
}

internal static class VenueRules
{
    public const int MinSides = 2;

    public const int MaxSides = 1_000_000;

    public const int DefaultSides = 1000;

    public const int MinRoundSeconds = 15;

    public const int MaxRoundSeconds = 600;

    public const int DefaultRoundSeconds = 60;

    public const int MinStartAt = 2;

    public const int MaxStartAt = 1_000_000;

    public const int DefaultStartAt = 1000;

    public const long DefaultStake = 10_000;

    public const int DuelTurnSeconds = 60;

    public const int ChallengeLapseSeconds = 120;

    public const int MinRaffleSeconds = 60;

    public const int MaxRaffleSeconds = 86_400;

    public const int DefaultRaffleSeconds = 600;

    public const int MinTicketsPerPerson = 1;

    public const int MaxTicketsPerPerson = 100;

    public const int MinWinners = 1;

    public const int MaxWinners = 10;

    public const int MaxEntrants = 100;

    public const int TitleMaxLength = 48;

    public const int RollLogLimit = 50;

    public const int DuelHistoryLimit = 10;

    public const int ActsPerMinute = 60;

    public const long LosingRoll = 1;

    public static string Check(CasinoTableConfigDto config)
    {
        var kind = VenueKinds.Of(config.GameKind);
        if (kind == VenueRoomKind.None)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (config.Currency == CasinoCurrencies.Chips)
        {
            return CasinoReasons.PracticeOnly;
        }

        if (config.Currency != CasinoCurrencies.Practice && config.Currency != CasinoCurrencies.Gil)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (config.Name.Trim().Length > CasinoHostingRules.NameMaxLength
            || config.Listing < CasinoListings.Private || config.Listing > CasinoListings.Open)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return kind switch
        {
            VenueRoomKind.Dice => CheckDice(config.Dice),
            VenueRoomKind.Deathroll => CheckDeathroll(config),
            _ => string.Empty,
        };
    }

    public static bool IsSides(int sides)
    {
        return sides >= MinSides && sides <= MaxSides;
    }

    public static bool IsRoundSeconds(int seconds)
    {
        return seconds >= MinRoundSeconds && seconds <= MaxRoundSeconds;
    }

    public static bool IsStartAt(int startAt)
    {
        return startAt >= MinStartAt && startAt <= MaxStartAt;
    }

    public static bool IsRaffle(string title, int ticketsPerPerson, int winners, int durationSeconds)
    {
        var trimmed = title.Trim();
        return trimmed.Length >= 1 && trimmed.Length <= TitleMaxLength
            && ticketsPerPerson >= MinTicketsPerPerson && ticketsPerPerson <= MaxTicketsPerPerson
            && winners >= MinWinners && winners <= MaxWinners
            && durationSeconds >= MinRaffleSeconds && durationSeconds <= MaxRaffleSeconds;
    }

    public static bool Loses(long value)
    {
        return value == LosingRoll;
    }

    private static string CheckDice(CasinoDiceTableOptionsDto? dice)
    {
        var options = dice ?? new CasinoDiceTableOptionsDto();
        return IsSides(options.Sides) && IsRoundSeconds(options.RoundSeconds) ? string.Empty
            : CasinoReasons.ConfigInvalid;
    }

    private static string CheckDeathroll(CasinoTableConfigDto config)
    {
        var options = config.Deathroll ?? new CasinoDeathrollOptionsDto();
        if (!IsStartAt(options.StartAt) || options.Stake < 1)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (config.Currency == CasinoCurrencies.Practice && options.Stake > config.PracticeStack)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return config.Currency == CasinoCurrencies.Gil && options.Stake > CasinoHostingRules.MaxGil
            ? CasinoReasons.ConfigInvalid
            : string.Empty;
    }
}
