using Aetherphone.Core;
using Aetherphone.Core.Social;
using Newtonsoft.Json;

namespace Aetherphone.Apps.Velvet;

internal sealed class StoredVelvetFilters
{
    public VelvetFilterPreferences DiscoverInclude { get; set; } = new();
    public VelvetFilterPreferences DiscoverExclude { get; set; } = new();
    public VelvetFilterPreferences FeedInclude { get; set; } = new();
    public VelvetFilterPreferences FeedExclude { get; set; } = new();
    public VelvetFilterPreferences Mutes { get; set; } = new();

    public StoredVelvetFilters Clone() => new()
    {
        DiscoverInclude = DiscoverInclude.Clone(),
        DiscoverExclude = DiscoverExclude.Clone(),
        FeedInclude = FeedInclude.Clone(),
        FeedExclude = FeedExclude.Clone(),
        Mutes = Mutes.Clone(),
    };
}

internal sealed class VelvetFilterArchive
{
    private readonly object sync = new();
    private readonly DirectoryInfo baseDir;

    public VelvetFilterArchive(DirectoryInfo baseDir)
    {
        this.baseDir = baseDir;
        if (!baseDir.Exists)
        {
            baseDir.Create();
        }
    }

    public StoredVelvetFilters Load(string accountId)
    {
        if (accountId.Length == 0)
        {
            return new StoredVelvetFilters();
        }

        try
        {
            var path = PathFor(accountId);
            if (!File.Exists(path))
            {
                return new StoredVelvetFilters();
            }

            var stored = JsonConvert.DeserializeObject<StoredVelvetFilters>(File.ReadAllText(path));
            return new StoredVelvetFilters
            {
                DiscoverInclude = stored?.DiscoverInclude ?? new VelvetFilterPreferences(),
                DiscoverExclude = stored?.DiscoverExclude ?? new VelvetFilterPreferences(),
                FeedInclude = stored?.FeedInclude ?? new VelvetFilterPreferences(),
                FeedExclude = stored?.FeedExclude ?? new VelvetFilterPreferences(),
                Mutes = stored?.Mutes ?? new VelvetFilterPreferences(),
            };
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"VelvetFilterArchive load failed for {accountId}");
            return new StoredVelvetFilters();
        }
    }

    public bool Save(string accountId, StoredVelvetFilters filters)
    {
        if (accountId.Length == 0)
        {
            return false;
        }

        try
        {
            lock (sync)
            {
                var path = PathFor(accountId);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(filters));
                File.Move(temp, path, true);
            }

            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"VelvetFilterArchive write failed for {accountId}");
            return false;
        }
    }

    private string PathFor(string accountId) => HashedFileName.For(baseDir, accountId.ToLowerInvariant());
}
