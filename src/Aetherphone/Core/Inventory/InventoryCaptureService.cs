using Aetherphone.Core.Home;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Inventory;

internal sealed class InventorySnapshot
{
    public readonly List<InventorySource> Sources = new();
    public readonly List<RetainerSummary> Retainers = new();
    public DateTime RetainersCapturedUtc;
    public long Gil;
    public bool HasLocal;

    public void Clear()
    {
        Sources.Clear();
        Retainers.Clear();
        RetainersCapturedUtc = default;
        Gil = 0;
        HasLocal = false;
    }
}

internal sealed class InventoryCaptureService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private const int RosterTicks = 10;
    private static readonly InventorySourceKind[] LocalKinds =
    {
        InventorySourceKind.Inventory, InventorySourceKind.Equipped, InventorySourceKind.Armoury,
        InventorySourceKind.Crystals, InventorySourceKind.Saddlebag,
    };

    private readonly FrameworkTicker ticker;
    private readonly InventoryStore store;
    private readonly object sync = new();
    private readonly InventoryLocalRead localRead = new();
    private readonly InventoryReadBuffer cachedRead = new();
    private readonly List<RetainerSummary> rosterRead = new();
    private readonly List<StoredSource> storedScratch = new();
    private readonly List<StoredRetainer> retainerScratch = new();
    private readonly InventoryStack[][] localStacks = new InventoryStack[LocalKinds.Length][];
    private readonly int[] localCapacity = new int[LocalKinds.Length];
    private readonly CaptureGate saddlebagGate = new();
    private readonly CaptureGate retainerGate = new();
    private readonly CaptureGate freeCompanyGate = new();
    private int rosterCountdown;
    private ulong activeCharacterId;
    private ulong pendingRetainerId;
    private long localGil;
    private bool hasLocal;
    private bool saddlebagLive;
    private int localRevision;

    public InventoryCaptureService(IFramework framework, InventoryStore store, AppGate gate)
    {
        this.store = store;
        for (var index = 0; index < localStacks.Length; index++)
        {
            localStacks[index] = Array.Empty<InventoryStack>();
        }

        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    public int Revision => Volatile.Read(ref localRevision) + store.Revision;

    public void Fill(InventorySnapshot snapshot)
    {
        snapshot.Clear();
        var characterId = activeCharacterId;
        var saddlebagIsLive = false;
        lock (sync)
        {
            snapshot.HasLocal = hasLocal;
            snapshot.Gil = localGil;
            if (hasLocal)
            {
                saddlebagIsLive = saddlebagLive;
                var nowUtc = DateTime.UtcNow;
                for (var index = 0; index < LocalKinds.Length; index++)
                {
                    var kind = LocalKinds[index];
                    if (kind == InventorySourceKind.Saddlebag && !saddlebagIsLive)
                    {
                        continue;
                    }

                    snapshot.Sources.Add(new InventorySource(kind, string.Empty, characterId, localStacks[index],
                        nowUtc, localCapacity[index], false));
                }
            }
        }

        store.CopySources(characterId, storedScratch);
        for (var index = 0; index < storedScratch.Count; index++)
        {
            var stored = storedScratch[index];
            if (stored.Kind == InventorySourceKind.Saddlebag && saddlebagIsLive)
            {
                continue;
            }

            snapshot.Sources.Add(ToSource(stored));
        }

        var rosterUnix = store.CopyRetainers(characterId, retainerScratch);
        snapshot.RetainersCapturedUtc = rosterUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(rosterUnix).UtcDateTime
            : default;
        for (var index = 0; index < retainerScratch.Count; index++)
        {
            var retainer = retainerScratch[index];
            snapshot.Retainers.Add(new RetainerSummary(retainer.RetainerId, retainer.Name, retainer.Gil,
                retainer.ItemCount, retainer.MarketCount));
        }

        storedScratch.Clear();
        retainerScratch.Clear();
    }

    private void OnTick()
    {
        activeCharacterId = InventoryReader.ReadLocalContentId();
        RefreshLocal();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CaptureSaddlebag(nowUnix);
        CaptureRoster(nowUnix);
        CaptureRetainer(nowUnix);
        CaptureFreeCompany(nowUnix);
    }

    private void RefreshLocal()
    {
        if (!InventoryReader.ReadLocal(localRead))
        {
            if (hasLocal)
            {
                hasLocal = false;
                Interlocked.Increment(ref localRevision);
            }

            return;
        }

        var changed = !hasLocal || localRead.Gil != localGil ||
                      saddlebagLive != (localRead.Saddlebag.LoadedPages != 0);
        lock (sync)
        {
            changed |= Assign(0, localRead.Bags);
            changed |= Assign(1, localRead.Equipped);
            changed |= Assign(2, localRead.Armoury);
            changed |= Assign(3, localRead.Crystals);
            changed |= Assign(4, localRead.Saddlebag);
            localGil = localRead.Gil;
            saddlebagLive = localRead.Saddlebag.LoadedPages != 0;
            hasLocal = true;
        }

        if (changed)
        {
            Interlocked.Increment(ref localRevision);
        }
    }

    private bool Assign(int index, InventoryReadBuffer read)
    {
        var capacityChanged = localCapacity[index] != read.Capacity;
        localCapacity[index] = read.Capacity;
        if (!capacityChanged && SameStacks(localStacks[index], read.Stacks))
        {
            return false;
        }

        localStacks[index] = read.Stacks.ToArray();
        return true;
    }

    private void CaptureSaddlebag(long nowUnix)
    {
        if (activeCharacterId == 0 || !hasLocal || localRead.Saddlebag.LoadedPages == 0 ||
            !saddlebagGate.ShouldCapture(activeCharacterId, localRead.Saddlebag, nowUnix))
        {
            return;
        }

        store.CaptureSource(activeCharacterId, BuildSource(InventorySourceKind.Saddlebag, string.Empty,
            activeCharacterId, localRead.Saddlebag, nowUnix));
    }

    private void CaptureRoster(long nowUnix)
    {
        if (rosterCountdown > 0 && pendingRetainerId == 0)
        {
            rosterCountdown--;
            return;
        }

        rosterCountdown = RosterTicks;
        if (activeCharacterId == 0 || !InventoryReader.ReadRetainerRoster(rosterRead))
        {
            return;
        }

        store.CaptureRetainers(activeCharacterId, rosterRead, nowUnix);
    }

    private void CaptureRetainer(long nowUnix)
    {
        if (activeCharacterId == 0 ||
            !InventoryReader.ReadActiveRetainer(cachedRead, out var retainerId, out var retainerName))
        {
            pendingRetainerId = 0;
            return;
        }

        if (pendingRetainerId != retainerId)
        {
            pendingRetainerId = retainerId;
            return;
        }

        if (!retainerGate.ShouldCapture(retainerId, cachedRead, nowUnix))
        {
            return;
        }

        store.CaptureSource(activeCharacterId,
            BuildSource(InventorySourceKind.Retainer, retainerName, retainerId, cachedRead, nowUnix));
    }

    private void CaptureFreeCompany(long nowUnix)
    {
        if (activeCharacterId == 0 ||
            !InventoryReader.ReadFreeCompany(cachedRead, out var freeCompanyId, out var freeCompanyName) ||
            !freeCompanyGate.ShouldCapture(freeCompanyId, cachedRead, nowUnix))
        {
            return;
        }

        store.CaptureSource(activeCharacterId,
            BuildSource(InventorySourceKind.FreeCompany, freeCompanyName, freeCompanyId, cachedRead, nowUnix));
    }

    private static StoredSource BuildSource(InventorySourceKind kind, string ownerName, ulong ownerId,
        InventoryReadBuffer read, long nowUnix)
    {
        var stacks = read.Stacks;
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
                Page = stack.Page,
            };
        }

        return new StoredSource
        {
            Kind = kind,
            OwnerName = ownerName,
            OwnerId = ownerId,
            CapturedUnix = nowUnix,
            Capacity = read.Capacity,
            LoadedPages = read.LoadedPages,
            Stacks = stored,
        };
    }

    private static InventorySource ToSource(StoredSource stored)
    {
        var stacks = new InventoryStack[stored.Stacks.Length];
        for (var index = 0; index < stacks.Length; index++)
        {
            var stack = stored.Stacks[index];
            stacks[index] = new InventoryStack(stack.ItemId, stack.Quantity, stack.HighQuality, stack.Slot,
                stack.Page);
        }

        var captured = DateTimeOffset.FromUnixTimeSeconds(stored.CapturedUnix).UtcDateTime;
        return new InventorySource(stored.Kind, stored.OwnerName, stored.OwnerId, stacks, captured, stored.Capacity,
            true);
    }

    private static bool SameStacks(InventoryStack[] current, List<InventoryStack> read)
    {
        if (current.Length != read.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Length; index++)
        {
            if (!current[index].Equals(read[index]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class CaptureGate
    {
        private const long RefreshSeconds = 60;
        private readonly List<InventoryStack> last = new();
        private ulong ownerId;
        private int capacity;
        private ushort pages;
        private long capturedUnix;

        public bool ShouldCapture(ulong owner, InventoryReadBuffer read, long nowUnix)
        {
            if (owner == ownerId && capacity == read.Capacity && pages == read.LoadedPages &&
                nowUnix - capturedUnix < RefreshSeconds && SameStacks(last, read.Stacks))
            {
                return false;
            }

            ownerId = owner;
            capacity = read.Capacity;
            pages = read.LoadedPages;
            capturedUnix = nowUnix;
            last.Clear();
            last.AddRange(read.Stacks);
            return true;
        }

        private static bool SameStacks(List<InventoryStack> left, List<InventoryStack> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
