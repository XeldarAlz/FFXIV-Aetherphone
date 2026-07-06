using System.Numerics;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class SocialNotificationService : IDisposable
{
    private const int TypeLike = 0;
    private const int TypeComment = 1;
    private const int TypeFollow = 2;
    private const int TypeConnectRequest = 3;
    private const int TypeConnectAccept = 4;
    private const string ChirperApp = "chirper";
    private const string AethergramApp = "aethergram";
    private const string VelvetApp = "velvet";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly IFramework framework;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> seenIds = new();
    private volatile bool polling;
    private volatile bool primed;
    private DateTime lastPollUtc = DateTime.MinValue;

    public SocialNotificationService(AethernetSession session, AethernetClient client, NotificationService notifications,
        IFramework framework)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.framework = framework;
        framework.Update += OnFrameworkTick;
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            primed = false;
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastPollUtc < PollInterval)
        {
            return;
        }

        lastPollUtc = now;
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

        primed = true;
    }

    private void Present(NotificationDto item)
    {
        var body = BodyFor(item);
        if (body.Length == 0)
        {
            return;
        }

        notifications.Notify(new PhoneNotification(item.App, ActorLabel(item), body, DateTime.Now, AccentFor(item.App))
        {
            ActorId = item.ActorId,
            PostId = item.PostId,
            SocialType = item.Type,
        });
    }

    private static string ActorLabel(NotificationDto item)
    {
        if (!string.IsNullOrEmpty(item.ActorDisplayName))
        {
            return item.ActorDisplayName;
        }

        return string.IsNullOrEmpty(item.ActorHandle) ? item.ActorName : item.ActorHandle;
    }

    private static string BodyFor(NotificationDto item)
    {
        var isPhoto = item.App != ChirperApp;
        switch (item.Type)
        {
            case TypeLike:
                return Loc.T(isPhoto ? L.Social.LikedPhoto : L.Social.LikedChirp);
            case TypeComment:
                var action = Loc.T(isPhoto ? L.Social.CommentedPhoto : L.Social.CommentedChirp);
                return string.IsNullOrEmpty(item.Preview) ? action : $"{action}: “{item.Preview}”";
            case TypeFollow:
                return Loc.T(L.Social.Followed);
            case TypeConnectRequest:
                return Loc.T(L.Social.ConnectionRequest);
            case TypeConnectAccept:
                return Loc.T(L.Social.ConnectionAccepted);
            default:
                return string.Empty;
        }
    }

    private static Vector4 AccentFor(string app)
    {
        return app switch
        {
            AethergramApp => AppAccents.For(AethergramApp),
            VelvetApp => AppAccents.For(VelvetApp),
            _ => AppAccents.For(ChirperApp),
        };
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
