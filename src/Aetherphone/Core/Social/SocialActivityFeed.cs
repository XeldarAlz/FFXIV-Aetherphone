using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal sealed class SocialActivityFeed
{
    private readonly string app;
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private volatile NotificationDto[] items = Array.Empty<NotificationDto>();
    private volatile string? nextCursor;
    private volatile bool loading;
    private volatile bool loaded;
    private string? lastAccountId;

    public SocialActivityFeed(string app, AethernetSession session, AccountClient client)
    {
        this.app = app;
        this.session = session;
        this.client = client;
    }

    public NotificationDto[] Items => items;

    public void Invalidate()
    {
        loaded = false;
    }

    public void EnsureFresh(NotificationDto[] latestGlobal)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            items = Array.Empty<NotificationDto>();
            nextCursor = null;
            loaded = false;
        }

        if (loading)
        {
            return;
        }

        if (!loaded || HasNewerThanLoaded(latestGlobal))
        {
            Fetch(null);
        }
    }

    public void LoadOlder()
    {
        if (loading || nextCursor is null)
        {
            return;
        }

        Fetch(nextCursor);
    }

    private bool HasNewerThanLoaded(NotificationDto[] latestGlobal)
    {
        var current = items;
        var newestLoaded = current.Length > 0 ? current[0].CreatedAtUnix : 0L;
        for (var index = 0; index < latestGlobal.Length; index++)
        {
            if (latestGlobal[index].App == app && latestGlobal[index].CreatedAtUnix > newestLoaded)
            {
                return true;
            }
        }

        return false;
    }

    private void Fetch(string? cursor)
    {
        loading = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await client.NotificationsAsync(app, cursor, CancellationToken.None).ConfigureAwait(false);
                if (page is not null)
                {
                    Apply(cursor, page);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Activity] {app} fetch failed: {exception.Message}");
            }
            finally
            {
                loading = false;
            }
        });
    }

    private void Apply(string? cursor, NotificationPage page)
    {
        if (cursor is null)
        {
            items = page.Items;
        }
        else
        {
            var current = items;
            var merged = new NotificationDto[current.Length + page.Items.Length];
            current.CopyTo(merged, 0);
            page.Items.CopyTo(merged, current.Length);
            items = merged;
        }

        nextCursor = page.NextCursor;
        loaded = true;
    }
}
