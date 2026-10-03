using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using EmoteSheet = Lumina.Excel.Sheets.Emote;

namespace Aetherphone.Core.Collections;

internal sealed unsafe class CollectionsJournal : IDisposable
{
    public const string AppId = "collections";
    private const long TickIntervalMilliseconds = 2000;
    private const int SettleTicks = 3;
    private const int NameWaitTicks = 8;
    private const int GroupedNoticeThreshold = 3;
    private const string GroupKey = "collections.unlocks";
    private static readonly Vector4 Accent = AppAccents.For(AppId);
    private static readonly int AllLocalMask = BuildLocalMask();

    private readonly IUnlockState unlockState;
    private readonly GameData gameData;
    private readonly CollectionsCatalogService catalog;
    private readonly NotificationService notifications;
    private readonly CollectionLedgerStore store;
    private readonly FrameworkTicker ticker;
    private readonly List<int> gained = new();
    private readonly List<PendingNotice> pending = new();
    private CollectionLedger ledger = new();
    private ulong contentId;
    private int settle;
    private bool baselined;
    private int dirtyMask;
    private int revision;

    public CollectionsJournal(IFramework framework, IUnlockState unlockState, GameData gameData,
        CollectionsCatalogService catalog, NotificationService notifications, DirectoryInfo configDirectory,
        AppGate gate)
    {
        this.unlockState = unlockState;
        this.gameData = gameData;
        this.catalog = catalog;
        this.notifications = notifications;
        store = new CollectionLedgerStore(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Collections")));
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
        unlockState.Unlock += OnUnlock;
    }

    public bool IsTracking => contentId != 0;
    public int Revision => revision;
    public IReadOnlyList<CollectionUnlock> Recent => ledger.Recent;
    public IReadOnlyList<CollectionPin> Pins => ledger.Pins;

    public bool IsPinned(CollectionCategory category, int id) => contentId != 0 && ledger.IsPinned(category, id);

    public bool TogglePin(CollectionCategory category, int id)
    {
        if (contentId == 0)
        {
            return false;
        }

        var pinned = ledger.TogglePin(category, id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        revision++;
        store.Save(contentId, ledger);
        return pinned;
    }

    public void Dispose()
    {
        unlockState.Unlock -= OnUnlock;
        ticker.Dispose();
    }

    private void OnUnlock(RowRef rowRef)
    {
        var mask = MaskFor(rowRef);
        if (mask != 0)
        {
            Interlocked.Or(ref dirtyMask, mask);
        }
    }

    private static int MaskFor(RowRef rowRef)
    {
        if (rowRef.Is<Mount>())
        {
            return Bit(CollectionCategory.Mounts);
        }

        if (rowRef.Is<Companion>())
        {
            return Bit(CollectionCategory.Minions);
        }

        if (rowRef.Is<EmoteSheet>())
        {
            return Bit(CollectionCategory.Emotes);
        }

        if (rowRef.Is<Orchestrion>())
        {
            return Bit(CollectionCategory.Orchestrions);
        }

        if (rowRef.Is<CharaMakeCustomize>())
        {
            return Bit(CollectionCategory.Hairstyles);
        }

        if (rowRef.Is<Glasses>() || rowRef.Is<GlassesStyle>())
        {
            return Bit(CollectionCategory.Facewear);
        }

        if (rowRef.Is<TripleTriadCard>())
        {
            return Bit(CollectionCategory.TriadCards);
        }

        return rowRef.Is<Item>() ? AllLocalMask : 0;
    }

    private static int Bit(CollectionCategory category) => 1 << (int)category;

    private static int BuildLocalMask()
    {
        var mask = 0;
        var categories = CollectionCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            if (CollectionsCatalogService.HasLocalUnlocks(categories[index]))
            {
                mask |= Bit(categories[index]);
            }
        }

        return mask;
    }

    private void OnTick()
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Collections journal tick failed");
        }
    }

    private void Tick()
    {
        var playerState = PlayerState.Instance();
        var current = playerState is null ? 0UL : playerState->ContentId;
        if (current == 0)
        {
            Forget();
            return;
        }

        if (current != contentId)
        {
            contentId = current;
            ledger = store.Load(contentId);
            settle = SettleTicks;
            baselined = false;
            pending.Clear();
            Interlocked.Exchange(ref dirtyMask, 0);
            revision++;
        }

        if (gameData.LocalPlayer is null)
        {
            return;
        }

        if (settle > 0)
        {
            settle--;
            return;
        }

        if (!baselined)
        {
            baselined = true;
            Interlocked.Exchange(ref dirtyMask, 0);
            Scan(AllLocalMask, false);
            return;
        }

        var mask = Interlocked.Exchange(ref dirtyMask, 0);
        if (mask != 0)
        {
            Scan(mask, true);
        }

        FlushPending();
    }

    private void Forget()
    {
        if (contentId == 0)
        {
            return;
        }

        contentId = 0;
        ledger = new CollectionLedger();
        pending.Clear();
        baselined = false;
        revision++;
    }

    private void Scan(int mask, bool live)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var changed = false;
        var categories = CollectionCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            if ((mask & Bit(category)) == 0)
            {
                continue;
            }

            var local = catalog.ScanLocal(category);
            if (local is null)
            {
                continue;
            }

            var outcome = ledger.Observe(category, local.OwnedIds, nowUnix, gained, live);
            if (outcome == UnlockScanOutcome.Skipped || outcome == UnlockScanOutcome.Unchanged)
            {
                continue;
            }

            changed = true;
            if (outcome != UnlockScanOutcome.Gained)
            {
                continue;
            }

            QueueNotices(category);
        }

        if (!changed)
        {
            return;
        }

        revision++;
        store.Save(contentId, ledger);
    }

    private void QueueNotices(CollectionCategory category)
    {
        if (gained.Count > GroupedNoticeThreshold)
        {
            notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Collections.NotifyManyTitle),
                Loc.T(L.Collections.NotifyManyBody, gained.Count), DateTime.Now, Accent, GroupKey));
            return;
        }

        catalog.RequestCatalog(category);
        for (var index = 0; index < gained.Count; index++)
        {
            pending.Add(new PendingNotice(category, gained[index]));
        }
    }

    private void FlushPending()
    {
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            var notice = pending[index];
            var entry = catalog.RequestCatalog(notice.Category);
            var item = entry.Find(notice.Id);
            var waiting = item is null && entry.State == CollectionState.Loading && notice.Attempts < NameWaitTicks;
            if (waiting)
            {
                pending[index] = notice with { Attempts = notice.Attempts + 1 };
                continue;
            }

            pending.RemoveAt(index);
            var body = item is null ? Loc.T(L.Collections.NotifyFallbackBody) : item.Name;
            notifications.Notify(new PhoneNotification(AppId, Loc.T(CollectionText.NewTitle(notice.Category)), body,
                DateTime.Now, Accent, GroupKey));
        }
    }

    private readonly record struct PendingNotice(CollectionCategory Category, int Id, int Attempts = 0);
}
