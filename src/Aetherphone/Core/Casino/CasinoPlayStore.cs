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
    private volatile bool metersInFlight;
    private CasinoSlotsSpinDto? spinResult;
    private CasinoSlotsGambleDto? gambleResult;
    private CasinoSlotsMetersDto? metersResult;
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
    }

    public CasinoOriginalsStore Originals { get; }

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

    public CasinoSlotsGambleDto? TakeGambleResult()
    {
        return Interlocked.Exchange(ref gambleResult, null);
    }

    public CasinoSlotsMetersDto? TakeMeters()
    {
        return Interlocked.Exchange(ref metersResult, null);
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

    public bool SpinMachine(string machineId, string mode, long bet)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || !SlotsRules.IsBet(bet)
            || !SlotsRules.Offers(machineId, mode))
        {
            return false;
        }

        if (ReplayedPendingRound(sittingId))
        {
            return false;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = bet,
            MachineId = machineId,
            Mode = mode,
        });
        IssueSpin(sittingId, roundId, bet, machineId, mode);
        return true;
    }

    public bool GambleSlots(string parentRoundId, int pick, long stake)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || parentRoundId.Length == 0
            || (pick != SlotsRules.GambleRed && pick != SlotsRules.GambleBlack))
        {
            return false;
        }

        if (ReplayedPendingRound(sittingId))
        {
            return false;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsGambleKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = stake,
            ParentRoundId = parentRoundId,
            Pick = pick,
        });
        IssueGamble(sittingId, roundId, parentRoundId, pick);
        return true;
    }

    public void RequestMeters(string machineId, long bet)
    {
        if (metersInFlight || !session.IsSignedIn || !SlotsRules.IsBet(bet))
        {
            return;
        }

        metersInFlight = true;
        work.Run("slots meters", async token =>
        {
            var result = await casino.SlotsMetersAsync(machineId, bet, token).ConfigureAwait(false);
            if (result is not null)
            {
                Interlocked.Exchange(ref metersResult, result);
            }
        }, () => metersInFlight = false);
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
            var machineId = SlotsRules.IsMachine(pending.MachineId) ? pending.MachineId : SlotsRules.BirdId;
            var mode = pending.Mode.Length > 0 ? pending.Mode : SlotsRules.BaseMode;
            IssueSpin(pending.SittingId, pending.RoundId, pending.Stake, machineId, mode);
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.SlotsGambleKind, StringComparison.Ordinal))
        {
            roundInFlight = true;
            IssueGamble(pending.SittingId, pending.RoundId, pending.ParentRoundId, pending.Pick);
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

    private void IssueSpin(string sittingId, string roundId, long bet, string machineId, string mode)
    {
        work.Run("slots spin", async token =>
        {
            var result = await casino.SpinSlotsAsync(sittingId, roundId, bet, machineId, mode, token)
                .ConfigureAwait(false);
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

    private void IssueGamble(string sittingId, string roundId, string parentRoundId, int pick)
    {
        work.Run("slots gamble", async token =>
        {
            var result = await casino.GambleSlotsAsync(sittingId, roundId, parentRoundId, pick, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            ClearPendingRound(roundId);
            Interlocked.Exchange(ref gambleResult, result);
            if (result.Granted)
            {
                store.AbsorbStack(sittingId, result.Stack);
            }
            else
            {
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
        work.Dispose();
    }
}
