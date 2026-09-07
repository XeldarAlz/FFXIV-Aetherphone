using System.Linq;
using Aetherphone.Core;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Apps.Velvet;

internal sealed class StoredNotInterestedList
{
    [JsonProperty("ids")] public List<string> UserIds { get; set; } = new();

    [JsonProperty("passes")] public Dictionary<string, long> Passes { get; set; } = new();
}

internal readonly record struct NotInterestedSnapshot(string[] UserIds, Dictionary<string, long> Passes)
{
    public static readonly NotInterestedSnapshot Empty =
        new(Array.Empty<string>(), new Dictionary<string, long>(StringComparer.Ordinal));
}

internal sealed class VelvetNotInterestedArchive
{
    private readonly object sync = new();
    private readonly DirectoryInfo baseDir;

    public VelvetNotInterestedArchive(DirectoryInfo baseDir)
    {
        this.baseDir = baseDir;
        if (!baseDir.Exists)
        {
            baseDir.Create();
        }
    }

    public NotInterestedSnapshot Load(string accountId)
    {
        if (accountId.Length == 0)
        {
            return NotInterestedSnapshot.Empty;
        }

        try
        {
            var path = PathFor(accountId);
            if (!File.Exists(path))
            {
                return NotInterestedSnapshot.Empty;
            }

            var stored = JsonConvert.DeserializeObject<StoredNotInterestedList>(File.ReadAllText(path));
            if (stored is null)
            {
                return NotInterestedSnapshot.Empty;
            }

            var ids = stored.UserIds is { Count: > 0 } list ? list.ToArray() : Array.Empty<string>();
            return new NotInterestedSnapshot(ids, new Dictionary<string, long>(stored.Passes, StringComparer.Ordinal));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"VelvetNotInterestedArchive load failed for {accountId}");
            return NotInterestedSnapshot.Empty;
        }
    }

    public void Save(string accountId, IReadOnlyCollection<string> userIds, IReadOnlyDictionary<string, long> passes)
    {
        if (accountId.Length == 0)
        {
            return;
        }

        try
        {
            lock (sync)
            {
                var path = PathFor(accountId);
                var temp = path + ".tmp";
                var stored = new StoredNotInterestedList
                {
                    UserIds = userIds.ToList(),
                    Passes = new Dictionary<string, long>(passes, StringComparer.Ordinal),
                };
                File.WriteAllText(temp, JsonConvert.SerializeObject(stored));
                File.Move(temp, path, true);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"VelvetNotInterestedArchive write failed for {accountId}");
        }
    }

    private string PathFor(string accountId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accountId.ToLowerInvariant()));
        var builder = new StringBuilder(hash.Length * 2 + 5);
        for (var index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2"));
        }

        builder.Append(".json");
        return Path.Combine(baseDir.FullName, builder.ToString());
    }
}
