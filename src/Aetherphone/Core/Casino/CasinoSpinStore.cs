using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoSpinStore : IDisposable
{
    private const long StatusRefreshMilliseconds = 120_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CoinStore coins;
    private readonly StoreWork work = new("CasinoSpin");

    private volatile CasinoDailySpinDto? answer;
    private volatile bool claiming;
    private CasinoDailySpinDto? claimResult;
    private int claimFailed;
    private long statusLoadedAtTick;
    private long statusAttemptedAtTick;
    private int loadingStatus;
    private int claimGeneration;
    private string? lastAccountId;

    public CasinoSpinStore(AethernetSession session, CasinoClient casino, CoinStore coins)
    {
        this.session = session;
        this.casino = casino;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CasinoDailySpinDto? Answer => answer;

    public bool Claiming => claiming;

    public bool Busy => claiming || Interlocked.CompareExchange(ref loadingStatus, 0, 0) != 0;

    public void EnsureFresh()
    {
        RefreshStatus(StatusRefreshMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref statusLoadedAtTick, 0);
        Interlocked.Exchange(ref statusAttemptedAtTick, 0);
        RefreshStatus(0);
    }

    public CasinoDailySpinDto? TakeClaimResult()
    {
        return Interlocked.Exchange(ref claimResult, null);
    }

    public bool TakeClaimFailure()
    {
        return Interlocked.Exchange(ref claimFailed, 0) != 0;
    }

    public void Claim()
    {
        if (Busy || !session.IsSignedIn || !DailySpinStatus.CanClaim(answer, false))
        {
            return;
        }

        Interlocked.Increment(ref claimGeneration);
        claiming = true;
        work.Run("daily spin", async token =>
        {
            var result = await casino.ClaimDailySpinAsync(token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref claimFailed, 1);
                return;
            }

            answer = result;
            Interlocked.Exchange(ref statusLoadedAtTick, Environment.TickCount64);
            Interlocked.Exchange(ref claimResult, result);
            if (result.Granted && result.Balance > 0)
            {
                coins.AbsorbLocalAward(result.Balance);
            }
        }, () => claiming = false);
    }

    private void RefreshStatus(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn || claiming)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref statusAttemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref statusLoadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref loadingStatus, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref statusAttemptedAtTick, now);
        LoadStatus(Interlocked.CompareExchange(ref claimGeneration, 0, 0));
    }

    private void LoadStatus(int generation)
    {
        work.Run("daily spin status", async token =>
        {
            var status = await casino.DailySpinStatusAsync(token).ConfigureAwait(false);
            if (status is null
                || claiming
                || Interlocked.CompareExchange(ref claimGeneration, 0, 0) != generation)
            {
                return;
            }

            answer = status;
            Interlocked.Exchange(ref statusLoadedAtTick, Environment.TickCount64);
        }, () => Interlocked.Exchange(ref loadingStatus, 0));
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        answer = null;
        Interlocked.Exchange(ref claimResult, null);
        Interlocked.Exchange(ref claimFailed, 0);
        Interlocked.Exchange(ref statusLoadedAtTick, 0);
        Interlocked.Exchange(ref statusAttemptedAtTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
