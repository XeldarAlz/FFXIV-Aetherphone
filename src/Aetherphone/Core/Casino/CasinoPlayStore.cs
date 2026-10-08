using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoPlayStore : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore store;
    private readonly StoreWork work = new("CasinoPlay");
    private readonly object pendingRoundsSwapLock = new();

    private volatile bool roundInFlight;
    private CasinoSlotsSpinDto? spinResult;
    private CasinoScratchCardDto? scratchResult;
    private CasinoBarkeepStartDto? barkeepStartResult;
    private CasinoBarkeepFinishDto? barkeepFinishResult;
    private int roundFailed;

    public CasinoPlayStore(Configuration configuration, AethernetSession session, CasinoClient casino,
        CasinoStore store)
    {
        this.configuration = configuration;
        this.session = session;
        this.casino = casino;
        this.store = store;
        Originals = new CasinoOriginalsStore(session, casino, store);
        Plinko = new CasinoPlinkoStore(session, casino, store);
    }

    public CasinoOriginalsStore Originals { get; }

    public CasinoPlinkoStore Plinko { get; }

    public bool RoundInFlight => roundInFlight;

    public PendingCasinoRound? PendingRound
    {
        get
        {
            var contentId = session.ActiveContentId;
            if (contentId == 0)
            {
                return null;
            }

            var snapshot = configuration.PendingCasinoRounds;
            return snapshot.TryGetValue(contentId, out var pending) ? pending : null;
        }
    }

    public CasinoSlotsSpinDto? TakeSpinResult()
    {
        return Interlocked.Exchange(ref spinResult, null);
    }

    public CasinoScratchCardDto? TakeScratchResult()
    {
        return Interlocked.Exchange(ref scratchResult, null);
    }

    public CasinoBarkeepStartDto? TakeBarkeepStart()
    {
        return Interlocked.Exchange(ref barkeepStartResult, null);
    }

    public CasinoBarkeepFinishDto? TakeBarkeepFinish()
    {
        return Interlocked.Exchange(ref barkeepFinishResult, null);
    }

    public bool TakeRoundFailure()
    {
        return Interlocked.Exchange(ref roundFailed, 0) != 0;
    }

    public void SpinSlots(long stake)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || !SlotsRules.IsStakeInRange(stake))
        {
            return;
        }

        if (ReplayedPendingRound(sittingId))
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = stake,
        });
        IssueSpin(sittingId, roundId, stake);
    }

    public void BuyScratch(int tier)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || !ScratchRules.IsValidTier(tier))
        {
            return;
        }

        if (ReplayedPendingRound(sittingId))
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.ScratchKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = ScratchRules.Prices[tier],
        });
        IssueScratch(sittingId, roundId, tier);
    }

    public void StartBarkeep()
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0)
        {
            return;
        }

        if (ReplayedPendingRound(sittingId))
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.BartenderKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = BarkeepRules.EntryChips,
        });
        IssueBarkeepStart(sittingId, roundId);
    }

    public void FinishBarkeep(string roundId, CasinoBarkeepOrderRequest[] orders)
    {
        if (roundInFlight || !session.IsSignedIn || roundId.Length == 0)
        {
            return;
        }

        var pending = PendingRound;
        var sittingId = pending is not null
            && string.Equals(pending.RoundId, roundId, StringComparison.Ordinal)
                ? pending.SittingId
                : string.Empty;
        roundInFlight = true;
        IssueBarkeepFinish(sittingId, roundId, orders);
    }

    internal static bool ReplaysBeforeStaking(PendingCasinoRound? pending, string sittingId)
    {
        return pending is not null && sittingId.Length > 0
            && string.Equals(pending.SittingId, sittingId, StringComparison.Ordinal);
    }

    private bool ReplayedPendingRound(string sittingId)
    {
        if (!ReplaysBeforeStaking(PendingRound, sittingId))
        {
            return false;
        }

        RecoverPendingRound();
        return true;
    }

    public void RecoverPendingRound()
    {
        if (roundInFlight || !session.IsSignedIn)
        {
            return;
        }

        var pending = PendingRound;
        if (pending is null)
        {
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.SlotsKind, StringComparison.Ordinal))
        {
            roundInFlight = true;
            IssueSpin(pending.SittingId, pending.RoundId, pending.Stake);
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.ScratchKind, StringComparison.Ordinal))
        {
            var tier = ScratchRules.TierForPrice(pending.Stake);
            if (tier < 0)
            {
                ClearPendingRound(pending.RoundId);
                return;
            }

            roundInFlight = true;
            IssueScratch(pending.SittingId, pending.RoundId, tier);
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.BartenderKind, StringComparison.Ordinal))
        {
            roundInFlight = true;
            IssueBarkeepStart(pending.SittingId, pending.RoundId);
        }
    }

    private void IssueSpin(string sittingId, string roundId, long stake)
    {
        work.Run("slots spin", async token =>
        {
            var result = await casino.SpinSlotsAsync(sittingId, roundId, stake, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            ClearPendingRound(roundId);
            Interlocked.Exchange(ref spinResult, result);
            if (result.Granted && result.Jackpot == 0)
            {
                store.AbsorbStack(sittingId, result.Stack);
            }
            else
            {
                store.AbsorbCeiling(result.Ceiling);
                store.RefreshNow();
            }
        }, () => roundInFlight = false);
    }

    private void IssueScratch(string sittingId, string roundId, int tier)
    {
        work.Run("scratch buy", async token =>
        {
            var result = await casino.BuyScratchAsync(sittingId, roundId, tier, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            ClearPendingRound(roundId);
            Interlocked.Exchange(ref scratchResult, result);
            if (result.Granted)
            {
                store.AbsorbStack(sittingId, result.Stack);
            }
            else
            {
                store.AbsorbCeiling(result.Ceiling);
                store.RefreshNow();
            }
        }, () => roundInFlight = false);
    }

    private void IssueBarkeepStart(string sittingId, string roundId)
    {
        work.Run("barkeep start", async token =>
        {
            var result = await casino.StartBarkeepAsync(sittingId, roundId, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            if (result.Granted)
            {
                store.AbsorbStack(sittingId, result.Stack);
            }
            else
            {
                ClearPendingRound(roundId);
                store.RefreshNow();
            }

            Interlocked.Exchange(ref barkeepStartResult, result);
        }, () => roundInFlight = false);
    }

    private void IssueBarkeepFinish(string sittingId, string roundId, CasinoBarkeepOrderRequest[] orders)
    {
        work.Run("barkeep finish", async token =>
        {
            var result = await casino.FinishBarkeepAsync(roundId, orders, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            if (!string.Equals(result.Reason, CasinoReasons.Cooldown, StringComparison.Ordinal))
            {
                ClearPendingRound(roundId);
            }

            Interlocked.Exchange(ref barkeepFinishResult, result);
            if (result.Granted && sittingId.Length > 0)
            {
                store.AbsorbStack(sittingId, result.Stack);
                return;
            }

            store.RefreshNow();
        }, () => roundInFlight = false);
    }

    internal static Dictionary<ulong, PendingCasinoRound>? RememberRound(
        Dictionary<ulong, PendingCasinoRound> snapshot, ulong contentId, PendingCasinoRound round)
    {
        if (snapshot.TryGetValue(contentId, out var known)
            && string.Equals(known.RoundId, round.RoundId, StringComparison.Ordinal))
        {
            return null;
        }

        var next = new Dictionary<ulong, PendingCasinoRound>(snapshot);
        next[contentId] = round;
        return next;
    }

    internal static Dictionary<ulong, PendingCasinoRound>? ClearRound(
        Dictionary<ulong, PendingCasinoRound> snapshot, ulong contentId, string roundId)
    {
        if (!snapshot.TryGetValue(contentId, out var known)
            || !string.Equals(known.RoundId, roundId, StringComparison.Ordinal))
        {
            return null;
        }

        var next = new Dictionary<ulong, PendingCasinoRound>(snapshot);
        next.Remove(contentId);
        return next;
    }

    private void RememberPendingRound(PendingCasinoRound round)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingRoundsSwapLock)
        {
            var next = RememberRound(configuration.PendingCasinoRounds, contentId, round);
            if (next is null)
            {
                return;
            }

            configuration.PendingCasinoRounds = next;
        }

        configuration.Save();
    }

    private void ClearPendingRound(string roundId)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingRoundsSwapLock)
        {
            var next = ClearRound(configuration.PendingCasinoRounds, contentId, roundId);
            if (next is null)
            {
                return;
            }

            configuration.PendingCasinoRounds = next;
        }

        configuration.Save();
    }

    public void Dispose()
    {
        Originals.Dispose();
        Plinko.Dispose();
        work.Dispose();
    }
}
