using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Market;

internal sealed class MarketAlertService : IDisposable
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);
    private static readonly Vector4 Accent = new(0.95f, 0.74f, 0.26f, 1f);
    private readonly MarketboardService market;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly object sync = new();
    private readonly CancellationTokenSource cancellation = new();
    private volatile int triggeredCount;

    public MarketAlertService(MarketboardService market, NotificationService notifications, Configuration configuration)
    {
        this.market = market;
        this.notifications = notifications;
        this.configuration = configuration;
        _ = Task.Run(() => RunAsync(cancellation.Token));
    }

    public int TriggeredCount => triggeredCount;
    public int Count => configuration.MarketAlerts.Count;

    public void CopyInto(List<MarketAlert> buffer)
    {
        buffer.Clear();
        lock (sync)
        {
            var alerts = configuration.MarketAlerts;
            for (var index = 0; index < alerts.Count; index++)
            {
                buffer.Add(alerts[index]);
            }
        }
    }

    public bool HasAlertFor(uint itemId)
    {
        lock (sync)
        {
            var alerts = configuration.MarketAlerts;
            for (var index = 0; index < alerts.Count; index++)
            {
                if (alerts[index].ItemId == itemId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void Add(MarketAlert alert)
    {
        lock (sync)
        {
            configuration.MarketAlerts.Add(alert);
        }

        configuration.Save();
        Recount();
    }

    public void Remove(MarketAlert alert)
    {
        lock (sync)
        {
            configuration.MarketAlerts.Remove(alert);
        }

        configuration.Save();
        Recount();
    }

    public void SetEnabled(MarketAlert alert, bool enabled)
    {
        alert.Enabled = enabled;
        if (!enabled)
        {
            alert.Triggered = false;
            alert.Acknowledged = false;
        }

        configuration.Save();
        Recount();
    }

    public void Acknowledge()
    {
        lock (sync)
        {
            var alerts = configuration.MarketAlerts;
            for (var index = 0; index < alerts.Count; index++)
            {
                if (alerts[index].Triggered)
                {
                    alerts[index].Acknowledged = true;
                }
            }
        }

        Recount();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(StartupDelay, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await PollAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Market alert poll failed: {exception.Message}");
            }

            try
            {
                await Task.Delay(PollInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        MarketAlert[] copy;
        lock (sync)
        {
            copy = configuration.MarketAlerts.ToArray();
        }

        for (var index = 0; index < copy.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var alert = copy[index];
            if (!alert.Enabled || alert.ItemId == 0 || alert.ScopeName.Length == 0)
            {
                continue;
            }

            var scope = new MarketScope(alert.ScopeKind, alert.ScopeName, alert.ScopeName);
            var snapshot = await market.FetchAsync(alert.ItemId, scope, token).ConfigureAwait(false);
            if (snapshot is null)
            {
                continue;
            }

            var price = snapshot.Min(alert.HqOnly);
            alert.LastSeenPrice = price;
            var crossed = alert.Below ? price > 0 && price <= alert.Threshold : price >= alert.Threshold;
            if (crossed && !alert.Triggered)
            {
                alert.Triggered = true;
                alert.Acknowledged = false;
                Present(alert, price);
            }
            else if (!crossed && alert.Triggered)
            {
                alert.Triggered = false;
                alert.Acknowledged = false;
            }
        }

        Recount();
    }

    private void Present(MarketAlert alert, long price)
    {
        var title = alert.HqOnly ? $"{alert.ItemName} ({Loc.T(L.Common.Hq)})" : alert.ItemName;
        var arrow = alert.Below ? "≤" : "≥";
        var body = Loc.T(L.Market.AlertBody, arrow, MarketFormat.Gil(alert.Threshold), MarketFormat.Gil(price),
            alert.ScopeName);
        notifications.Notify(new PhoneNotification("market", title, body, DateTime.Now, Accent));
    }

    private void Recount()
    {
        var count = 0;
        lock (sync)
        {
            var alerts = configuration.MarketAlerts;
            for (var index = 0; index < alerts.Count; index++)
            {
                if (alerts[index].Triggered && !alerts[index].Acknowledged)
                {
                    count++;
                }
            }
        }

        triggeredCount = count;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
