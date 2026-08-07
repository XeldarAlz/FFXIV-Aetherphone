using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Moderation;

internal sealed class ModerationNoticeArchive
{
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private volatile ModerationNoticeDto[] items = Array.Empty<ModerationNoticeDto>();
    private volatile string? nextCursor;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile int pendingCount;
    private string? lastAccountId;

    public ModerationNoticeArchive(AethernetSession session, AccountClient client)
    {
        this.session = session;
        this.client = client;
    }

    public ModerationNoticeDto[] Items => items;

    public bool Loading => loading;

    public bool Loaded => loaded;

    public bool HasMore => nextCursor is not null;

    public int PendingCount => pendingCount;

    public void Invalidate()
    {
        loaded = false;
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            items = Array.Empty<ModerationNoticeDto>();
            nextCursor = null;
            loaded = false;
        }

        if (!loaded && !loading)
        {
            Fetch(null);
        }
    }

    public void LoadMore()
    {
        if (loading || nextCursor is null)
        {
            return;
        }

        Fetch(nextCursor);
    }

    private void Fetch(string? cursor)
    {
        loading = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await client.NoticesAsync(cursor, CancellationToken.None).ConfigureAwait(false);
                if (page is not null)
                {
                    Apply(cursor, page);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notices] history fetch failed: {exception.Message}");
            }
            finally
            {
                loading = false;
            }
        });
    }

    private void Apply(string? cursor, ModerationNoticePage page)
    {
        if (cursor is null)
        {
            items = page.Items;
        }
        else
        {
            var current = items;
            var merged = new ModerationNoticeDto[current.Length + page.Items.Length];
            current.CopyTo(merged, 0);
            page.Items.CopyTo(merged, current.Length);
            items = merged;
        }

        pendingCount = page.PendingCount;
        nextCursor = page.NextCursor;
        loaded = true;
    }
}
