using System.Text;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct TradeWindowSample(
    bool Open,
    string[] NameCandidates,
    long MyOffer,
    long TheirOffer,
    long WalletGil);

internal readonly record struct TradeCompletion(string[] NameCandidates, long Received, long Sent)
{
    public long Net => Received - Sent;
}

internal readonly record struct TradeSeat(string UserId, string DisplayName);

internal readonly record struct TradeTableContext(
    string TableId,
    string MyUserId,
    string HostUserId,
    string HostName,
    TradeSeat[] Seats,
    CasinoLedgerEntryDto[] Entries)
{
    public bool Known => !string.IsNullOrEmpty(TableId) && !string.IsNullOrEmpty(MyUserId)
        && !string.IsNullOrEmpty(HostUserId);

    public bool Hosting => Known && string.Equals(MyUserId, HostUserId, StringComparison.Ordinal);
}

internal enum TradeProposalKind : byte
{
    None,
    Propose,
    Confirm,
}

internal readonly record struct TradeProposal(
    TradeProposalKind Kind,
    string TableId,
    string LedgerKind,
    string CounterpartyUserId,
    string CounterpartyName,
    long Amount,
    string EntryId,
    bool HostSide,
    bool Incoming)
{
    public static readonly TradeProposal None = new(TradeProposalKind.None, string.Empty, string.Empty, string.Empty,
        string.Empty, 0, string.Empty, false, false);
}

internal enum TradeTrackerStage : byte
{
    Idle,
    Open,
    Settling,
}

internal sealed class TradeSyncTracker
{
    public const long SettleWindowMilliseconds = 4_000;

    private string[] candidates = Array.Empty<string>();
    private long myOffer;
    private long theirOffer;
    private long baseline;
    private long closedAtTick;

    public TradeTrackerStage Stage { get; private set; }

    public TradeCompletion? Observe(in TradeWindowSample sample, long nowTick)
    {
        if (sample.Open)
        {
            if (Stage != TradeTrackerStage.Open)
            {
                candidates = Array.Empty<string>();
                myOffer = 0;
                theirOffer = 0;
            }

            Stage = TradeTrackerStage.Open;
            baseline = sample.WalletGil;
            if (sample.NameCandidates.Length > 0)
            {
                candidates = sample.NameCandidates;
            }

            myOffer = Math.Max(0, sample.MyOffer);
            theirOffer = Math.Max(0, sample.TheirOffer);
            return null;
        }

        if (Stage == TradeTrackerStage.Open)
        {
            Stage = TradeTrackerStage.Settling;
            closedAtTick = nowTick;
        }

        if (Stage != TradeTrackerStage.Settling)
        {
            return null;
        }

        var delta = sample.WalletGil - baseline;
        if (delta != 0)
        {
            Stage = TradeTrackerStage.Idle;
            return Settle(delta);
        }

        if (nowTick - closedAtTick > SettleWindowMilliseconds)
        {
            Stage = TradeTrackerStage.Idle;
        }

        return null;
    }

    public void Reset()
    {
        Stage = TradeTrackerStage.Idle;
        candidates = Array.Empty<string>();
        myOffer = 0;
        theirOffer = 0;
        baseline = 0;
        closedAtTick = 0;
    }

    private TradeCompletion Settle(long delta)
    {
        if (theirOffer - myOffer == delta)
        {
            return new TradeCompletion(candidates, theirOffer, myOffer);
        }

        if (myOffer - theirOffer == delta)
        {
            return new TradeCompletion(candidates, myOffer, theirOffer);
        }

        return delta > 0
            ? new TradeCompletion(candidates, delta, 0)
            : new TradeCompletion(candidates, 0, -delta);
    }
}

internal static class TradeLedgerMatcher
{
    public static TradeProposal Match(in TradeCompletion completion, in TradeTableContext context)
    {
        var net = completion.Net;
        if (!context.Known || net == 0)
        {
            return TradeProposal.None;
        }

        return context.Hosting ? MatchHost(completion, context, net) : MatchPlayer(completion, context, net);
    }

    private static TradeProposal MatchHost(in TradeCompletion completion, in TradeTableContext context, long net)
    {
        var seat = SeatNamed(context.Seats, completion.NameCandidates, context.MyUserId);
        if (seat.UserId.Length == 0)
        {
            return TradeProposal.None;
        }

        return Resolve(context, seat, net, true);
    }

    private static TradeProposal MatchPlayer(in TradeCompletion completion, in TradeTableContext context, long net)
    {
        if (!AnyNamed(completion.NameCandidates, context.HostName))
        {
            return TradeProposal.None;
        }

        return Resolve(context, new TradeSeat(context.HostUserId, context.HostName), net, false);
    }

    private static TradeProposal Resolve(in TradeTableContext context, TradeSeat counterparty, long net, bool hostSide)
    {
        var incoming = net > 0;
        var amount = incoming ? net : -net;
        var payer = incoming ? counterparty.UserId : context.MyUserId;
        var payee = incoming ? context.MyUserId : counterparty.UserId;
        var entry = Unsettled(context.Entries, payer, payee, amount, incoming ? LedgerSide.Payee : LedgerSide.Payer);
        if (entry is not null)
        {
            return new TradeProposal(TradeProposalKind.Confirm, context.TableId, entry.Kind, counterparty.UserId,
                counterparty.DisplayName, amount, entry.EntryId, hostSide, incoming);
        }

        var kind = incoming == hostSide ? CasinoLedgerKinds.BuyIn : CasinoLedgerKinds.Payout;
        return new TradeProposal(TradeProposalKind.Propose, context.TableId, kind, counterparty.UserId,
            counterparty.DisplayName, amount, string.Empty, hostSide, incoming);
    }

    internal static CasinoLedgerEntryDto? Unsettled(CasinoLedgerEntryDto[] entries, string payer, string payee,
        long amount, LedgerSide mySide)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.Settled || entry.Amount != amount
                || !string.Equals(entry.PayerUserId, payer, StringComparison.Ordinal)
                || !string.Equals(entry.PayeeUserId, payee, StringComparison.Ordinal))
            {
                continue;
            }

            var stamped = mySide == LedgerSide.Payer ? entry.PayerConfirmed : entry.PayeeConfirmed;
            if (!stamped)
            {
                return entry;
            }
        }

        return null;
    }

    internal static TradeSeat SeatNamed(TradeSeat[] seats, string[] candidates, string excludedUserId)
    {
        for (var seatIndex = 0; seatIndex < seats.Length; seatIndex++)
        {
            var seat = seats[seatIndex];
            if (seat.UserId.Length == 0 || string.Equals(seat.UserId, excludedUserId, StringComparison.Ordinal))
            {
                continue;
            }

            if (AnyNamed(candidates, seat.DisplayName))
            {
                return seat;
            }
        }

        return new TradeSeat(string.Empty, string.Empty);
    }

    internal static bool AnyNamed(string[] candidates, string displayName)
    {
        var wanted = TradeNames.Normalize(displayName);
        if (wanted.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < candidates.Length; index++)
        {
            if (string.Equals(TradeNames.Normalize(candidates[index]), wanted, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class TradeNames
{
    private const char PrivateUseFirst = '\uE000';
    private const char PrivateUseLast = '\uF8FF';
    private const char NoBreakSpace = '\u00A0';
    private const char NarrowNoBreakSpace = '\u202F';
    private const int MaxAmountDigits = 12;

    public static string Normalize(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        var pendingSpace = false;
        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (character is >= PrivateUseFirst and <= PrivateUseLast or '@' or '(' or '[')
            {
                break;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (!char.IsLetter(character) && character != '\'' && character != '-')
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static bool TryAmount(string text, out long amount)
    {
        amount = 0;
        var digits = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is >= '0' and <= '9')
            {
                digits++;
                if (digits > MaxAmountDigits)
                {
                    amount = 0;
                    return false;
                }

                amount = amount * 10 + (character - '0');
                continue;
            }

            if (character is ',' or '.' or ' ' or '\'' or NoBreakSpace or NarrowNoBreakSpace)
            {
                continue;
            }

            amount = 0;
            return false;
        }

        return digits > 0;
    }
}
