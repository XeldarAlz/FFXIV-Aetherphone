using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.Coins;

internal sealed class HalloweenBadgeStore : IDisposable
{
    private const int NotFoundStatus = 404;
    private const long FailureBackoffMilliseconds = 60_000;
    private const long MissingBackoffMilliseconds = 15 * 60_000;
    private const long HeldBackMilliseconds = 5 * 60_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly CoinCatalogStore catalog;
    private readonly LoadoutStore loadout;
    private readonly StoreWork work = new("HalloweenBadge");

    private volatile HalloweenTreatsDto? progress;
    private long blockedUntilTick;
    private int reporting;
    private int generation;
    private string? lastAccountId;

    public HalloweenBadgeStore(AethernetSession session, CoinsClient coins, CoinCatalogStore catalog,
        LoadoutStore loadout)
    {
        this.session = session;
        this.coins = coins;
        this.catalog = catalog;
        this.loadout = loadout;
        lastAccountId = session.CurrentUser?.Id;
        session.Changed += OnSessionChanged;
    }

    public bool Granted => progress?.Granted == true;

    public bool Waiting => Environment.TickCount64 < Interlocked.Read(ref blockedUntilTick);

    public void Report(int found)
    {
        var known = progress;
        if (!session.IsSignedIn || known is { Granted: true } || (known is not null && known.Found >= found)
            || Environment.TickCount64 < Interlocked.Read(ref blockedUntilTick)
            || Interlocked.Exchange(ref reporting, 1) != 0)
        {
            return;
        }

        var ticket = Volatile.Read(ref generation);
        work.Run("report", async token =>
        {
            var status = 0;
            var fresh = await coins.ReportHalloweenTreatsAsync(found, token, failure => status = failure.StatusCode)
                .ConfigureAwait(false);
            if (Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            if (fresh is null)
            {
                Block(status == NotFoundStatus ? MissingBackoffMilliseconds : FailureBackoffMilliseconds);
                return;
            }

            var unlocked = fresh.Granted && !Granted;
            progress = fresh;
            if (fresh.Found < found)
            {
                Block(HeldBackMilliseconds);
            }

            if (unlocked)
            {
                catalog.RefreshNow();
                loadout.RefreshNow();
            }
        }, () => Interlocked.Exchange(ref reporting, 0));
    }

    private void Block(long milliseconds) =>
        Interlocked.Exchange(ref blockedUntilTick, Environment.TickCount64 + milliseconds);

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        Interlocked.Increment(ref generation);
        progress = null;
        Interlocked.Exchange(ref blockedUntilTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
