using Newtonsoft.Json;

namespace Aetherphone.Core.Wallet;

internal sealed class WalletHistoryStore
{
    private readonly DirectoryInfo root;

    public WalletHistoryStore(DirectoryInfo root)
    {
        this.root = root;
    }

    public WalletHistory Load(ulong contentId)
    {
        var path = PathFor(contentId);
        if (contentId == 0 || !File.Exists(path))
        {
            return new WalletHistory();
        }

        try
        {
            return JsonConvert.DeserializeObject<WalletHistory>(File.ReadAllText(path)) ?? new WalletHistory();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"WalletHistoryStore load failed for {contentId:X16}");
            return new WalletHistory();
        }
    }

    public void Save(ulong contentId, WalletHistory history)
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
            File.WriteAllText(temp, JsonConvert.SerializeObject(history));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"WalletHistoryStore write failed for {contentId:X16}");
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
