using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;

namespace Aetherphone.Core.Social;

internal sealed class BadgeCatalogStore : IDisposable
{
    private const long RefreshAfterMilliseconds = 600_000;
    private const long RefreshAfterUnknownIdMilliseconds = 30_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly StoreWork work = new("BadgeCatalog");

    private volatile Dictionary<string, BadgeStyle> stylesById = new(StringComparer.Ordinal);
    private long loadedAtTick;
    private long attemptedAtTick;
    private int fetching;

    public BadgeCatalogStore(AethernetSession session, AccountClient account)
    {
        this.session = session;
        this.account = account;
    }

    public BadgeStyle? Find(string badgeId)
    {
        if (stylesById.TryGetValue(badgeId, out var style))
        {
            EnsureFresh();
            return style;
        }

        Refresh(RefreshAfterUnknownIdMilliseconds);
        return null;
    }

    public void EnsureFresh()
    {
        Refresh(RefreshAfterMilliseconds);
    }

    private void Refresh(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref attemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref loadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref attemptedAtTick, now);
        work.Run("badge catalog refresh", async token =>
        {
            var catalog = await account.BadgeCatalogAsync(token).ConfigureAwait(false);
            if (catalog is null)
            {
                return;
            }

            var next = new Dictionary<string, BadgeStyle>(catalog.Badges.Length, StringComparer.Ordinal);
            for (var index = 0; index < catalog.Badges.Length; index++)
            {
                var style = BadgeStyle.From(catalog.Badges[index]);
                next[style.Id] = style;
            }

            stylesById = next;
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            AepLog.Debug($"[BadgeCatalog] loaded {next.Count} badges");
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
