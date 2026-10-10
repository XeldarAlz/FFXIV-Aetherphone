using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Coins;

internal sealed class CoinRadioListenTracker : IDisposable
{
    private const long TickMilliseconds = 1000;
    private const int UnloadEndTimeoutMilliseconds = 2000;
    private const int NotFoundStatus = 404;
    private const long MissingBackoffMilliseconds = 15 * 60_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly RadioPlayer radio;
    private readonly FrameworkTicker ticker;
    private readonly StoreWork work = new("CoinRadio");
    private readonly SemaphoreSlim gate = new(1, 1);

    private string listeningStationId = string.Empty;
    private string? accountKey;
    private volatile string? openSessionId;
    private int generation;
    private long blockedUntilTick;
    private long retryAtTick;

    public CoinRadioListenTracker(AethernetSession session, CoinsClient coins, RadioPlayer radio, IFramework framework)
    {
        this.session = session;
        this.coins = coins;
        this.radio = radio;
        accountKey = AccountKey();
        ticker = new FrameworkTicker(framework, TickMilliseconds, Tick);
    }

    private void Tick()
    {
        TrackAccount();
        var stationId = radio.CurrentStationInfo.CommunityId ?? string.Empty;
        var step = CoinRadioListen.Decide(listeningStationId, radio.State, stationId, session.IsSignedIn);
        switch (step)
        {
            case RadioListenStep.Start:
            case RadioListenStep.Switch:
                Begin(stationId);
                return;
            case RadioListenStep.End:
                Finish();
                return;
            default:
                RetryRefusedStart(stationId);
                return;
        }
    }

    private void RetryRefusedStart(string stationId)
    {
        var retryAt = Interlocked.Read(ref retryAtTick);
        if (retryAt == 0 || Environment.TickCount64 < retryAt || openSessionId is not null)
        {
            return;
        }

        if (!string.Equals(listeningStationId, stationId, StringComparison.Ordinal)
            || radio.State != RadioPlaybackState.Playing)
        {
            return;
        }

        Interlocked.Exchange(ref retryAtTick, 0);
        Begin(stationId);
    }

    private void TrackAccount()
    {
        var key = AccountKey();
        if (string.Equals(key, accountKey, StringComparison.Ordinal))
        {
            return;
        }

        accountKey = key;
        listeningStationId = string.Empty;
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref retryAtTick, 0);
        openSessionId = null;
    }

    private string? AccountKey() => session.IsSignedIn ? session.CurrentUser?.Id ?? session.Token : null;

    private void Begin(string stationId)
    {
        listeningStationId = stationId;
        var ticket = Interlocked.Increment(ref generation);
        if (Environment.TickCount64 < Interlocked.Read(ref blockedUntilTick) && openSessionId is null)
        {
            return;
        }

        work.Run("listen start", token => StartAsync(stationId, ticket, token));
    }

    private void Finish()
    {
        listeningStationId = string.Empty;
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref retryAtTick, 0);
        work.Run("listen end", EndOpenAsync);
    }

    private async Task StartAsync(string stationId, int ticket, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            await EndOpenLockedAsync(token).ConfigureAwait(false);
            var status = 0;
            var started = await coins.StartRadioListenAsync(stationId, token, failure => status = failure.StatusCode)
                .ConfigureAwait(false);
            if (status == NotFoundStatus)
            {
                Interlocked.Exchange(ref blockedUntilTick, Environment.TickCount64 + MissingBackoffMilliseconds);
            }

            var accepted = started is { Started: true } && !string.IsNullOrEmpty(started.SessionId);
            var retryDelay = CoinRadioListen.RetryDelayFor(accepted, started?.Reason ?? string.Empty, status);
            Interlocked.Exchange(ref retryAtTick, retryDelay > 0 ? Environment.TickCount64 + retryDelay : 0);
            if (started is null || !accepted)
            {
                return;
            }

            openSessionId = started.SessionId;
            if (Volatile.Read(ref generation) != ticket)
            {
                await EndOpenLockedAsync(token).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EndOpenAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await EndOpenLockedAsync(token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EndOpenLockedAsync(CancellationToken token)
    {
        var sessionId = Interlocked.Exchange(ref openSessionId, null);
        if (sessionId is null || !session.IsSignedIn)
        {
            return;
        }

        await coins.EndRadioListenAsync(sessionId, token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        ticker.Dispose();
        work.Dispose();
        EndOpenBeforeAssemblyUnloads();
    }

    private void EndOpenBeforeAssemblyUnloads()
    {
        var sessionId = Interlocked.Exchange(ref openSessionId, null);
        if (sessionId is null || !session.IsSignedIn)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(UnloadEndTimeoutMilliseconds);
            coins.EndRadioListenAsync(sessionId, timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[CoinRadio] listen end on unload failed");
        }
    }
}
