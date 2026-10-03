using Newtonsoft.Json;

namespace Aetherphone.Core.Inventory;

internal sealed class StoredStack
{
    [JsonProperty("i")] public uint ItemId { get; set; }
    [JsonProperty("q")] public int Quantity { get; set; }
    [JsonProperty("h")] public bool HighQuality { get; set; }
    [JsonProperty("s")] public int Slot { get; set; }
    [JsonProperty("p")] public int Page { get; set; }
}

internal sealed class StoredSource
{
    [JsonProperty("kind")] public InventorySourceKind Kind { get; set; }
    [JsonProperty("ownerName")] public string OwnerName { get; set; } = string.Empty;
    [JsonProperty("ownerId")] public ulong OwnerId { get; set; }
    [JsonProperty("capturedUnix")] public long CapturedUnix { get; set; }
    [JsonProperty("capacity")] public int Capacity { get; set; }
    [JsonProperty("pages")] public int LoadedPages { get; set; }
    [JsonProperty("stacks")] public StoredStack[] Stacks { get; set; } = Array.Empty<StoredStack>();
}

internal sealed class StoredRetainer
{
    [JsonProperty("id")] public ulong RetainerId { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("gil")] public long Gil { get; set; }
    [JsonProperty("items")] public int ItemCount { get; set; }
    [JsonProperty("market")] public int MarketCount { get; set; }
}

internal sealed class StoredCharacter
{
    [JsonProperty("sources")] public List<StoredSource> Sources { get; set; } = new();
    [JsonProperty("retainers")] public List<StoredRetainer> Retainers { get; set; } = new();
    [JsonProperty("retainersUnix")] public long RetainersCapturedUnix { get; set; }
}

internal sealed class InventoryStore
{
    private const long RefreshPersistSeconds = 60;
    private readonly object sync = new();
    private readonly DirectoryInfo root;
    private readonly Dictionary<ulong, StoredCharacter> characters = new();
    private readonly Dictionary<ulong, long> persistedUnix = new();
    private int revision;

    public InventoryStore(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public int Revision => Volatile.Read(ref revision);

    public void CaptureSource(ulong characterId, StoredSource source)
    {
        if (characterId == 0 || source.OwnerId == 0)
        {
            return;
        }

        lock (sync)
        {
            var character = EnsureLoaded(characterId);
            var index = IndexOf(character.Sources, source.Kind, source.OwnerId);
            var existing = index >= 0 ? character.Sources[index] : null;
            var merged = InventorySourceMerge.Merge(existing, source);
            var changed = existing is null || !InventorySourceMerge.SameContent(existing, merged);
            if (index >= 0)
            {
                character.Sources[index] = merged;
            }
            else
            {
                character.Sources.Add(merged);
            }

            Commit(characterId, character, changed, source.CapturedUnix);
        }
    }

    public void CaptureRetainers(ulong characterId, List<RetainerSummary> roster, long nowUnix)
    {
        if (characterId == 0 || roster.Count == 0)
        {
            return;
        }

        lock (sync)
        {
            var character = EnsureLoaded(characterId);
            if (SameRoster(character.Retainers, roster))
            {
                return;
            }

            var retainers = new List<StoredRetainer>(roster.Count);
            for (var index = 0; index < roster.Count; index++)
            {
                var summary = roster[index];
                retainers.Add(new StoredRetainer
                {
                    RetainerId = summary.RetainerId,
                    Name = summary.Name,
                    Gil = summary.Gil,
                    ItemCount = summary.ItemCount,
                    MarketCount = summary.MarketCount,
                });
            }

            character.Retainers = retainers;
            character.RetainersCapturedUnix = nowUnix;
            Commit(characterId, character, true, nowUnix);
        }
    }

    public void CopySources(ulong characterId, List<StoredSource> into)
    {
        into.Clear();
        if (characterId == 0)
        {
            return;
        }

        lock (sync)
        {
            var character = EnsureLoaded(characterId);
            for (var index = 0; index < character.Sources.Count; index++)
            {
                into.Add(character.Sources[index]);
            }
        }
    }

    public long CopyRetainers(ulong characterId, List<StoredRetainer> into)
    {
        into.Clear();
        if (characterId == 0)
        {
            return 0;
        }

        lock (sync)
        {
            var character = EnsureLoaded(characterId);
            for (var index = 0; index < character.Retainers.Count; index++)
            {
                into.Add(character.Retainers[index]);
            }

            return character.RetainersCapturedUnix;
        }
    }

    private void Commit(ulong characterId, StoredCharacter character, bool changed, long nowUnix)
    {
        if (changed)
        {
            Interlocked.Increment(ref revision);
        }

        persistedUnix.TryGetValue(characterId, out var lastPersist);
        if (!changed && nowUnix - lastPersist < RefreshPersistSeconds)
        {
            return;
        }

        persistedUnix[characterId] = nowUnix;
        Persist(characterId, character);
    }

    private static bool SameRoster(List<StoredRetainer> stored, List<RetainerSummary> roster)
    {
        if (stored.Count != roster.Count)
        {
            return false;
        }

        for (var index = 0; index < roster.Count; index++)
        {
            var left = stored[index];
            var right = roster[index];
            if (left.RetainerId != right.RetainerId || left.Gil != right.Gil || left.ItemCount != right.ItemCount ||
                left.MarketCount != right.MarketCount || !string.Equals(left.Name, right.Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf(List<StoredSource> sources, InventorySourceKind kind, ulong ownerId)
    {
        for (var index = 0; index < sources.Count; index++)
        {
            var existing = sources[index];
            if (existing.Kind == kind && existing.OwnerId == ownerId)
            {
                return index;
            }
        }

        return -1;
    }

    private StoredCharacter EnsureLoaded(ulong characterId)
    {
        if (characters.TryGetValue(characterId, out var existing))
        {
            return existing;
        }

        var character = TryLoad(characterId) ?? new StoredCharacter();
        character.Sources ??= new List<StoredSource>();
        character.Retainers ??= new List<StoredRetainer>();
        characters[characterId] = character;
        return character;
    }

    private StoredCharacter? TryLoad(ulong characterId)
    {
        try
        {
            var info = new FileInfo(PathFor(characterId));
            if (!info.Exists)
            {
                return null;
            }

            var text = File.ReadAllText(info.FullName);
            return JsonConvert.DeserializeObject<StoredCharacter>(text);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"InventoryStore load failed for {characterId}");
            return null;
        }
    }

    private void Persist(ulong characterId, StoredCharacter character)
    {
        try
        {
            var text = JsonConvert.SerializeObject(character);
            File.WriteAllText(PathFor(characterId), text);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"InventoryStore write failed for {characterId}");
        }
    }

    private string PathFor(ulong characterId) =>
        Path.Combine(root.FullName, string.Concat(characterId.ToString("x16"), ".json"));
}
