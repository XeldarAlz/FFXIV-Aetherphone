using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Analytics;

internal sealed class AnalyticsService : IAnalyticsService
{
    private const int MaxQueued = 500;
    private const int MaxBatch = 100;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FinalFlushTimeout = TimeSpan.FromSeconds(3);

    private readonly AnalyticsClient client;
    private readonly Configuration configuration;
    private readonly string gameRegion;
    private readonly string pluginVersion;
    private readonly string installId;
    private readonly string sessionId;
    private readonly ConcurrentQueue<PendingEvent> queue = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task flushLoop;

    private int queuedCount;

    public AnalyticsService(AnalyticsClient client, Configuration configuration, string gameRegion)
    {
        this.client = client;
        this.configuration = configuration;
        this.gameRegion = gameRegion;
        pluginVersion = AepConstants.Version;
        installId = EnsureInstallId(configuration);
        sessionId = Guid.NewGuid().ToString("N");
        flushLoop = Task.Run(() => RunFlushLoopAsync(cancellation.Token));
    }

    public void Track(AnalyticsEvent analyticsEvent)
    {
        if (!configuration.AnalyticsEnabled || analyticsEvent is null)
        {
            return;
        }

        if (Volatile.Read(ref queuedCount) >= MaxQueued)
        {
            return;
        }

        var props = SerializeProperties(analyticsEvent.Properties);
        queue.Enqueue(new PendingEvent(analyticsEvent.Type, analyticsEvent.AppId, props, DateTime.UtcNow));
        Interlocked.Increment(ref queuedCount);
    }

    private static string EnsureInstallId(Configuration configuration)
    {
        if (string.IsNullOrEmpty(configuration.AnalyticsInstallId))
        {
            configuration.AnalyticsInstallId = Guid.NewGuid().ToString("N");
            configuration.Save();
        }

        return configuration.AnalyticsInstallId;
    }

    private static string? SerializeProperties(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return null;
        }

        var map = properties as Dictionary<string, string> ?? new Dictionary<string, string>(properties);
        return JsonSerializer.Serialize(map, AethernetJsonContext.Default.DictionaryStringString);
    }

    private async Task RunFlushLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FlushAsync(CancellationToken token)
    {
        var batch = new List<AnalyticsEventDto>(MaxBatch);
        while (batch.Count < MaxBatch && queue.TryDequeue(out var pending))
        {
            Interlocked.Decrement(ref queuedCount);
            batch.Add(new AnalyticsEventDto(pending.Type, pending.AppId, pending.ClientTime, pending.Props));
        }

        if (batch.Count == 0)
        {
            return;
        }

        var request = new AnalyticsBatchRequest(installId, sessionId, pluginVersion, gameRegion, batch.ToArray());
        try
        {
            await client.SendAsync(request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Analytics flush failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();

        var finalFlush = new CancellationTokenSource(FinalFlushTimeout);
        _ = FlushAsync(finalFlush.Token).ContinueWith(
            static (task, resource) =>
            {
                ((CancellationTokenSource)resource!).Dispose();
                _ = task.Exception;
            },
            finalFlush,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private readonly record struct PendingEvent(string Type, string? AppId, string? Props, DateTime ClientTime);
}
