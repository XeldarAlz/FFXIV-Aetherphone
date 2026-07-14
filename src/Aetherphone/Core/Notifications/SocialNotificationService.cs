using System.Numerics;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class SocialNotificationService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(600);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> seenIds = new();
    private volatile NotificationDto[] latest = Array.Empty<NotificationDto>();
    private volatile bool polling;
    private volatile bool primed;

    public SocialNotificationService(AethernetSession session, AethernetClient client, NotificationService notifications,
        Configuration configuration, IFramework framework, PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.framework = framework;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        signals.SocialPinged += cadence.RequestImmediate;
        framework.Update += OnFrameworkTick;
    }

    public NotificationDto[] Latest => latest;

    public int CountFor(string app)
    {
        var items = latest;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app)
            {
                count++;
            }
        }

        return count;
    }

    public int UnseenCount(string app)
    {
        var items = latest;
        configuration.SocialActivitySeenUnix.TryGetValue(app, out var seenUnix);
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app && items[index].CreatedAtUnix > seenUnix)
            {
                count++;
            }
        }

        return count;
    }

    public void MarkSeen(string app)
    {
        var items = latest;
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app && items[index].CreatedAtUnix > newest)
            {
                newest = items[index].CreatedAtUnix;
            }
        }

        configuration.SocialActivitySeenUnix.TryGetValue(app, out var seenUnix);
        if (newest <= seenUnix)
        {
            return;
        }

        configuration.SocialActivitySeenUnix[app] = newest;
        configuration.Save();
    }

    public void RefreshNow()
    {
        if (session.IsSignedIn)
        {
            cadence.Mark(DateTime.UtcNow);
            Poll();
        }
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            primed = false;
            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        Poll();
    }

    private void Poll()
    {
        if (polling)
        {
            return;
        }

        polling = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await client.NotificationsAsync(token).ConfigureAwait(false);
                if (page is not null)
                {
                    Ingest(page.Items);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notifications] poll failed: {exception.Message}");
            }
            finally
            {
                polling = false;
            }
        });
    }

    private void Ingest(NotificationDto[] items)
    {
        var wasPrimed = primed;
        for (var index = items.Length - 1; index >= 0; index--)
        {
            var item = items[index];
            if (wasPrimed && !seenIds.Contains(item.Id))
            {
                Present(item);
            }
        }

        seenIds.Clear();
        for (var index = 0; index < items.Length; index++)
        {
            seenIds.Add(items[index].Id);
        }

        latest = items;
        primed = true;
    }

    private void Present(NotificationDto item)
    {
        var body = SocialActivity.Body(item);
        if (body.Length == 0)
        {
            return;
        }

        var removed = item.Type == SocialActivity.TypePostRemoved;
        var title = removed ? Loc.T(L.Moderation.RemovedTitle) : SocialActivity.ActorLabel(item);
        notifications.Notify(new PhoneNotification(item.App, title, body, DateTime.Now,
            AccentFor(item.App))
        {
            ActorId = item.ActorId,
            PostId = item.PostId,
            SocialType = item.Type,
        });

        if (removed)
        {
            Plugin.Confirm.Alert(
                Loc.T(L.Moderation.RemovedTitle),
                $"{body}\n\n{Loc.T(L.Moderation.RemovedFooter)}",
                Loc.T(L.Moderation.RemovedDismiss));
        }
    }

    private static Vector4 AccentFor(string app)
    {
        return app switch
        {
            SocialActivity.AethergramApp => AppAccents.For(SocialActivity.AethergramApp),
            SocialActivity.VelvetApp => AppAccents.For(SocialActivity.VelvetApp),
            _ => AppAccents.For(SocialActivity.ChirperApp),
        };
    }

    public void Dispose()
    {
        signals.SocialPinged -= cadence.RequestImmediate;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
