using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal sealed class LoadoutStore : IDisposable
{
    public const string BadgeKind = "flair";

    public const string FrameKind = "frame";

    public const int BadgeSlots = 2;

    private const long RefreshAfterMilliseconds = 60_000;
    private const long RefreshOnEnterMilliseconds = 5_000;
    private const long RetryAfterAttemptMilliseconds = 20_000;

    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly StoreWork work = new("Loadout");

    private volatile InventorySectionDto[] sections = Array.Empty<InventorySectionDto>();
    private volatile bool loadedOnce;
    private volatile bool equipping;
    private long loadedAtTick;
    private long attemptedAtTick;
    private int fetching;
    private string? lastAccountId;

    public LoadoutStore(AethernetSession session, AccountClient account)
    {
        this.session = session;
        this.account = account;
        session.Changed += OnSessionChanged;
    }

    public InventorySectionDto[] Sections => sections;

    public bool LoadedOnce => loadedOnce;

    public bool Equipping => equipping;

    public bool Fetching => Volatile.Read(ref fetching) != 0;

    public InventorySectionDto? Section(string kind)
    {
        var current = sections;
        for (var index = 0; index < current.Length; index++)
        {
            if (string.Equals(current[index].Kind, kind, StringComparison.Ordinal))
            {
                return current[index];
            }
        }

        return null;
    }

    public void EnsureFresh()
    {
        Refresh(RefreshAfterMilliseconds);
    }

    public void RefreshOnEnter()
    {
        Refresh(RefreshOnEnterMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
        Refresh(0);
    }

    public void Equip(string kind, string itemId, int? slot)
    {
        if (equipping || !session.IsSignedIn)
        {
            return;
        }

        equipping = true;
        work.Run("inventory equip", async token =>
        {
            var updated = await account.EquipAsync(kind, itemId, slot, token).ConfigureAwait(false);
            if (updated is not null)
            {
                sections = updated.Sections;
                loadedOnce = true;
                Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
                Interlocked.Exchange(ref attemptedAtTick, 0);
            }
        }, () => equipping = false);
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
        work.Run("inventory refresh", async token =>
        {
            var inventory = await account.InventoryAsync(token).ConfigureAwait(false);
            if (inventory is null)
            {
                return;
            }

            sections = inventory.Sections;
            loadedOnce = true;
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            Interlocked.Exchange(ref attemptedAtTick, 0);
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        sections = Array.Empty<InventorySectionDto>();
        loadedOnce = false;
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
