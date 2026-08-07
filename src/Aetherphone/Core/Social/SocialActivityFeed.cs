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
            var current = items;
            if (current.Length == 0)
            {
                items = page.Items;
                nextCursor = page.NextCursor;
            }
            else
            {
                items = IdentifiedMerge.MergeById(current, page.Items, ByNewestFirst);
            }
        }
        else
        {
            items = IdentifiedMerge.MergeById(items, page.Items, ByNewestFirst);
            nextCursor = page.NextCursor;
        }

        loaded = true;
    }

    private static int ByNewestFirst(NotificationDto left, NotificationDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }
}
