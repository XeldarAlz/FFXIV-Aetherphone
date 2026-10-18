using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Coins;

internal sealed class HalloweenBadgeStore : IDisposable
{
    private const int NotFoundStatus = 404;
    private const long FailureBackoffMilliseconds = 60_000;
    private const long MissingBackoffMilliseconds = 15 * 60_000;
    private const long HeldBackMilliseconds = 5 * 60_000;
    private const long RefreshMilliseconds = 3 * 60_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly CoinCatalogStore catalog;
    private readonly LoadoutStore loadout;
    private readonly StoreWork work = new("HalloweenBadge");

    private volatile HalloweenTreatsDto? progress;
    private long blockedUntilTick;
    private long refreshDueTick;
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
        Prime();
    }

    public bool Granted => progress?.Granted == true;

    public int Found => progress?.Found ?? 0;

    public int Generation => Volatile.Read(ref generation);

    public bool Waiting => Environment.TickCount64 < Interlocked.Read(ref blockedUntilTick);

    public void Report(int found) => Send(found, false);

    public void Refresh(int found) => Send(found, true);

    private void Send(int found, bool refresh)
    {
        if (!session.IsSignedIn || !Due(progress, found, refresh)
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
            Interlocked.Exchange(ref refreshDueTick, Environment.TickCount64 + RefreshMilliseconds);
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

    private bool Due(HalloweenTreatsDto? known, int found, bool refresh)
    {
        if (known is { Granted: true })
        {
            return false;
        }

        if (found > (known?.Found ?? 0))
        {
            return true;
        }

        return refresh && (known is null || Environment.TickCount64 >= Interlocked.Read(ref refreshDueTick));
    }

    private void Prime()
    {
        if (session.IsSignedIn && SeasonalTheme.IsHalloweenDate(DateTime.Today))
        {
            Send(0, true);
        }
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
        Interlocked.Exchange(ref refreshDueTick, 0);
        Prime();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
