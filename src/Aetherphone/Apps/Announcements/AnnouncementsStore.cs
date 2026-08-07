using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Announcements;

internal sealed class AnnouncementsStore : IDisposable
{
    public const string AppId = "announcements";

    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly AethernetSession session;
    private readonly AnnouncementsClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly StoreWork work = new StoreWork("Announcements");

    private volatile AnnouncementDto[] announcements = Array.Empty<AnnouncementDto>();
    private volatile string? announcementsCursor;
    private volatile bool loadingMore;
    private volatile bool pagedDeeper;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool pingRefreshRequested;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;
    private readonly RealtimeSignalBus signals;

    public AnnouncementsStore(AethernetSession session, AnnouncementsClient client,
        NotificationService notifications, Configuration configuration, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.signals = signals;
        signals.AnnouncementsPinged += OnAnnouncementsPinged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnAnnouncementsPinged()
    {
        pingRefreshRequested = true;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public AnnouncementDto[] Announcements => announcements;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool HasMore => announcementsCursor is not null;

    public bool LoadedOnce => loadedOnce;

    public int UnreadCount
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return 0;
            }

            var snapshot = announcements;
            var seenUnix = configuration.AnnouncementsSeenUnix;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].CreatedAtUnix > seenUnix)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsUnread(AnnouncementDto announcement) =>
        announcement.CreatedAtUnix > configuration.AnnouncementsSeenUnix;

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        work.Run("announcements refresh", async token =>
        {
            var page = await client.ListAsync(null, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            if (pagedDeeper)
            {
                announcements = IdentifiedMerge.MergeById(announcements, page.Items, ByNewestFirst);
            }
            else
            {
                announcements = page.Items;
                announcementsCursor = page.NextCursor;
            }

            loadedOnce = true;
            Announce(page.Items);
        }, () => loading = false);
    }

    public void LoadMore()
    {
        var cursor = announcementsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        pagedDeeper = true;
        work.Run("announcements more", async token =>
        {
            var page = await client.ListAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            announcements = IdentifiedMerge.MergeById(announcements, page.Items, ByNewestFirst);
            announcementsCursor = page.NextCursor;
        }, () => loadingMore = false);
    }

    private static int ByNewestFirst(AnnouncementDto left, AnnouncementDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void MarkAllSeen()
    {
        var newest = NewestUnix(announcements);
        if (newest <= configuration.AnnouncementsSeenUnix)
        {
            return;
        }

        configuration.AnnouncementsSeenUnix = newest;
        configuration.Save();
    }

    private void Announce(AnnouncementDto[] items)
    {
        var newest = NewestUnix(items);
        if (!configuration.AnnouncementsInitialized)
        {
            configuration.AnnouncementsInitialized = true;
            configuration.AnnouncementsNotifiedUnix = newest;
            configuration.AnnouncementsSeenUnix = newest;
            configuration.Save();
            return;
        }

        var notifiedUnix = configuration.AnnouncementsNotifiedUnix;
        if (newest <= notifiedUnix)
        {
            return;
        }

        var accent = AppAccents.For(AppId);
        for (var index = items.Length - 1; index >= 0; index--)
        {
            var announcement = items[index];
            if (announcement.CreatedAtUnix <= notifiedUnix)
            {
                continue;
            }

            var text = AnnouncementText.For(announcement);
            notifications.Notify(new PhoneNotification(AppId, text.Title, text.Body, DateTime.Now, accent,
                announcement.Id));
        }

        configuration.AnnouncementsNotifiedUnix = newest;
        configuration.Save();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!pingRefreshRequested && now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

        pingRefreshRequested = false;
        lastBackgroundRefreshUtc = now;
        Refresh();
    }

    private static long NewestUnix(AnnouncementDto[] items)
    {
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].CreatedAtUnix > newest)
            {
                newest = items[index].CreatedAtUnix;
            }
        }

        return newest;
    }

    public void Dispose()
    {
        signals.AnnouncementsPinged -= OnAnnouncementsPinged;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
