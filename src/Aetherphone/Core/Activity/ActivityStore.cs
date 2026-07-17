using Newtonsoft.Json;

namespace Aetherphone.Core.Activity;

internal sealed class ActivityStore
{
    private readonly DirectoryInfo root;

    public ActivityStore(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public ActivityLedger Load(ulong contentId)
    {
        var path = PathFor(contentId);
        if (!File.Exists(path))
        {
            return new ActivityLedger();
        }

        try
        {
            return JsonConvert.DeserializeObject<ActivityLedger>(File.ReadAllText(path)) ?? new ActivityLedger();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"ActivityStore load failed for {contentId:X16}: {exception.Message}");
            return new ActivityLedger();
        }
    }

    public void Save(ulong contentId, ActivityLedger ledger)
    {
        if (contentId == 0)
        {
            return;
        }

        try
        {
            var path = PathFor(contentId);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(ledger));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"ActivityStore write failed for {contentId:X16}: {exception.Message}");
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
