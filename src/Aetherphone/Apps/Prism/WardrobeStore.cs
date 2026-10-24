using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Inventory;
using Newtonsoft.Json;

namespace Aetherphone.Apps.Prism;

internal sealed record WardrobeOutfit(
    string Id,
    string Name,
    string State,
    Guid DesignId,
    GlamGearDto[] Gear,
    bool Favourite,
    long CreatedAtUnix,
    string? GlamKey = null,
    WardrobeCover? Cover = null);

internal sealed record WardrobeCover(string Path, float Zoom, float CenterX, float CenterY);

internal sealed class WardrobeStore
{
    private const string FolderName = "PrismWardrobe";
    private const int DesignsPerFrame = 2;

    private readonly DirectoryInfo root;
    private readonly object sync = new();
    private volatile WardrobeOutfit[] outfits = Array.Empty<WardrobeOutfit>();
    private readonly Queue<(Guid Id, string Name)> importQueue = new();
    private readonly List<WardrobeOutfit> imported = new();
    private ulong loadedFor;
    private volatile bool loading;

    public WardrobeStore(string configDirectory)
    {
        root = new DirectoryInfo(Path.Combine(configDirectory, FolderName));
    }

    public WardrobeOutfit[] Outfits => outfits;

    public bool Loading => loading;

    public bool Importing => importQueue.Count > 0;

    public void Ensure()
    {
        var contentId = InventoryReader.ReadLocalContentId();
        if (contentId == 0 || contentId == loadedFor)
        {
            return;
        }

        loadedFor = contentId;
        outfits = Array.Empty<WardrobeOutfit>();
        importQueue.Clear();
        loading = true;
        var path = PathFor(contentId);
        _ = Task.Run(() =>
        {
            WardrobeOutfit[]? read = null;
            try
            {
                if (File.Exists(path))
                {
                    read = JsonConvert.DeserializeObject<WardrobeOutfit[]>(File.ReadAllText(path));
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"Wardrobe load failed for {contentId:X16}");
            }

            if (loadedFor == contentId)
            {
                outfits = read ?? Array.Empty<WardrobeOutfit>();
                loading = false;
            }
        });
    }

    public WardrobeOutfit? Find(string id)
    {
        var current = outfits;
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].Id == id)
            {
                return current[index];
            }
        }

        return null;
    }

    public WardrobeOutfit? FindByGlam(string glamKey)
    {
        var current = outfits;
        for (var index = 0; index < current.Length; index++)
        {
            if (glamKey.Length > 0 && current[index].GlamKey == glamKey)
            {
                return current[index];
            }
        }

        return null;
    }

    public void Add(WardrobeOutfit outfit)
    {
        if (loading)
        {
            return;
        }

        var current = outfits;
        var grown = new WardrobeOutfit[current.Length + 1];
        grown[0] = outfit;
        Array.Copy(current, 0, grown, 1, current.Length);
        Commit(grown);
    }

    public void Update(WardrobeOutfit outfit)
    {
        if (loading)
        {
            return;
        }

        var current = outfits;
        var updated = (WardrobeOutfit[])current.Clone();
        for (var index = 0; index < updated.Length; index++)
        {
            if (updated[index].Id == outfit.Id)
            {
                updated[index] = outfit;
            }
        }

        Commit(updated);
    }

    public void Remove(string id)
    {
        if (loading)
        {
            return;
        }

        var current = outfits;
        var kept = new List<WardrobeOutfit>(current.Length);
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].Id != id)
            {
                kept.Add(current[index]);
            }
        }

        Commit(kept.ToArray());
    }

    public int StartImport(List<(Guid Id, string Name)> designs)
    {
        if (loading || Importing)
        {
            return -1;
        }

        var current = outfits;
        imported.Clear();
        for (var index = 0; index < designs.Count; index++)
        {
            if (!HasDesign(current, designs[index].Id))
            {
                importQueue.Enqueue(designs[index]);
            }
        }

        return importQueue.Count;
    }

    public bool HasDesign(Guid id) => HasDesign(outfits, id);

    public int PumpImport(GlamourService glamour)
    {
        if (importQueue.Count == 0)
        {
            return -1;
        }

        for (var step = 0; step < DesignsPerFrame && importQueue.TryDequeue(out var design); step++)
        {
            imported.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"), design.Name, string.Empty, design.Id,
                GlamourerDesign.Read(glamour.Design(design.Id), out _, out _), false,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        if (importQueue.Count > 0)
        {
            return -1;
        }

        var grown = new List<WardrobeOutfit>(outfits);
        grown.AddRange(imported);
        Commit(grown.ToArray());
        var added = imported.Count;
        imported.Clear();
        return added;
    }

    private static bool HasDesign(WardrobeOutfit[] current, Guid id)
    {
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].DesignId == id)
            {
                return true;
            }
        }

        return false;
    }

    private void Commit(WardrobeOutfit[] next)
    {
        outfits = next;
        var contentId = loadedFor;
        if (contentId == 0)
        {
            return;
        }

        _ = Task.Run(() => Write(contentId, next));
    }

    private void Write(ulong contentId, WardrobeOutfit[] snapshot)
    {
        lock (sync)
        {
            try
            {
                if (!root.Exists)
                {
                    root.Create();
                }

                var path = PathFor(contentId);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(loadedFor == contentId ? outfits : snapshot));
                File.Move(temp, path, true);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"Wardrobe write failed for {contentId:X16}");
            }
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
