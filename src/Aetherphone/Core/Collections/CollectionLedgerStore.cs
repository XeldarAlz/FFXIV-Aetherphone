using Newtonsoft.Json;

namespace Aetherphone.Core.Collections;

internal sealed class CollectionLedgerStore
{
    private readonly DirectoryInfo root;

    public CollectionLedgerStore(DirectoryInfo root)
    {
        this.root = root;
    }

    public CollectionLedger Load(ulong contentId)
    {
        var path = PathFor(contentId);
        if (!File.Exists(path))
        {
            return new CollectionLedger();
        }

        try
        {
            return JsonConvert.DeserializeObject<CollectionLedger>(File.ReadAllText(path)) ?? new CollectionLedger();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"CollectionLedgerStore load failed for {contentId:X16}");
            return new CollectionLedger();
        }
    }

    public void Save(ulong contentId, CollectionLedger ledger)
    {
        if (contentId == 0)
        {
            return;
        }

        try
        {
            if (!root.Exists)
            {
                root.Create();
            }

            var path = PathFor(contentId);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(ledger));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"CollectionLedgerStore write failed for {contentId:X16}");
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
