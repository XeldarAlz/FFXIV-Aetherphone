using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal sealed class CoinGameSessionTracker : IDisposable
{
    private const int UnloadEndTimeoutMilliseconds = 2000;
    private const string CooldownReason = "cooldown";
    private const long MinimumRetryMilliseconds = 1000;

    // Mirrors CoinEconomy.GameIssueCooldownSeconds on the Aethernet server.
    private const long IssueCooldownMilliseconds = 60_000;

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("CoinGames");
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile string? openSessionId;
    private volatile string? openGameId;
    private CoinAwardDto? lastAward;
    private volatile string? lastAwardGameId;
    private int generation;
    private long openStartTicks;
    private long lastIssuedTicks;
    private long cooldownEndTicks;
    private volatile int openMinSeconds = 180;
    private volatile int openDeepSeconds = 900;

    public CoinGameSessionTracker(Configuration configuration, AethernetSession session, CoinsClient coins)
    {
        this.configuration = configuration;
        this.session = session;
        this.coins = coins;
        session.Changed += OnSessionChanged;
        OnSessionChanged();
    }

    public string? OpenGameId => openGameId;

    public int OpenMinSeconds => openMinSeconds;

    public int OpenDeepSeconds => openDeepSeconds;

    public int OpenSessionSeconds
    {
        get
        {
            if (openSessionId is null)
            {
                return -1;
            }

            var start = Volatile.Read(ref openStartTicks);
            return start == 0 ? -1 : (int)((Environment.TickCount64 - start) / 1000);
        }
    }

    public int CooldownSeconds
    {
        get
        {
            var end = Volatile.Read(ref cooldownEndTicks);
            if (end == 0)
            {
                return 0;
            }

            var remaining = end - Environment.TickCount64;
            return remaining <= 0 ? 0 : (int)((remaining + 999) / 1000);
        }
    }

    public CoinAwardDto? TakeAward(out string? gameId)
    {
        gameId = lastAwardGameId;
        var award = Interlocked.Exchange(ref lastAward, null);
        if (award is null)
        {
            gameId = null;
        }

        return award;
    }

    public void GameOpened(string gameId)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var ticket = Interlocked.Increment(ref generation);
        openGameId = gameId;
        work.Run("session start", token => StartAsync(gameId, ticket, token));
    }

    public void GameClosed()
    {
        var gameId = openGameId;
        Interlocked.Increment(ref generation);
        openSessionId = null;
        openGameId = null;
        Volatile.Write(ref openStartTicks, 0);
        Volatile.Write(ref cooldownEndTicks, 0);
        if (!session.IsSignedIn)
        {
            return;
        }

        lastAwardGameId = gameId;
        work.Run("session end", EndPendingAsync);
    }

    private void OnSessionChanged()
    {
        if (!session.IsSignedIn || configuration.PendingCoinGameSession.Length == 0 || openSessionId is not null)
        {
            return;
        }

        work.Run("session recover", EndPendingAsync);
    }

    private async Task StartAsync(string gameId, int ticket, CancellationToken token)
    {
        while (true)
        {
            var retryAt = await IssueAsync(gameId, ticket, token).ConfigureAwait(false);
            if (retryAt == 0)
            {
                return;
            }

            var delay = Math.Max(retryAt - Environment.TickCount64, MinimumRetryMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(delay), token).ConfigureAwait(false);
            if (Volatile.Read(ref generation) != ticket)
            {
                return;
            }
        }
    }

    private async Task<long> IssueAsync(string gameId, int ticket, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref generation) != ticket)
            {
                return 0;
            }

            await EndPendingLockedAsync(token).ConfigureAwait(false);
            var issued = await coins.StartGameSessionAsync(gameId, token).ConfigureAwait(false);
            if (issued is null)
            {
                return 0;
            }

            if (issued.SessionId.Length == 0)
            {
                return BeginCooldown(issued, ticket);
            }

            var startTicks = ServerStartTicks(issued);
            Volatile.Write(ref lastIssuedTicks, startTicks);
            RememberPending(issued.SessionId);
            if (Volatile.Read(ref generation) != ticket)
            {
                return 0;
            }

            openMinSeconds = issued.MinSeconds > 0 ? issued.MinSeconds : openMinSeconds;
            openDeepSeconds = issued.DeepSeconds > 0 ? issued.DeepSeconds : openDeepSeconds;
            Volatile.Write(ref cooldownEndTicks, 0);
            Volatile.Write(ref openStartTicks, startTicks);
            openSessionId = issued.SessionId;
            return 0;
        }
        finally
        {
            gate.Release();
        }
    }

    private long BeginCooldown(CoinGameSessionDto issued, int ticket)
    {
        if (!string.Equals(issued.Reason, CooldownReason, StringComparison.Ordinal)
            || Volatile.Read(ref generation) != ticket)
        {
            return 0;
        }

        var now = Environment.TickCount64;
        var lastIssued = Volatile.Read(ref lastIssuedTicks);
        var end = lastIssued != 0 && lastIssued + IssueCooldownMilliseconds > now
            ? lastIssued + IssueCooldownMilliseconds
            : now + IssueCooldownMilliseconds;
        Volatile.Write(ref cooldownEndTicks, end);
        return end;
    }

    private static long ServerStartTicks(CoinGameSessionDto issued)
    {
        var startTicks = Environment.TickCount64;
        if (issued.StartedAtUnix <= 0)
        {
            return startTicks;
        }

        var alreadyElapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued.StartedAtUnix;
        return alreadyElapsed > 0 ? startTicks - alreadyElapsed * 1000 : startTicks;
    }

    private async Task EndPendingAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await EndPendingLockedAsync(token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EndPendingLockedAsync(CancellationToken token)
    {
        var pending = configuration.PendingCoinGameSession;
        if (pending.Length == 0)
        {
            return;
        }

        var award = await coins.EndGameSessionAsync(pending, token).ConfigureAwait(false);
        if (award is null)
        {
            return;
        }

        ClearPending(pending);
        if (award.Granted)
        {
            Interlocked.Exchange(ref lastAward, award);
        }
    }

    private void RememberPending(string sessionId)
    {
        configuration.PendingCoinGameSession = sessionId;
        configuration.Save();
    }

    private void ClearPending(string sessionId)
    {
        if (!string.Equals(configuration.PendingCoinGameSession, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        configuration.PendingCoinGameSession = string.Empty;
        configuration.Save();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
        openSessionId = null;
        openGameId = null;
        BankPendingAwardBeforeAssemblyUnloads();
    }

    private void BankPendingAwardBeforeAssemblyUnloads()
    {
        var pending = configuration.PendingCoinGameSession;
        if (pending.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(UnloadEndTimeoutMilliseconds);
            var award = coins.EndGameSessionAsync(pending, timeout.Token).GetAwaiter().GetResult();
            if (award is not null)
            {
                ClearPending(pending);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[CoinGames] session end on unload failed");
        }
    }
}
