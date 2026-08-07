using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class SocialNotificationService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan AckRetryInterval = TimeSpan.FromSeconds(15);

    private static readonly string[] ServedApps =
    {
        SocialActivity.ChirperApp,
        SocialActivity.AethergramApp,
        SocialActivity.VelvetApp,
        SocialActivity.YellowPagesApp,
        SocialActivity.MessageApp,
    };

    private readonly AethernetSession session;
    private readonly AppInstaller installer;
    private readonly AccountClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly NotificationAckQueue acks;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> seenIds = new();
    private volatile NotificationDto[] latest = Array.Empty<NotificationDto>();
    private volatile Dictionary<string, int>? serverUnread;
    private volatile bool polling;
    private volatile bool primed;
    private volatile bool flushingAcks;
    private DateTime nextAckFlushUtc = DateTime.MinValue;
    private string? lastAccountId;

    public SocialNotificationService(AethernetSession session, AccountClient client, NotificationService notifications,
        Configuration configuration, IFramework framework, PhoneVisibility visibility, RealtimeSignalBus signals,
        AppInstaller installer)
    {
        this.session = session;
        this.installer = installer;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.framework = framework;
        this.signals = signals;
        acks = new NotificationAckQueue(configuration.PendingNotificationAcks, configuration.Save);
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        signals.SocialPinged += cadence.RequestImmediate;
        signals.ConnectedChanged += OnRealtimeConnected;
        session.Changed += OnSessionChanged;
        framework.Update += OnFrameworkTick;
    }

    private void OnRealtimeConnected(bool active)
    {
        if (active)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (accountId is null || string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        latest = Array.Empty<NotificationDto>();
        serverUnread = null;
        seenIds.Clear();
        primed = false;
        cadence.RequestImmediate();
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
        var counts = serverUnread;
        if (counts is null)
        {
            return UnreadAfter(app, SeenUnix(app));
        }

        var pending = PendingAckWatermark(app);
        if (pending > 0)
        {
            return UnreadAfter(app, pending);
        }

        return counts.GetValueOrDefault(app, 0);
    }

    private int UnreadAfter(string app, long watermark)
    {
        var items = latest;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.App == app && !item.Read && item.CreatedAtUnix > watermark)
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

        notifications.RemoveSocial(app);
        var hadServerUnread = ClearServerUnread(app);
        var advances = newest > SeenUnix(app);
        if (advances)
        {
            configuration.SocialActivitySeenUnix[SeenKey(app)] = newest;
            configuration.Save();
        }

        if (!advances && !hadServerUnread)
        {
            return;
        }

        EnqueueAck(app, advances ? newest : NowUnix());
    }

    public void AcknowledgeAll()
    {
        var counts = serverUnread;
        var outstanding = false;
        if (counts is not null)
        {
            foreach (var count in counts.Values)
            {
                if (count > 0)
                {
                    outstanding = true;
                    break;
                }
            }
        }

        if (!outstanding && notifications.UnreadCount == 0)
        {
            return;
        }

        if (counts is not null && counts.Count > 0)
        {
            serverUnread = new Dictionary<string, int>();
        }

        EnqueueAck(null, NowUnix());
    }

    private bool ClearServerUnread(string app)
    {
        var counts = serverUnread;
        if (counts is null || counts.GetValueOrDefault(app, 0) <= 0)
        {
            return false;
        }

        var updated = new Dictionary<string, int>(counts);
        updated[app] = 0;
        serverUnread = updated;
        return true;
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void AcknowledgeUpTo(string app, long upToUnix)
    {
        if (upToUnix <= 0)
        {
            return;
        }

        if (upToUnix > SeenUnix(app))
        {
            configuration.SocialActivitySeenUnix[SeenKey(app)] = upToUnix;
            configuration.Save();
        }

        var counts = serverUnread;
        if (counts is not null)
        {
            var updated = new Dictionary<string, int>(counts);
            updated[app] = UnreadAfter(app, upToUnix);
            serverUnread = updated;
        }

        EnqueueAck(app, upToUnix);
    }

    private string SeenKey(string app)
    {
        var accountId = lastAccountId;
        return string.IsNullOrEmpty(accountId) ? app : accountId + ":" + app;
    }

    private long SeenUnix(string app)
    {
        if (configuration.SocialActivitySeenUnix.TryGetValue(SeenKey(app), out var value))
        {
            return value;
        }

        return configuration.SocialActivitySeenUnix.GetValueOrDefault(app, 0L);
    }

    private string? CurrentAccountId => session.CurrentUser?.Id ?? lastAccountId;

    private long PendingAckWatermark(string app)
    {
        var accountId = CurrentAccountId;
        return string.IsNullOrEmpty(accountId) ? 0 : acks.WatermarkFor(accountId, app);
    }

    private void EnqueueAck(string? app, long upToUnix)
    {
        var accountId = CurrentAccountId;
        if (string.IsNullOrEmpty(accountId) || !acks.Enqueue(accountId, app, upToUnix))
        {
            return;
        }

        nextAckFlushUtc = DateTime.MinValue;
    }

    private void FlushPendingAcks(DateTime nowUtc)
    {
        if (flushingAcks || nowUtc < nextAckFlushUtc)
        {
            return;
        }

        var accountId = CurrentAccountId;
        if (string.IsNullOrEmpty(accountId) || !acks.TryPeek(accountId, out var key, out var app, out var watermark))
        {
            return;
        }

        var scope = app ?? NotificationAckQueue.GlobalScope;
        flushingAcks = true;
        nextAckFlushUtc = nowUtc + AckRetryInterval;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await client.MarkNotificationsReadAsync(watermark, app, token).ConfigureAwait(false);
                if (result is null)
                {
                    AepLog.Warning($"[Notifications] read ack for {scope} did not land; retrying");
                    return;
                }

                await framework.RunOnFrameworkThread(() => ConfirmAck(key, watermark)).ConfigureAwait(false);
                cadence.RequestImmediate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notifications] read ack failed: {exception.Message}");
            }
            finally
            {
                flushingAcks = false;
            }
        });
    }

    private void ConfirmAck(string key, long watermark) => acks.ConfirmSent(key, watermark);

    public void RefreshNow()
    {
        if (session.IsSignedIn && AnyServedAppInstalled())
        {
            cadence.RequestImmediate();
        }
    }

    private bool AnyServedAppInstalled()
    {
        for (var index = 0; index < ServedApps.Length; index++)
        {
            if (installer.IsInstalled(ServedApps[index]))
            {
                return true;
            }
        }

        return false;
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn || !AnyServedAppInstalled())
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        FlushPendingAcks(nowUtc);
        if (polling || !cadence.Due(nowUtc))
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
                    serverUnread = page.UnreadByApp;
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
            if (SocialActivity.IsModerationNotice(item.Type) || !installer.IsInstalled(item.App))
            {
                continue;
            }

            if (wasPrimed && !item.Read && !seenIds.Contains(item.Id))
            {
                Present(item);
            }
        }

        seenIds.Clear();
        for (var index = 0; index < items.Length; index++)
        {
            seenIds.Add(items[index].Id);
        }

        latest = KeepInstalled(items);
        primed = true;
    }

    private NotificationDto[] KeepInstalled(NotificationDto[] items)
    {
        var installedCount = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (installer.IsInstalled(items[index].App))
            {
                installedCount++;
            }
        }

        if (installedCount == items.Length)
        {
            return items;
        }

        var kept = new NotificationDto[installedCount];
        var next = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (installer.IsInstalled(items[index].App))
            {
                kept[next++] = items[index];
            }
        }

        return kept;
    }

    private void Present(NotificationDto item)
    {
        var body = SocialActivity.Body(item);
        if (body.Length == 0)
        {
            return;
        }

        var title = AdTitle(item) ?? SocialActivity.ActorLabel(item);
        notifications.Notify(new PhoneNotification(item.App, title, body, DateTime.Now,
            AccentFor(item.App), GroupKeyFor(item))
        {
            ActorId = item.ActorId,
            PostId = item.PostId,
            SocialType = item.Type,
            CreatedAtUnix = item.CreatedAtUnix,
            ChannelId = item.Type == SocialActivity.TypeMissedCall ? NotificationChannels.PhoneChannel : null,
        });
    }

    private static string GroupKeyFor(NotificationDto item)
    {
        if (item.App == SocialActivity.YellowPagesApp)
        {
            return item.PostId ?? item.Id;
        }

        if (item.Type == SocialActivity.TypeMissedCall)
        {
            return "call:" + item.ActorId;
        }

        if (!string.IsNullOrEmpty(item.PostId))
        {
            return item.App + ":post:" + item.PostId;
        }

        return item.App + ":" + item.Type + ":" + item.ActorId;
    }

    private static string? AdTitle(NotificationDto item)
    {
        return item.Type switch
        {
            SocialActivity.TypeAdExpiring => Loc.T(L.YellowPages.NotifExpiringTitle),
            SocialActivity.TypeAdHidden => Loc.T(L.YellowPages.NotifHiddenTitle),
            SocialActivity.TypeAdOpened => Loc.T(L.YellowPages.NotifOpenedTitle),
            SocialActivity.TypeAdInquiry => Loc.T(L.YellowPages.NotifInquiryTitle),
            _ => null,
        };
    }

    private static Vector4 AccentFor(string app)
    {
        return app switch
        {
            SocialActivity.AethergramApp => AppAccents.For(SocialActivity.AethergramApp),
            SocialActivity.VelvetApp => AppAccents.For(SocialActivity.VelvetApp),
            SocialActivity.YellowPagesApp => AppAccents.For(SocialActivity.YellowPagesApp),
            SocialActivity.MessageApp => AppAccents.For(SocialActivity.MessageApp),
            SocialActivity.MusicApp => AppAccents.For(SocialActivity.MusicApp),
            _ => AppAccents.For(SocialActivity.ChirperApp),
        };
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.SocialPinged -= cadence.RequestImmediate;
        signals.ConnectedChanged -= OnRealtimeConnected;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
