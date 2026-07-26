using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class SocialNotificationService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);

    private static readonly string[] ServedApps =
    {
        SocialActivity.ChirperApp,
        SocialActivity.AethergramApp,
        SocialActivity.VelvetApp,
        SocialActivity.YellowPagesApp,
    };

    private readonly AethernetSession session;
    private readonly ConfirmService confirm;
    private readonly AppInstaller installer;
    private readonly AccountClient client;
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
    private string? lastAccountId;

    public SocialNotificationService(AethernetSession session, AccountClient client, NotificationService notifications,
        Configuration configuration, IFramework framework, PhoneVisibility visibility, RealtimeSignalBus signals,
        ConfirmService confirm, AppInstaller installer)
    {
        this.session = session;
        this.confirm = confirm;
        this.installer = installer;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.framework = framework;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        signals.SocialPinged += cadence.RequestImmediate;
        session.Changed += OnSessionChanged;
        framework.Update += OnFrameworkTick;
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        latest = Array.Empty<NotificationDto>();
        primed = false;
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
        if (session.IsSignedIn && AnyServedAppInstalled())
        {
            cadence.Mark(DateTime.UtcNow);
            Poll();
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
            primed = false;
            latest = Array.Empty<NotificationDto>();
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
        var moderationMarker = configuration.ModerationNoticeSeenUnix;
        var moderationSeeded = moderationMarker != 0;
        var newestModeration = moderationMarker;
        for (var index = items.Length - 1; index >= 0; index--)
        {
            var item = items[index];
            var isModeration = IsModerationNotice(item.Type);
            if (isModeration && item.CreatedAtUnix > newestModeration)
            {
                newestModeration = item.CreatedAtUnix;
            }

            if (!isModeration && !installer.IsInstalled(item.App))
            {
                continue;
            }

            var freshModeration = isModeration && moderationSeeded && item.CreatedAtUnix > moderationMarker;
            var freshSession = wasPrimed && !seenIds.Contains(item.Id);
            if (freshModeration || freshSession)
            {
                Present(item);
            }
        }

        seenIds.Clear();
        for (var index = 0; index < items.Length; index++)
        {
            seenIds.Add(items[index].Id);
        }

        if (newestModeration > configuration.ModerationNoticeSeenUnix)
        {
            configuration.ModerationNoticeSeenUnix = newestModeration;
            configuration.Save();
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

    private static bool IsModerationNotice(int type)
    {
        return type is SocialActivity.TypePostRemoved
            or SocialActivity.TypeWarning
            or SocialActivity.TypeReportUpdate;
    }

    private void Present(NotificationDto item)
    {
        var body = SocialActivity.Body(item);
        if (body.Length == 0)
        {
            return;
        }

        var moderationTitle = ModerationTitle(item);
        var title = moderationTitle ?? AdTitle(item) ?? SocialActivity.ActorLabel(item);
        var groupKey = item.App == SocialActivity.YellowPagesApp ? item.PostId : null;
        notifications.Notify(new PhoneNotification(item.App, title, body, DateTime.Now,
            AccentFor(item.App), groupKey)
        {
            ActorId = item.ActorId,
            PostId = item.PostId,
            SocialType = item.Type,
        });

        if (moderationTitle is not null)
        {
            var alertBody = item.Type == SocialActivity.TypePostRemoved
                ? $"{body}\n\n{Loc.T(L.Moderation.RemovedFooter)}"
                : body;
            confirm.Alert(moderationTitle, alertBody, Loc.T(L.Moderation.RemovedDismiss));
        }
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

    private static string? ModerationTitle(NotificationDto item)
    {
        return item.Type switch
        {
            SocialActivity.TypePostRemoved => Loc.T(string.IsNullOrEmpty(item.CommentId)
                ? L.Moderation.RemovedTitle
                : L.Moderation.RemovedCommentTitle),
            SocialActivity.TypeWarning => Loc.T(L.Moderation.WarningTitle),
            SocialActivity.TypeReportUpdate => Loc.T(L.Moderation.ReportUpdateTitle),
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
            _ => AppAccents.For(SocialActivity.ChirperApp),
        };
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.SocialPinged -= cadence.RequestImmediate;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
