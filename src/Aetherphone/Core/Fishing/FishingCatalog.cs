using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Fishing;

internal enum FishingCatalogState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal sealed class FishEntry
{
    public required TimedFish Fish { get; init; }
    public required string Name { get; init; }
    public required string SearchKey { get; init; }
    public required uint IconId { get; init; }
    public required string SpotName { get; init; }
    public required string ZoneName { get; init; }
    public required uint TerritoryId { get; init; }
    public required uint MapId { get; init; }
    public required Vector2 MapPosition { get; init; }
    public required WeatherOdds[] Odds { get; init; }
    public required uint LogRow { get; init; }
    public uint ItemId => Fish.ItemId;
}

internal sealed class FishingCatalog
{
    public const string AppId = "fishing";
    private const string DataFolder = "Fishing";
    private const string DataFile = "TimedFish.txt";
    private const float MapSpan = 41f;
    private const float MapTextureSize = 2048f;
    private const float MapSizeDivisor = 100f;
    private const long NoWindowRetrySeconds = 86400;

    private readonly IDataManager data;
    private readonly string dataPath;
    private readonly Dictionary<uint, string> itemNames = new();
    private readonly Dictionary<uint, uint> itemIcons = new();
    private readonly Dictionary<byte, string> weatherNames = new();
    private readonly Dictionary<byte, uint> weatherIcons = new();
    private readonly Dictionary<uint, string> oceanSpotNames = new();
    private readonly object gate = new();
    private Dictionary<uint, uint>? logRowsByItem;
    private FishEntry[] entries = Array.Empty<FishEntry>();
    private FishWindow[] windows = Array.Empty<FishWindow>();
    private long[] retryAt = Array.Empty<long>();
    private Dictionary<uint, int> indexByItem = new();
    private volatile FishingCatalogState state;

    public FishingCatalog(IDataManager data, string pluginDirectory)
    {
        this.data = data;
        dataPath = Path.Combine(pluginDirectory, DataFolder, DataFile);
    }

    public FishingCatalogState State => state;

    public FishEntry[] Entries => entries;

    public void EnsureLoaded()
    {
        if (state != FishingCatalogState.Idle)
        {
            return;
        }

        state = FishingCatalogState.Loading;
        _ = Task.Run(Load);
    }

    public void Retry()
    {
        if (state != FishingCatalogState.Failed)
        {
            return;
        }

        state = FishingCatalogState.Idle;
        EnsureLoaded();
    }

    public bool TryIndex(uint itemId, out int index)
    {
        if (state == FishingCatalogState.Ready && indexByItem.TryGetValue(itemId, out index))
        {
            return true;
        }

        index = -1;
        return false;
    }

    public FishWindow WindowAt(int index) =>
        state == FishingCatalogState.Ready && index >= 0 && index < windows.Length ? windows[index] : FishWindow.None;

    public void RefreshWindows(long nowUnix, int budget)
    {
        if (state != FishingCatalogState.Ready)
        {
            return;
        }

        var spent = 0;
        for (var index = 0; index < windows.Length && spent < budget; index++)
        {
            var window = windows[index];
            var stale = window.Exists ? nowUnix >= window.EndUnix : nowUnix >= retryAt[index];
            if (!stale)
            {
                continue;
            }

            Compute(entries[index], nowUnix, out windows[index], out retryAt[index]);
            spent++;
        }
    }

    private static void Compute(FishEntry entry, long nowUnix, out FishWindow window, out long retry)
    {
        var rule = entry.Fish.Rule;
        if (rule.AlwaysOpen)
        {
            window = new FishWindow(0, long.MaxValue);
            retry = long.MaxValue;
            return;
        }

        window = FishWindowMath.Next(rule, entry.Odds, nowUnix);
        retry = window.Exists ? 0 : nowUnix + NoWindowRetrySeconds;
    }

    public string ItemName(uint itemId)
    {
        lock (gate)
        {
            if (itemNames.TryGetValue(itemId, out var cached))
            {
                return cached;
            }
        }

        var name = string.Empty;
        var icon = 0u;
        if (data.GetExcelSheet<Item>().TryGetRow(itemId, out var item))
        {
            name = item.Name.ExtractText();
            icon = item.Icon;
        }

        lock (gate)
        {
            itemNames[itemId] = name;
            itemIcons[itemId] = icon;
        }

        return name;
    }

    public uint ItemIcon(uint itemId)
    {
        lock (gate)
        {
            if (itemIcons.TryGetValue(itemId, out var cached))
            {
                return cached;
            }
        }

        ItemName(itemId);
        lock (gate)
        {
            return itemIcons.TryGetValue(itemId, out var icon) ? icon : 0u;
        }
    }

    public string WeatherName(byte weatherId)
    {
        lock (gate)
        {
            if (weatherNames.TryGetValue(weatherId, out var cached))
            {
                return cached;
            }
        }

        var name = string.Empty;
        var icon = 0u;
        if (data.GetExcelSheet<Weather>().TryGetRow(weatherId, out var weather))
        {
            name = weather.Name.ExtractText();
            icon = (uint)Math.Max(0, weather.Icon);
        }

        lock (gate)
        {
            weatherNames[weatherId] = name;
            weatherIcons[weatherId] = icon;
        }

        return name;
    }

    public uint WeatherIcon(byte weatherId)
    {
        lock (gate)
        {
            if (weatherIcons.TryGetValue(weatherId, out var cached))
            {
                return cached;
            }
        }

        WeatherName(weatherId);
        lock (gate)
        {
            return weatherIcons.TryGetValue(weatherId, out var icon) ? icon : 0u;
        }
    }

    public string OceanSpotName(uint spotId)
    {
        if (oceanSpotNames.TryGetValue(spotId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (data.GetExcelSheet<IKDSpot>().TryGetRow(spotId, out var spot))
        {
            name = spot.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }

        oceanSpotNames[spotId] = name;
        return name;
    }

    public uint LogRow(uint itemId) => LogRows().TryGetValue(itemId, out var row) ? row : 0u;

    private void Load()
    {
        try
        {
            var text = File.ReadAllText(dataPath);
            var parsed = TimedFishParser.Parse(text);
            var logRows = LogRows();
            var built = new List<FishEntry>(parsed.Count);
            var odds = new Dictionary<uint, WeatherOdds[]>();
            for (var index = 0; index < parsed.Count; index++)
            {
                if (TryBuild(parsed[index], logRows, odds, out var entry))
                {
                    built.Add(entry);
                }
            }

            var lookup = new Dictionary<uint, int>(built.Count);
            for (var index = 0; index < built.Count; index++)
            {
                lookup[built[index].ItemId] = index;
            }

            var builtEntries = built.ToArray();
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var builtWindows = new FishWindow[builtEntries.Length];
            var builtRetry = new long[builtEntries.Length];
            for (var index = 0; index < builtEntries.Length; index++)
            {
                Compute(builtEntries[index], nowUnix, out builtWindows[index], out builtRetry[index]);
            }

            entries = builtEntries;
            windows = builtWindows;
            retryAt = builtRetry;
            indexByItem = lookup;
            state = FishingCatalogState.Ready;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Fishing] timed fish data failed to load");
            state = FishingCatalogState.Failed;
        }
    }

    private Dictionary<uint, uint> LogRows()
    {
        lock (gate)
        {
            if (logRowsByItem is not null)
            {
                return logRowsByItem;
            }
        }

        var rows = new Dictionary<uint, uint>();
        foreach (var parameter in data.GetExcelSheet<FishParameter>())
        {
            var itemId = parameter.Item.RowId;
            if (itemId != 0 && parameter.IsInLog)
            {
                rows.TryAdd(itemId, parameter.RowId);
            }
        }

        lock (gate)
        {
            logRowsByItem ??= rows;
            return logRowsByItem;
        }
    }

    private bool TryBuild(TimedFish fish, Dictionary<uint, uint> logRows, Dictionary<uint, WeatherOdds[]> oddsCache,
        out FishEntry entry)
    {
        entry = null!;
        if (!data.GetExcelSheet<Item>().TryGetRow(fish.ItemId, out var item) ||
            !data.GetExcelSheet<FishingSpot>().TryGetRow(fish.SpotId, out var spot))
        {
            return false;
        }

        var name = item.Name.ExtractText();
        if (name.Length == 0)
        {
            return false;
        }

        var territoryId = spot.TerritoryType.RowId;
        var spotName = spot.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var zoneName = string.Empty;
        var mapId = 0u;
        var position = Vector2.Zero;
        if (spot.TerritoryType.ValueNullable is { } territory)
        {
            zoneName = territory.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (territory.Map.ValueNullable is { } map)
            {
                mapId = map.RowId;
                var sizeFactor = Math.Max(1f, map.SizeFactor / MapSizeDivisor);
                position = new Vector2(MapCoordinate(spot.X, sizeFactor), MapCoordinate(spot.Z, sizeFactor));
            }
        }

        if (!oddsCache.TryGetValue(territoryId, out var odds))
        {
            odds = BuildOdds(territoryId);
            oddsCache[territoryId] = odds;
        }

        lock (gate)
        {
            itemNames[fish.ItemId] = name;
            itemIcons[fish.ItemId] = item.Icon;
        }

        entry = new FishEntry
        {
            Fish = fish,
            Name = name,
            SearchKey = string.Concat(name, "\n", spotName, "\n", zoneName).ToLowerInvariant(),
            IconId = item.Icon,
            SpotName = spotName,
            ZoneName = zoneName,
            TerritoryId = territoryId,
            MapId = mapId,
            MapPosition = position,
            Odds = odds,
            LogRow = logRows.TryGetValue(fish.ItemId, out var row) ? row : 0u,
        };
        return true;
    }

    private WeatherOdds[] BuildOdds(uint territoryId)
    {
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory) ||
            !data.GetExcelSheet<WeatherRate>().TryGetRow(territory.WeatherRate.RowId, out var rate))
        {
            return Array.Empty<WeatherOdds>();
        }

        var built = new List<WeatherOdds>(rate.Rate.Count);
        var cumulative = 0;
        for (var index = 0; index < rate.Rate.Count; index++)
        {
            var weatherId = (byte)rate.Weather[index].RowId;
            var chance = rate.Rate[index];
            if (weatherId == 0 || chance <= 0)
            {
                continue;
            }

            cumulative += chance;
            built.Add(new WeatherOdds(weatherId, cumulative));
        }

        return built.ToArray();
    }

    private static float MapCoordinate(short raw, float sizeFactor) =>
        MapSpan / sizeFactor * (raw / MapTextureSize) + 1f;
}
