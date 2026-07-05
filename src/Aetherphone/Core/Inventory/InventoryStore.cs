using Newtonsoft.Json;

namespace Aetherphone.Core.Inventory;

internal sealed class StoredStack
{
    [JsonProperty("i")] public uint ItemId { get; set; }
    [JsonProperty("q")] public int Quantity { get; set; }
    [JsonProperty("h")] public bool HighQuality { get; set; }
    [JsonProperty("s")] public int Slot { get; set; }
}

internal sealed class StoredSource
{
    [JsonProperty("kind")] public InventorySourceKind Kind { get; set; }
    [JsonProperty("ownerName")] public string OwnerName { get; set; } = string.Empty;
    [JsonProperty("ownerId")] public ulong OwnerId { get; set; }
    [JsonProperty("capturedUnix")] public long CapturedUnix { get; set; }
    [JsonProperty("stacks")] public StoredStack[] Stacks { get; set; } = Array.Empty<StoredStack>();
}

internal sealed class StoredCharacter
{
    [JsonProperty("sources")] public List<StoredSource> Sources { get; set; } = new();
}

internal sealed class InventoryStore
{
    private readonly object sync = new();
    private readonly DirectoryInfo root;
    private readonly Dictionary<ulong, StoredCharacter> characters = new();
    private readonly HashSet<ulong> loaded = new();

    public InventoryStore(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public void CaptureSource(ulong characterId, StoredSource source)
    {
        if (characterId == 0 || source.OwnerId == 0)
        {
            return;
        }

        lock (sync)
        {
            var character = EnsureLoaded(characterId);
            ReplaceOrAdd(character.Sources, source);
            Persist(characterId, character);
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

    private StoredCharacter EnsureLoaded(ulong characterId)
    {
        if (characters.TryGetValue(characterId, out var existing))
        {
            return existing;
        }

        var character = TryLoad(characterId) ?? new StoredCharacter();
        characters[characterId] = character;
        loaded.Add(characterId);
        return character;
    }

    private static void ReplaceOrAdd(List<StoredSource> sources, StoredSource source)
    {
        for (var index = 0; index < sources.Count; index++)
        {
            var existing = sources[index];
            if (existing.Kind == source.Kind && existing.OwnerId == source.OwnerId)
            {
                sources[index] = source;
                return;
            }
        }

        sources.Add(source);
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
            AepLog.Warning($"InventoryStore load failed for {characterId}: {exception.Message}");
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
            AepLog.Warning($"InventoryStore write failed for {characterId}: {exception.Message}");
        }
    }

    private string PathFor(ulong characterId) =>
        Path.Combine(root.FullName, string.Concat(characterId.ToString("x16"), ".json"));
}
