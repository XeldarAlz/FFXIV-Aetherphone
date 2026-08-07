using System.Text.Json;
using System.Globalization;

namespace Aetherphone.Core.Housing;

internal sealed class HousingCache
{
    private static readonly TimeSpan WorldListMaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan DistrictMaxAge = TimeSpan.FromDays(7);
    private readonly DirectoryInfo root;
    private readonly object sync = new();

    public HousingCache(DirectoryInfo root)
    {
        this.root = root;
    }

    public string DisplayName => "Saved housing data";

    public IReadOnlyList<HousingWorld>? ReadWorlds()
    {
        var payload = ReadFile("worlds.json", HousingJsonContext.Default.HousingWorldCacheFile, WorldListMaxAge);
        var entries = payload?.Worlds;
        if (payload is null || entries is null || payload.Schema != HousingCacheFile.CurrentSchema)
        {
            return null;
        }

        var worlds = new List<HousingWorld>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.Id == 0 || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var dataCenter = entry.DataCenterName ?? string.Empty;
            worlds.Add(new HousingWorld(entry.Id, entry.Name, entry.DataCenterId, dataCenter,
                HousingRegions.For(dataCenter)));
        }

        return worlds.Count == 0 ? null : worlds;
    }

    public void WriteWorlds(IReadOnlyList<HousingWorld> worlds)
    {
        var entries = new HousingWorldCacheEntry[worlds.Count];
        for (var index = 0; index < worlds.Count; index++)
        {
            var world = worlds[index];
            entries[index] = new HousingWorldCacheEntry
            {
                Id = world.Id,
                Name = world.Name,
                DataCenterId = world.DataCenterId,
                DataCenterName = world.DataCenterName,
            };
        }

        WriteFile("worlds.json", new HousingWorldCacheFile
        {
            CachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Worlds = entries,
        }, HousingJsonContext.Default.HousingWorldCacheFile);
    }

    public HousingDistrictSnapshot? Read(uint worldId, uint districtId)
    {
        var payload = ReadFile(FileName(worldId, districtId), HousingJsonContext.Default.HousingCacheFile,
            DistrictMaxAge);
        if (payload is null || payload.Schema != HousingCacheFile.CurrentSchema || payload.WorldId != worldId ||
            payload.DistrictId != districtId)
        {
            return null;
        }

        var stored = payload.Plots ?? Array.Empty<HousingCachePlot>();
        var plots = new List<HousingPlot>(stored.Length);
        for (var index = 0; index < stored.Length; index++)
        {
            var entry = stored[index];
            if (entry.Ward <= 0 || entry.Plot <= 0)
            {
                continue;
            }

            plots.Add(new HousingPlot
            {
                Key = new HousingPlotKey(worldId, districtId, entry.Ward, entry.Plot),
                Size = (HousingPlotSize)entry.Size,
                Price = entry.Price,
                Eligibility = (HousingPurchaseEligibility)entry.Eligibility,
                Mode = (HousingPurchaseMode)entry.Mode,
                Phase = (HousingLotteryPhase)entry.Phase,
                PhaseEndsUtc = FromUnix(entry.PhaseEndUnix),
                Entries = entry.Entries,
                LastSeenUtc = FromUnix(entry.LastSeenUnix) ?? default,
                FirstSeenUtc = FromUnix(entry.FirstSeenUnix) ?? default,
            });
        }

        plots.Sort(HousingPlotOrder.ByWardThenPlot);
        return new HousingDistrictSnapshot
        {
            WorldId = worldId,
            DistrictId = districtId,
            WorldName = payload.WorldName ?? string.Empty,
            DistrictName = string.IsNullOrEmpty(payload.DistrictName)
                ? HousingDistricts.Name(districtId)
                : payload.DistrictName,
            FetchedUtc = FromUnix(payload.CachedAtUnix) ?? DateTime.UtcNow,
            Source = HousingProviderKind.Cache,
            Plots = plots,
        };
    }

    public void Write(HousingDistrictSnapshot snapshot)
    {
        if (snapshot.Source == HousingProviderKind.Cache)
        {
            return;
        }

        var plots = new HousingCachePlot[snapshot.Plots.Count];
        for (var index = 0; index < snapshot.Plots.Count; index++)
        {
            var plot = snapshot.Plots[index];
            plots[index] = new HousingCachePlot
            {
                Ward = plot.Key.Ward,
                Plot = plot.Key.Plot,
                Size = (byte)plot.Size,
                Price = plot.Price,
                Eligibility = (byte)plot.Eligibility,
                Mode = (byte)plot.Mode,
                Phase = (byte)plot.Phase,
                PhaseEndUnix = ToUnix(plot.PhaseEndsUtc),
                Entries = plot.Entries,
                LastSeenUnix = ToUnix(plot.LastSeenUtc),
                FirstSeenUnix = ToUnix(plot.FirstSeenUtc),
            };
        }

        WriteFile(FileName(snapshot.WorldId, snapshot.DistrictId), new HousingCacheFile
        {
            Provider = snapshot.Source.ToString(),
            CachedAtUnix = ToUnix(snapshot.FetchedUtc),
            WorldId = snapshot.WorldId,
            WorldName = snapshot.WorldName,
            DistrictId = snapshot.DistrictId,
            DistrictName = snapshot.DistrictName,
            Plots = plots,
        }, HousingJsonContext.Default.HousingCacheFile);
    }

    public void Clear()
    {
        lock (sync)
        {
            try
            {
                if (!root.Exists)
                {
                    return;
                }

                var files = root.GetFiles("*.json");
                for (var index = 0; index < files.Length; index++)
                {
                    files[index].Delete();
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Housing cache clear failed: {exception.Message}");
            }
        }
    }

    private static string FileName(uint worldId, uint districtId) =>
        string.Concat("w", worldId.ToString(CultureInfo.InvariantCulture), "-d",
            districtId.ToString(CultureInfo.InvariantCulture), ".json");

    private T? ReadFile<T>(string name, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        TimeSpan maxAge)
        where T : class
    {
        try
        {
            var path = Path.Combine(root.FullName, name);
            var info = new FileInfo(path);
            if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc > maxAge)
            {
                return null;
            }

            lock (sync)
            {
                using var stream = File.OpenRead(path);
                return JsonSerializer.Deserialize(stream, typeInfo);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Housing cache read failed for {name}: {exception.Message}");
            return null;
        }
    }

    private void WriteFile<T>(string name, T payload,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        try
        {
            lock (sync)
            {
                if (!root.Exists)
                {
                    root.Create();
                }

                var path = Path.Combine(root.FullName, name);
                File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(payload, typeInfo));
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Housing cache write failed for {name}: {exception.Message}");
        }
    }

    private static DateTime? FromUnix(long unixSeconds) =>
        unixSeconds <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    private static long ToUnix(DateTime? moment) =>
        moment is null || moment.Value == default
            ? 0L
            : new DateTimeOffset(DateTime.SpecifyKind(moment.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
