using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Inventory;

internal sealed class InventoryCaptureService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;

    private readonly IFramework framework;
    private readonly InventoryStore store;

    private readonly object sync = new();

    private readonly List<InventoryStack> scratchBags = new();
    private readonly List<InventoryStack> scratchArmoury = new();
    private readonly List<InventoryStack> scratchCrystals = new();
    private readonly List<InventoryStack> scratchSaddlebag = new();
    private readonly List<InventoryStack> scratchEquipped = new();
    private readonly List<InventoryStack> scratchCached = new();

    private InventoryStack[] localBags = Array.Empty<InventoryStack>();
    private InventoryStack[] localArmoury = Array.Empty<InventoryStack>();
    private InventoryStack[] localCrystals = Array.Empty<InventoryStack>();
    private InventoryStack[] localSaddlebag = Array.Empty<InventoryStack>();
    private InventoryStack[] localEquipped = Array.Empty<InventoryStack>();

    private long lastTickMilliseconds;
    private ulong activeCharacterId;
    private bool hasLocal;

    public InventoryCaptureService(IFramework framework, InventoryStore store)
    {
        this.framework = framework;
        this.store = store;
        this.framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
    }

    public ulong ActiveCharacterId => activeCharacterId;

    public bool HasLocal => hasLocal;

    public void SnapshotLocal(List<InventoryStack> bags, List<InventoryStack> armoury, List<InventoryStack> crystals, List<InventoryStack> saddlebag, List<InventoryStack> equipped)
    {
        bags.Clear();
        armoury.Clear();
        crystals.Clear();
        saddlebag.Clear();
        equipped.Clear();

        lock (sync)
        {
            Copy(localBags, bags);
            Copy(localArmoury, armoury);
            Copy(localCrystals, crystals);
            Copy(localSaddlebag, saddlebag);
            Copy(localEquipped, equipped);
        }
    }

    public void CopyCachedSources(List<StoredSource> into)
    {
        store.CopySources(activeCharacterId, into);
    }

    private void OnUpdate(IFramework owner)
    {
        var now = Environment.TickCount64;
        if (now - lastTickMilliseconds < TickIntervalMilliseconds)
        {
            return;
        }

        lastTickMilliseconds = now;
        activeCharacterId = InventoryReader.ReadLocalContentId();

        RefreshLocal();
        CaptureRetainer();
        CaptureFreeCompany();
    }

    private void RefreshLocal()
    {
        if (!InventoryReader.ReadLocal(scratchBags, scratchArmoury, scratchCrystals, scratchSaddlebag, scratchEquipped))
        {
            hasLocal = false;
            return;
        }

        var bags = scratchBags.ToArray();
        var armoury = scratchArmoury.ToArray();
        var crystals = scratchCrystals.ToArray();
        var saddlebag = scratchSaddlebag.ToArray();
        var equipped = scratchEquipped.ToArray();

        lock (sync)
        {
            localBags = bags;
            localArmoury = armoury;
            localCrystals = crystals;
            localSaddlebag = saddlebag;
            localEquipped = equipped;
        }

        hasLocal = true;
    }

    private void CaptureRetainer()
    {
        if (activeCharacterId == 0 || !InventoryReader.ReadActiveRetainer(scratchCached, out var retainerId, out var retainerName))
        {
            return;
        }

        store.CaptureSource(activeCharacterId, BuildSource(InventorySourceKind.Retainer, retainerName, retainerId, scratchCached));
    }

    private void CaptureFreeCompany()
    {
        if (activeCharacterId == 0 || !InventoryReader.ReadFreeCompany(scratchCached, out var freeCompanyId, out var freeCompanyName))
        {
            return;
        }

        store.CaptureSource(activeCharacterId, BuildSource(InventorySourceKind.FreeCompany, freeCompanyName, freeCompanyId, scratchCached));
    }

    private static StoredSource BuildSource(InventorySourceKind kind, string ownerName, ulong ownerId, List<InventoryStack> stacks)
    {
        var stored = new StoredStack[stacks.Count];
        for (var index = 0; index < stacks.Count; index++)
        {
            var stack = stacks[index];
            stored[index] = new StoredStack
            {
                ItemId = stack.ItemId,
                Quantity = stack.Quantity,
                HighQuality = stack.HighQuality,
                Slot = stack.Slot,
            };
        }

        return new StoredSource
        {
            Kind = kind,
            OwnerName = ownerName,
            OwnerId = ownerId,
            CapturedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Stacks = stored,
        };
    }

    private static void Copy(InventoryStack[] from, List<InventoryStack> into)
    {
        for (var index = 0; index < from.Length; index++)
        {
            into.Add(from[index]);
        }
    }
}
