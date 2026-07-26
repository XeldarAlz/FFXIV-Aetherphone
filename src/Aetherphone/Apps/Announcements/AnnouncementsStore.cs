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
    private volatile bool loading;
    private volatile bool loadedOnce;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;

    public AnnouncementsStore(AethernetSession session, AnnouncementsClient client,
        NotificationService notifications, Configuration configuration)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public AnnouncementDto[] Announcements => announcements;

    public bool Loading => loading;

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
            var page = await client.ListAsync(token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            announcements = page.Items;
            loadedOnce = true;
            Announce(page.Items);
        }, () => loading = false);
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
        if (now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

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
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
