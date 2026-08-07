using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows;

namespace Aetherphone.Core.Aethernet;

internal sealed class PatreonLinkFlow : IDisposable
{
    public const string FailureNetwork = "network";
    public const string FailureTimeout = "timeout";

    private const int PollIntervalSeconds = 4;
    private const long StatusRefreshIntervalMilliseconds = 5 * 60 * 1000;
    private const long StatusRetryIntervalMilliseconds = 60 * 1000;

    private readonly AccountClient client;
    private readonly Action? changed;
    private readonly CancellationTokenSource cancellation = new();
    private CancellationTokenSource? waitCancellation;
    private volatile bool busy;
    private volatile bool waiting;
    private volatile string? authorizeUrl;
    private volatile PatreonLinkStatusResponse? status;
    private volatile string? failureReason;
    private volatile bool linkedJustNow;
    private long nextStatusFetchTick;
    private int statusFetching;

    public PatreonLinkFlow(AccountClient client, Action? changed = null)
    {
        this.client = client;
        this.changed = changed;
    }

    public bool Busy => busy;

    public bool Waiting => waiting;

    public PatreonLinkStatusResponse? Status => status;

    public bool ConsumeLinked()
    {
        if (!linkedJustNow)
        {
            return false;
        }

        linkedJustNow = false;
        return true;
    }

    public string? ConsumeFailure()
    {
        var reason = failureReason;
        if (reason is not null)
        {
            failureReason = null;
        }

        return reason;
    }

    public void EnsureStatus()
    {
        if (Environment.TickCount64 < Volatile.Read(ref nextStatusFetchTick)
            || Interlocked.Exchange(ref statusFetching, 1) == 1)
        {
            return;
        }

        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var fresh = await client.PatreonLinkStatusAsync(token).ConfigureAwait(false);
                if (fresh is not null)
                {
                    status = fresh;
                }

                var interval = fresh is null ? StatusRetryIntervalMilliseconds : StatusRefreshIntervalMilliseconds;
                Volatile.Write(ref nextStatusFetchTick, Environment.TickCount64 + interval);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Patreon status fetch failed: {exception.Message}");
                Volatile.Write(ref nextStatusFetchTick, Environment.TickCount64 + StatusRetryIntervalMilliseconds);
            }
            finally
            {
                Volatile.Write(ref statusFetching, 0);
            }
        });
    }

    public void Start()
    {
        if (busy || waiting)
        {
            return;
        }

        busy = true;
        var flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        Interlocked.Exchange(ref waitCancellation, flowCancellation)?.Dispose();
        var token = flowCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var start = await client.StartPatreonLinkAsync(token).ConfigureAwait(false);
                if (start is null || !start.Ok || start.Url is null)
                {
                    failureReason = FailureNetwork;
                    return;
                }

                authorizeUrl = start.Url;
                waiting = true;
                UrlActions.OpenInBrowser(start.Url);
                await PollLoopAsync(start.ExpiresInSeconds, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Patreon link failed: {exception.Message}");
                failureReason = FailureNetwork;
            }
            finally
            {
                busy = false;
                waiting = false;
                authorizeUrl = null;
            }
        });
    }

    public void OpenAgain()
    {
        if (authorizeUrl is { } url)
        {
            UrlActions.OpenInBrowser(url);
        }
    }

    public void Cancel()
    {
        var flowCancellation = waitCancellation;
        try
        {
            flowCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        waiting = false;
        busy = false;
    }

    public void Unlink()
    {
        if (busy || waiting)
        {
            return;
        }

        busy = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var fresh = await client.UnlinkPatreonAsync(token).ConfigureAwait(false);
                if (fresh is null)
                {
                    failureReason = FailureNetwork;
                    return;
                }

                status = fresh;
                changed?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Patreon unlink failed: {exception.Message}");
                failureReason = FailureNetwork;
            }
            finally
            {
                busy = false;
            }
        });
    }

    private async Task PollLoopAsync(int expiresInSeconds, CancellationToken token)
    {
        var interval = PollIntervalSeconds;
        var deadline = DateTime.UtcNow.AddSeconds(expiresInSeconds > 0 ? expiresInSeconds : 600);
        while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), token).ConfigureAwait(false);
            var rateLimited = false;
            var fresh = await client
                .PatreonLinkStatusAsync(token, statusCode => rateLimited |= statusCode == 429)
                .ConfigureAwait(false);
            if (fresh is null)
            {
                if (rateLimited)
                {
                    interval += 2;
                }

                continue;
            }

            status = fresh;
            if (fresh.Linked)
            {
                linkedJustNow = true;
                changed?.Invoke();
                return;
            }
        }

        if (!token.IsCancellationRequested)
        {
            failureReason = FailureTimeout;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        Interlocked.Exchange(ref waitCancellation, null)?.Dispose();
        cancellation.Dispose();
    }
}
