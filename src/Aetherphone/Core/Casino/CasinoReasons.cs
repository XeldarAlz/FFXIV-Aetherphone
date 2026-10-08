using System.Collections.Frozen;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino;

internal static class CasinoReasons
{
    public const string StakesPaused = "stakes_paused";
    public const string LossLimit = "loss_limit";
    public const string Draining = "draining";
    public const string Cooldown = "cooldown";
    public const string StakeRange = "stake_range";
    public const string BuyInRange = "buyin_range";

    public const string DailyBuyIn = "daily_buyin";
    public const string SittingOpen = "sitting_open";
    public const string Insufficient = "insufficient";
    public const string Frozen = "frozen";
    public const string Expired = "expired";
    public const string TableClosed = "table_closed";
    public const string RoundOpen = "round_open";
    public const string CapReached = "cap_reached";
    public const string Closed = "closed";
    public const string Locked = "locked";
    public const string NotRunning = "not_running";
    public const string StakeInvalid = "stake_invalid";
    public const string Pacing = "pacing";
    public const string Unavailable = "unavailable";
    public const string Ended = "ended";
    public const string Restarting = "restarting";
    public const string Unreachable = "unreachable";
    public const string AlreadyClaimed = "already_claimed";
    public const string Paused = "paused";
    public const string DailyCap = "daily_cap";
    public const string RuleCap = "rule_cap";
    public const string CardsFull = "cards_full";
    public const string SoldOut = "sold_out";
    public const string Full = "full";
    public const string InviteOnly = "private";
    public const string Denied = "denied";
    public const string KnockPending = "knock_pending";
    public const string BannedFromTable = "banned_from_table";
    public const string Blocked = "blocked";
    public const string AlreadyHosting = "already_hosting";
    public const string AlreadySeated = "already_seated";
    public const string SeatedElsewhere = "seated_elsewhere";
    public const string SeatTaken = "seat_taken";
    public const string NotSeated = "not_seated";
    public const string NotMember = "not_member";
    public const string NotYourTurn = "not_your_turn";
    public const string StaleAction = "stale_action";
    public const string StaleHand = "stale_hand";
    public const string HandOver = "hand_over";
    public const string InvalidAction = "invalid_action";
    public const string InvalidAmount = "invalid_amount";
    public const string InsufficientChips = "insufficient_chips";
    public const string TooLate = "too_late";
    public const string AtHandEnd = "at_hand_end";
    public const string Kicked = "kicked";
    public const string BoundElsewhere = "bound_elsewhere";
    public const string NoTables = "no_tables";
    public const string Ceiling = "ceiling";
    public const string Ladder = "ladder";
    public const string InvalidMove = "invalid_move";
    public const string BonusNotReady = "bonus_not_ready";
    public const string ClubInsufficient = "club_insufficient";
    public const string ConfigInvalid = "config_invalid";
    public const string PracticeOnly = "practice_only";
    public const string NotDealer = "not_dealer";
    public const string RebuyOff = "rebuy_off";
    public const string TournamentLive = "tournament_live";
    public const string NoTournament = "no_tournament";
    public const string NothingToDeal = "nothing_to_deal";
    public const string NoSpectators = "no_spectators";
    public const string DuelLive = "duel_live";
    public const string NoDuel = "no_duel";
    public const string RaffleLive = "raffle_live";
    public const string NoRaffle = "no_raffle";
    public const string TicketLimit = "ticket_limit";
    public const string RoundLive = "round_live";
    public const string BankLimit = "bank_limit";
    public const string HostFrozen = "host_frozen";
    public const string NotParty = "not_party";
    public const string AlreadyConfirmed = "already_confirmed";
    public const string Settled = "settled";
    public const string GilOnly = "gil_only";
    public const string MachineUnknown = "machine_unknown";

    public static readonly string[] All =
    {
        Ceiling,
        Ladder,
        BonusNotReady,
        ClubInsufficient,
        ConfigInvalid,
        PracticeOnly,
        NotDealer,
        RebuyOff,
        TournamentLive,
        NoTournament,
        NothingToDeal,
        NoSpectators,
        DuelLive,
        NoDuel,
        RaffleLive,
        NoRaffle,
        TicketLimit,
        RoundLive,
        BankLimit,
        HostFrozen,
        NotParty,
        AlreadyConfirmed,
        Settled,
        GilOnly,
        StakesPaused,
        LossLimit,
        Draining,
        Cooldown,
        StakeRange,
        BuyInRange,
        DailyBuyIn,
        SittingOpen,
        Insufficient,
        Frozen,
        Expired,
        TableClosed,
        RoundOpen,
        CapReached,
        Closed,
        Locked,
        NotRunning,
        StakeInvalid,
        Pacing,
        Unavailable,
        Ended,
        Restarting,
        Unreachable,
        AlreadyClaimed,
        Paused,
        DailyCap,
        RuleCap,
        CardsFull,
        SoldOut,
        Full,
        InviteOnly,
        Denied,
        KnockPending,
        BannedFromTable,
        Blocked,
        AlreadyHosting,
        AlreadySeated,
        SeatedElsewhere,
        SeatTaken,
        NotSeated,
        NotMember,
        NotYourTurn,
        StaleAction,
        StaleHand,
        HandOver,
        InvalidAction,
        InvalidAmount,
        InsufficientChips,
        TooLate,
        AtHandEnd,
        Kicked,
        BoundElsewhere,
        NoTables,
        InvalidMove,
        MachineUnknown,
    };

    private static readonly FrozenDictionary<string, LocString> Messages = new Dictionary<string, LocString>
    {
        [Ceiling] = L.Strip.ReasonCeiling,
        [Ladder] = L.Strip.ReasonLadder,
        [BonusNotReady] = L.Strip.ReasonBonusNotReady,
        [ClubInsufficient] = L.Strip.ReasonClubInsufficient,
        [ConfigInvalid] = L.Tables.ReasonConfigInvalid,
        [PracticeOnly] = L.Tables.ReasonPracticeOnly,
        [NotDealer] = L.Tables.ReasonNotDealer,
        [RebuyOff] = L.Tables.ReasonRebuyOff,
        [TournamentLive] = L.Tables.ReasonTournamentLive,
        [NoTournament] = L.Tables.ReasonNoTournament,
        [NothingToDeal] = L.Tables.ReasonNothingToDeal,
        [NoSpectators] = L.Tables.ReasonNoSpectators,
        [DuelLive] = L.Tables.ReasonDuelLive,
        [NoDuel] = L.Tables.ReasonNoDuel,
        [RaffleLive] = L.Tables.ReasonRaffleLive,
        [NoRaffle] = L.Tables.ReasonNoRaffle,
        [TicketLimit] = L.Tables.ReasonTicketLimit,
        [RoundLive] = L.Tables.ReasonRoundLive,
        [BankLimit] = L.Tables.ReasonBankLimit,
        [HostFrozen] = L.Tables.ReasonHostFrozen,
        [NotParty] = L.Tables.ReasonNotParty,
        [AlreadyConfirmed] = L.Tables.ReasonAlreadyConfirmed,
        [Settled] = L.Tables.ReasonSettled,
        [GilOnly] = L.Tables.ReasonGilOnly,
        [StakesPaused] = L.Casino.ReasonStakesPaused,
        [LossLimit] = L.Casino.ReasonLossLimit,
        [Draining] = L.Casino.ReasonDraining,
        [Cooldown] = L.Casino.ReasonCooldown,
        [StakeRange] = L.Casino.ReasonStakeRange,
        [BuyInRange] = L.Casino.ReasonBuyInRange,
        [DailyBuyIn] = L.Casino.ReasonDailyBuyIn,
        [SittingOpen] = L.Casino.ReasonSittingOpen,
        [Insufficient] = L.Casino.ReasonInsufficient,
        [Frozen] = L.Casino.ReasonFrozen,
        [Expired] = L.Casino.ReasonExpired,
        [TableClosed] = L.Casino.ReasonTableClosed,
        [RoundOpen] = L.Casino.ReasonRoundOpen,
        [CapReached] = L.Casino.ReasonCapReached,
        [Closed] = L.Casino.ReasonClosed,
        [Locked] = L.Casino.ReasonLocked,
        [NotRunning] = L.Casino.ReasonNotRunning,
        [StakeInvalid] = L.Casino.ReasonStakeInvalid,
        [Pacing] = L.Casino.ReasonPacing,
        [Unavailable] = L.Casino.ReasonUnavailable,
        [Ended] = L.Casino.ReasonEnded,
        [Restarting] = L.Casino.ReasonRestarting,
        [Unreachable] = L.Casino.ReasonUnreachable,
        [AlreadyClaimed] = L.Casino.ReasonClaimed,
        [Paused] = L.Casino.ReasonPaused,
        [DailyCap] = L.Casino.ReasonDailyCap,
        [RuleCap] = L.Casino.ReasonRuleCap,
        [CardsFull] = L.Casino.ReasonCardsFull,
        [SoldOut] = L.Casino.ReasonSoldOut,
        [Full] = L.Casino.ReasonFull,
        [InviteOnly] = L.Casino.ReasonInviteOnly,
        [Denied] = L.Casino.ReasonDenied,
        [KnockPending] = L.Casino.ReasonKnockPending,
        [BannedFromTable] = L.Casino.ReasonBannedFromTable,
        [Blocked] = L.Casino.ReasonBlocked,
        [AlreadyHosting] = L.Casino.ReasonAlreadyHosting,
        [AlreadySeated] = L.Casino.ReasonAlreadySeated,
        [SeatedElsewhere] = L.Casino.ReasonSeatedElsewhere,
        [SeatTaken] = L.Casino.ReasonSeatTaken,
        [NotSeated] = L.Casino.ReasonNotSeated,
        [NotMember] = L.Casino.ReasonNotMember,
        [NotYourTurn] = L.Casino.ReasonNotYourTurn,
        [StaleAction] = L.Casino.ReasonStaleAction,
        [StaleHand] = L.Casino.ReasonStaleHand,
        [HandOver] = L.Casino.ReasonHandOver,
        [InvalidAction] = L.Casino.ReasonInvalidAction,
        [InvalidAmount] = L.Casino.ReasonInvalidAmount,
        [InsufficientChips] = L.Casino.ReasonInsufficientChips,
        [TooLate] = L.Casino.ReasonTooLate,
        [AtHandEnd] = L.Casino.ReasonAtHandEnd,
        [Kicked] = L.Casino.ReasonKicked,
        [BoundElsewhere] = L.Casino.ReasonBoundElsewhere,
        [NoTables] = L.Casino.ReasonNoTables,
        [InvalidMove] = L.Originals.ReasonInvalidMove,
        [MachineUnknown] = L.Machines.ReasonMachineUnknown,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static long ceilingTextValue;
    private static LanguageInfo? ceilingTextLanguage;
    private static string ceilingText = string.Empty;

    public static bool TryMessage(string reason, out LocString message)
    {
        return Messages.TryGetValue(reason, out message);
    }

    public static LocString MessageFor(string reason)
    {
        return Messages.TryGetValue(reason, out var message) ? message : L.Casino.ReasonGeneric;
    }

    public static string Text(string reason, long ceiling)
    {
        if (ceiling <= 0 || !string.Equals(reason, Ceiling, StringComparison.Ordinal))
        {
            return Loc.T(MessageFor(reason));
        }

        if (ceiling == ceilingTextValue && ReferenceEquals(Loc.Current, ceilingTextLanguage))
        {
            return ceilingText;
        }

        ceilingTextValue = ceiling;
        ceilingTextLanguage = Loc.Current;
        ceilingText = Loc.T(L.Strip.ReasonCeilingAt, NumberText.Compact(ceiling));
        return ceilingText;
    }
}
