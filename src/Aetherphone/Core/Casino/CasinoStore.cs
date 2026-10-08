using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoStore : IDisposable
{
    private const long StateRefreshMilliseconds = 60_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CoinStore coins;
    private readonly StoreWork work = new("Casino");
    private readonly object pendingSittingsSwapLock = new();

    private volatile CasinoStateDto? state;
    private volatile bool openingSitting;
    private volatile bool toppingUp;
    private volatile bool closingSitting;
    private volatile bool savingLimits;
    private CasinoSittingResultDto? sittingResult;
    private CasinoSittingResultDto? closeResult;
    private CasinoLimitsDto? limitsResult;
    private int moneyMoveFailed;
    private int limitsSaveFailed;
    private long stateLoadedAtTick;
    private long stateAttemptedAtTick;
    private int fetchingState;
    private string? lastAccountId;

    public CasinoStore(Configuration configuration, AethernetSession session, CasinoClient casino, CoinStore coins)
    {
        this.configuration = configuration;
        this.session = session;
        this.casino = casino;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CasinoStateDto? State => state;

    public bool OpeningSitting => openingSitting;

    public bool ToppingUp => toppingUp;

    public bool ClosingSitting => closingSitting;

    public bool SavingLimits => savingLimits;

    public bool MovingMoney => openingSitting || toppingUp || closingSitting;

    public string PendingSittingId
    {
        get
        {
            var contentId = session.ActiveContentId;
            if (contentId == 0)
            {
                return string.Empty;
            }

            var snapshot = configuration.PendingCasinoSittings;
            return snapshot.TryGetValue(contentId, out var sittingId) ? sittingId : string.Empty;
        }
    }

    public long SittingSeenAtUnix
    {
        get
        {
            var contentId = session.ActiveContentId;
            if (contentId == 0)
            {
                return 0;
            }

            var snapshot = configuration.CasinoSittingSeenAtUnix;
            return snapshot.TryGetValue(contentId, out var seenAtUnix) ? seenAtUnix : 0;
        }
    }

    public void EnsureFresh()
    {
        RefreshState(StateRefreshMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref stateLoadedAtTick, 0);
        Interlocked.Exchange(ref stateAttemptedAtTick, 0);
        RefreshState(0);
    }

    public CasinoSittingResultDto? TakeSittingResult()
    {
        return Interlocked.Exchange(ref sittingResult, null);
    }

    public CasinoSittingResultDto? TakeCloseResult()
    {
        return Interlocked.Exchange(ref closeResult, null);
    }

    public CasinoLimitsDto? TakeLimitsResult()
    {
        return Interlocked.Exchange(ref limitsResult, null);
    }

    public bool TakeMoneyMoveFailure()
    {
        return Interlocked.Exchange(ref moneyMoveFailed, 0) != 0;
    }

    public bool TakeLimitsFailure()
    {
        return Interlocked.Exchange(ref limitsSaveFailed, 0) != 0;
    }

    public void OpenSitting(long amount)
    {
        if (MovingMoney || !session.IsSignedIn || amount <= 0)
        {
            return;
        }

        openingSitting = true;
        var clientSittingId = Guid.NewGuid().ToString("N");
        var clientActionId = Guid.NewGuid().ToString("N");
        RememberPendingSitting(clientSittingId);
        work.Run("open sitting", async token =>
        {
            var result = await casino.OpenSittingAsync(clientSittingId, clientActionId, amount, token)
                .ConfigureAwait(false);
            AbsorbMoneyMove(result, ref sittingResult);
        }, () => openingSitting = false);
    }

    public bool HasChips => (state?.Sitting?.Stack ?? 0) > 0;

    public long Jackpot => state?.Jackpot ?? 0;

    public CasinoCeiling Ceiling => CasinoLadder.CeilingFor(state);

    public void TopUp(long amount)
    {
        var sittingId = state?.Sitting?.Id ?? string.Empty;
        if (MovingMoney || !session.IsSignedIn || amount <= 0 || sittingId.Length == 0)
        {
            return;
        }

        toppingUp = true;
        var clientActionId = Guid.NewGuid().ToString("N");
        work.Run("top up", async token =>
        {
            var result = await casino.TopUpAsync(sittingId, clientActionId, amount, token).ConfigureAwait(false);
            AbsorbMoneyMove(result, ref sittingResult);
        }, () => toppingUp = false);
    }

    public void CloseSitting()
    {
        var sittingId = state?.Sitting?.Id ?? string.Empty;
        if (sittingId.Length == 0)
        {
            sittingId = PendingSittingId;
        }

        if (MovingMoney || !session.IsSignedIn || sittingId.Length == 0)
        {
            return;
        }

        closingSitting = true;
        work.Run("close sitting", async token =>
        {
            var result = await casino.CloseSittingAsync(sittingId, token).ConfigureAwait(false);
            AbsorbMoneyMove(result, ref closeResult);
        }, () => closingSitting = false);
    }

    public void SetLimits(long? selfLossLimit)
    {
        if (savingLimits || !session.IsSignedIn)
        {
            return;
        }

        savingLimits = true;
        work.Run("set limits", async token =>
        {
            var result = await casino.SetLimitsAsync(selfLossLimit, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref limitsSaveFailed, 1);
                return;
            }

            Interlocked.Exchange(ref limitsResult, result);
            var current = state;
            if (current is not null)
            {
                state = MergeLimits(current, result);
            }
        }, () => savingLimits = false);
    }

    public void AbsorbStack(string sittingId, long stack)
    {
        var next = StackAbsorbedInto(state, sittingId, stack);
        if (next is null)
        {
            return;
        }

        state = next;
    }

    public void AbsorbCeiling(long ceiling)
    {
        var next = CeilingAbsorbedInto(state, ceiling);
        if (next is null)
        {
            return;
        }

        state = next;
    }

    internal static CasinoStateDto? CeilingAbsorbedInto(CasinoStateDto? current, long ceiling)
    {
        if (current is null || ceiling <= 0)
        {
            return null;
        }

        var held = current.Ceiling ?? new CasinoCeilingDto();
        return held.MaxBet == ceiling ? null : current with { Ceiling = held with { MaxBet = ceiling } };
    }

    internal static CasinoStateDto? StackAbsorbedInto(CasinoStateDto? current, string sittingId, long stack)
    {
        if (current is null || sittingId.Length == 0)
        {
            return null;
        }

        var bankroll = current.Sitting;
        if (bankroll is not null && string.Equals(bankroll.Id, sittingId, StringComparison.Ordinal))
        {
            return bankroll.Stack == stack ? null : current with { Sitting = bankroll with { Stack = stack } };
        }

        var rack = current.TableSitting;
        if (rack is not null && string.Equals(rack.Id, sittingId, StringComparison.Ordinal))
        {
            return rack.Stack == stack ? null : current with { TableSitting = rack with { Stack = stack } };
        }

        return null;
    }

    internal static CasinoStateDto MergeLimits(CasinoStateDto current, CasinoLimitsDto limits)
    {
        return current with
        {
            LossLimit = limits.LossLimit,
            LossHeadroom = Math.Max(0, limits.LossLimit - current.NetLossToday - current.AtRisk),
            SelfLossLimit = limits.SelfLossLimit,
            PendingRaiseLimit = limits.PendingRaiseLimit,
            PendingRaiseAtUnix = limits.PendingRaiseAtUnix,
        };
    }

    private void AbsorbMoneyMove(CasinoSittingResultDto? result, ref CasinoSittingResultDto? slot)
    {
        if (result is null)
        {
            Interlocked.Exchange(ref moneyMoveFailed, 1);
            return;
        }

        Interlocked.Exchange(ref slot, result);
        if (result.Granted)
        {
            coins.AbsorbLocalAward(result.Balance);
        }

        RefreshNow();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            state = null;
            Interlocked.Exchange(ref sittingResult, null);
            Interlocked.Exchange(ref closeResult, null);
            Interlocked.Exchange(ref limitsResult, null);
            Interlocked.Exchange(ref moneyMoveFailed, 0);
            Interlocked.Exchange(ref limitsSaveFailed, 0);
            Interlocked.Exchange(ref stateLoadedAtTick, 0);
            Interlocked.Exchange(ref stateAttemptedAtTick, 0);
            if (session.IsSignedIn)
            {
                RefreshState(0);
            }

            return;
        }

        if (session.IsSignedIn && PendingSittingId.Length > 0 && state is null)
        {
            RefreshState(0);
        }
    }

    private void RefreshState(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref stateAttemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref stateLoadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingState, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref stateAttemptedAtTick, now);
        work.Run("state refresh", async token =>
        {
            var fresh = await casino.GetStateAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            state = fresh;
            Interlocked.Exchange(ref stateLoadedAtTick, Environment.TickCount64);
            ReconcilePendingSitting(fresh);
        }, () => Interlocked.Exchange(ref fetchingState, 0));
    }

    private void ReconcilePendingSitting(CasinoStateDto fresh)
    {
        if (openingSitting)
        {
            return;
        }

        var sittingId = fresh.Sitting?.Id ?? string.Empty;
        if (sittingId.Length > 0)
        {
            RememberPendingSitting(sittingId);
        }
        else
        {
            ClearPendingSitting();
        }
    }

    private void RememberPendingSitting(string sittingId)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingSittingsSwapLock)
        {
            var snapshot = configuration.PendingCasinoSittings;
            if (snapshot.TryGetValue(contentId, out var known) &&
                string.Equals(known, sittingId, StringComparison.Ordinal))
            {
                return;
            }

            var next = new Dictionary<ulong, string>(snapshot);
            next[contentId] = sittingId;
            configuration.PendingCasinoSittings = next;

            var seenAt = new Dictionary<ulong, long>(configuration.CasinoSittingSeenAtUnix);
            seenAt[contentId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            configuration.CasinoSittingSeenAtUnix = seenAt;
        }

        configuration.Save();
    }

    private void ClearPendingSitting()
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingSittingsSwapLock)
        {
            var snapshot = configuration.PendingCasinoSittings;
            if (!snapshot.ContainsKey(contentId))
            {
                return;
            }

            var next = new Dictionary<ulong, string>(snapshot);
            next.Remove(contentId);
            configuration.PendingCasinoSittings = next;

            if (configuration.CasinoSittingSeenAtUnix.ContainsKey(contentId))
            {
                var seenAt = new Dictionary<ulong, long>(configuration.CasinoSittingSeenAtUnix);
                seenAt.Remove(contentId);
                configuration.CasinoSittingSeenAtUnix = seenAt;
            }
        }

        configuration.Save();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
