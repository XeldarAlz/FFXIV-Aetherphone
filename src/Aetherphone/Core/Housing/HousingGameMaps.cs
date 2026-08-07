using System.Text;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Housing;

internal sealed class HousingGameMap
{
    private readonly Dictionary<int, Vector2> byPlot;

    public HousingGameMap(string texturePath, IReadOnlyList<HousingGamePlotPoint> plots, string mapId)
    {
        TexturePath = texturePath;
        Plots = plots;
        MapId = mapId;
        byPlot = new Dictionary<int, Vector2>(plots.Count);
        for (var index = 0; index < plots.Count; index++)
        {
            byPlot[plots[index].PlotNumber] = plots[index].NormalizedPosition;
        }
    }

    public string TexturePath { get; }
    public IReadOnlyList<HousingGamePlotPoint> Plots { get; }
    public string MapId { get; }
    public bool TryGetPoint(int plotNumber, out Vector2 normalized) => byPlot.TryGetValue(plotNumber, out normalized);
}

internal sealed class HousingGameDistrictMap
{
    public HousingGameDistrictMap(HousingGameMap main, HousingGameMap? subdivision)
    {
        Main = main;
        Subdivision = subdivision;
    }

    public HousingGameMap Main { get; }

    public HousingGameMap? Subdivision { get; }

    public bool HasSubdivision => Subdivision is not null;

    public HousingGameMap For(bool subdivision) => subdivision && Subdivision is not null ? Subdivision : Main;
}

internal readonly record struct HousingGamePlotPoint(int PlotNumber, Vector2 NormalizedPosition);

internal enum HousingGameMapFailure : byte
{
    None,
    TerritoryMissing,
    MapMissing,
    TextureMissing,
    NoMarkers,
    TooFewMarkers,
    ProjectionFailed,
    Error,
}

internal sealed class HousingGameMaps
{
    private const float MapPageSize = 2048f;
    private const float OutsideTolerance = 0.08f;
    private const float OutsideAllowance = 0.15f;

    private readonly IDataManager data;
    private readonly ITextureProvider textures;
    private readonly Dictionary<uint, Entry> cache = new();
    private readonly object sync = new();

    public HousingGameMaps(IDataManager data, ITextureProvider textures)
    {
        this.data = data;
        this.textures = textures;
    }

    private readonly record struct Entry(HousingGameDistrictMap? Map, HousingGameMapFailure Failure, string Detail);

    private readonly record struct Division(HousingGameMap? Map, HousingGameMapFailure Failure, string Detail);

    public HousingGameDistrictMap? For(uint districtId) => Resolve(districtId).Map;

    public HousingGameMapFailure FailureFor(uint districtId) => Resolve(districtId).Failure;

    public string DetailFor(uint districtId) => Resolve(districtId).Detail;

    public void Clear()
    {
        lock (sync)
        {
            cache.Clear();
        }
    }

    public IDalamudTextureWrap? Texture(HousingGameMap map)
    {
        try
        {
            return textures.GetFromGame(map.TexturePath).GetWrapOrDefault();
        }
        catch (Exception exception)
        {
            AepLog.Debug($"Housing map texture '{map.TexturePath}' failed to load: {exception.Message}");
            return null;
        }
    }

    private Entry Resolve(uint districtId)
    {
        lock (sync)
        {
            if (cache.TryGetValue(districtId, out var cached))
            {
                return cached;
            }

            Entry entry;
            try
            {
                entry = Build(districtId);
            }
            catch (Exception exception)
            {
                entry = new Entry(null, HousingGameMapFailure.Error, exception.Message);
            }

            cache[districtId] = entry;
            if (entry.Map is null)
            {
                AepLog.Warning($"Housing could not read the in-game map for district {districtId}: " +
                               $"{entry.Failure} ({entry.Detail}).");
            }
            else
            {
                AepLog.Info($"Housing loaded the in-game map '{entry.Map.Main.MapId}' for district {districtId} " +
                            $"with {entry.Map.Main.Plots.Count} main plots and " +
                            $"{entry.Map.Subdivision?.Plots.Count ?? 0} subdivision plots.");
            }

            return entry;
        }
    }

    private Entry Build(uint districtId)
    {
        if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(districtId) is not { } territory)
        {
            return new Entry(null, HousingGameMapFailure.TerritoryMissing, $"no TerritoryType row {districtId}");
        }

        if (territory.Map.ValueNullable is not { } mainMap)
        {
            return new Entry(null, HousingGameMapFailure.MapMissing, $"TerritoryType {districtId} has no map row");
        }

        var groups = CollectMarkerGroups(districtId);
        if (groups.Count == 0)
        {
            return new Entry(null, HousingGameMapFailure.NoMarkers,
                $"HousingMapMarkerInfo row {districtId} has no markers");
        }

        if (!groups.TryGetValue(mainMap.RowId, out var mainMarkers))
        {
            return new Entry(null, HousingGameMapFailure.NoMarkers,
                $"no markers reference the district map {mainMap.RowId}");
        }

        var main = BuildDivision(mainMap, mainMarkers, 1);
        if (main.Map is null)
        {
            return new Entry(null, main.Failure, main.Detail);
        }

        HousingGameMap? subdivision = null;
        if (TryFindSubdivision(groups, mainMap.RowId) is { } subEntry &&
            data.GetExcelSheet<Map>().GetRowOrDefault(subEntry.Key) is { } subMap)
        {
            var built = BuildDivision(subMap, subEntry.Value, HousingDistricts.PlotsPerDivision + 1);
            subdivision = built.Map;
        }

        return new Entry(new HousingGameDistrictMap(main.Map, subdivision), HousingGameMapFailure.None, string.Empty);
    }

    private static KeyValuePair<uint, List<Vector3>>? TryFindSubdivision(
        Dictionary<uint, List<Vector3>> groups, uint mainMapRowId)
    {
        KeyValuePair<uint, List<Vector3>>? best = null;
        foreach (var group in groups)
        {
            if (group.Key == mainMapRowId || group.Key == 0 ||
                group.Value.Count < HousingDistricts.PlotsPerDivision)
            {
                continue;
            }

            if (best is null || group.Value.Count > best.Value.Value.Count)
            {
                best = group;
            }
        }

        return best;
    }

    private Division BuildDivision(Map map, List<Vector3> markers, int firstPlotNumber)
    {
        var mapId = map.Id.ExtractText();
        var texturePath = ResolveTexturePath(mapId);
        if (texturePath is null)
        {
            return new Division(null, HousingGameMapFailure.TextureMissing, $"no texture for map '{mapId}'");
        }

        if (markers.Count < HousingDistricts.PlotsPerDivision)
        {
            return new Division(null, HousingGameMapFailure.TooFewMarkers,
                $"map '{mapId}' has {markers.Count} of {HousingDistricts.PlotsPerDivision} plot markers");
        }

        var plots = Project(map, markers, firstPlotNumber);
        return plots is null
            ? new Division(null, HousingGameMapFailure.ProjectionFailed, $"markers fell outside map '{mapId}'")
            : new Division(new HousingGameMap(texturePath, plots, mapId), HousingGameMapFailure.None, string.Empty);
    }

    private Dictionary<uint, List<Vector3>> CollectMarkerGroups(uint districtId)
    {
        var groups = new Dictionary<uint, List<Vector3>>();
        if (data.GetSubrowExcelSheet<HousingMapMarkerInfo>().GetRowOrDefault(districtId) is not { } row)
        {
            return groups;
        }

        foreach (var marker in row)
        {
            var mapRowId = marker.Map.RowId;
            if (!groups.TryGetValue(mapRowId, out var list))
            {
                list = new List<Vector3>(HousingDistricts.PlotsPerDivision + 2);
                groups[mapRowId] = list;
            }

            list.Add(new Vector3(marker.X, marker.Y, marker.Z));
        }

        return groups;
    }

    private static IReadOnlyList<HousingGamePlotPoint>? Project(Map map, List<Vector3> markers, int firstPlotNumber)
    {
        var scaleFactor = map.SizeFactor <= 0 ? 1f : map.SizeFactor / 100f;
        var count = Math.Min(markers.Count, HousingDistricts.PlotsPerDivision);
        List<HousingGamePlotPoint>? best = null;
        var bestSpread = 0f;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidate = new List<HousingGamePlotPoint>(count);
            var minX = 1f;
            var maxX = 0f;
            var minY = 1f;
            var maxY = 0f;
            var outside = 0;
            for (var index = 0; index < count; index++)
            {
                var world = markers[index];
                var vertical = attempt == 0 ? world.Z : world.Y;
                var x = Normalize(world.X, map.OffsetX, scaleFactor);
                var y = Normalize(vertical, map.OffsetY, scaleFactor);
                if (x < -OutsideTolerance || x > 1f + OutsideTolerance ||
                    y < -OutsideTolerance || y > 1f + OutsideTolerance)
                {
                    outside++;
                }

                x = Math.Clamp(x, 0f, 1f);
                y = Math.Clamp(y, 0f, 1f);
                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y);
                maxY = MathF.Max(maxY, y);
                candidate.Add(new HousingGamePlotPoint(firstPlotNumber + index, new Vector2(x, y)));
            }

            if (outside > count * OutsideAllowance)
            {
                continue;
            }

            var spread = (maxX - minX) * (maxY - minY);
            if (spread <= bestSpread)
            {
                continue;
            }

            bestSpread = spread;
            best = candidate;
        }

        return bestSpread < 0.004f ? null : best;
    }

    private static float Normalize(float world, short offset, float scaleFactor) =>
        ((world + offset) * scaleFactor + MapPageSize * 0.5f) / MapPageSize;

    private string? ResolveTexturePath(string mapId)
    {
        if (string.IsNullOrEmpty(mapId))
        {
            return null;
        }

        var candidates = TextureCandidates(mapId);
        for (var index = 0; index < candidates.Length; index++)
        {
            if (FileExists(candidates[index]))
            {
                return candidates[index];
            }
        }

        return null;
    }

    private static string[] TextureCandidates(string mapId)
    {
        var flat = mapId.Replace("/", string.Empty);
        var underscored = mapId.Replace('/', '_');
        return
        [
            $"ui/map/{mapId}/{flat}_m.tex",
            $"ui/map/{mapId}/{flat}m_m.tex",
            $"ui/map/{mapId}/{flat}_s.tex",
            $"ui/map/{mapId}/{underscored}_m.tex",
            $"ui/map/{mapId}/{underscored}m_m.tex",
        ];
    }

    private bool FileExists(string path)
    {
        try
        {
            return data.FileExists(path);
        }
        catch (Exception exception)
        {
            AepLog.Debug($"Housing could not test '{path}': {exception.Message}");
            return false;
        }
    }

    public string Describe(uint districtId)
    {
        var report = new StringBuilder();
        var district = HousingDistricts.Resolve(districtId);
        report.Append("Aetherphone Housing map diagnostics\n");
        report.Append($"district: {district.Name} (territory {districtId})\n");
        try
        {
            var territory = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(districtId);
            report.Append($"territoryRow: {(territory is null ? "MISSING" : "found")}\n");
            if (territory is { } row)
            {
                report.Append($"territory.Map.RowId: {row.Map.RowId}\n");
                var groups = CollectMarkerGroups(districtId);
                report.Append($"markerGroups: {groups.Count}\n");
                foreach (var group in groups)
                {
                    DescribeGroup(report, group.Key, group.Value, group.Key == row.Map.RowId);
                }
            }
        }
        catch (Exception exception)
        {
            report.Append($"exception: {exception}\n");
        }

        var entry = Resolve(districtId);
        report.Append($"result: {(entry.Map is null ? "fallback" : "in-game map")}\n");
        if (entry.Map is { } resolved)
        {
            report.Append($"main: '{resolved.Main.MapId}' {resolved.Main.Plots.Count} plots " +
                          $"({resolved.Main.TexturePath})\n");
            report.Append(resolved.Subdivision is { } sub
                ? $"subdivision: '{sub.MapId}' {sub.Plots.Count} plots ({sub.TexturePath})\n"
                : "subdivision: none\n");
        }

        report.Append($"failure: {entry.Failure}\n");
        report.Append($"detail: {entry.Detail}\n");
        return report.ToString();
    }

    private void DescribeGroup(StringBuilder report, uint mapRowId, List<Vector3> markers, bool isMain)
    {
        var mapRow = data.GetExcelSheet<Map>().GetRowOrDefault(mapRowId);
        var mapId = mapRow is { } value ? value.Id.ExtractText() : "?";
        var tag = isMain ? " <= district map" : string.Empty;
        report.Append($"group map {mapRowId} '{mapId}': {markers.Count} markers{tag}\n");
        if (mapRow is not { } map)
        {
            return;
        }

        report.Append($"  sizeFactor {map.SizeFactor} offset {map.OffsetX},{map.OffsetY}\n");
        var candidates = TextureCandidates(mapId);
        for (var index = 0; index < candidates.Length; index++)
        {
            report.Append($"  texture: {candidates[index]} -> {(FileExists(candidates[index]) ? "OK" : "missing")}\n");
        }

        var sampleCount = Math.Min(3, markers.Count);
        for (var index = 0; index < sampleCount; index++)
        {
            var world = markers[index];
            report.Append($"  marker[{index}] {world.X:F1},{world.Y:F1},{world.Z:F1}\n");
        }

        var scaleFactor = map.SizeFactor <= 0 ? 1f : map.SizeFactor / 100f;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var limit = Math.Min(markers.Count, HousingDistricts.PlotsPerDivision);
            for (var index = 0; index < limit; index++)
            {
                var world = markers[index];
                var vertical = attempt == 0 ? world.Z : world.Y;
                var x = Normalize(world.X, map.OffsetX, scaleFactor);
                var y = Normalize(vertical, map.OffsetY, scaleFactor);
                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y);
                maxY = MathF.Max(maxY, y);
            }

            report.Append($"  normalised {(attempt == 0 ? "X/Z" : "X/Y")}: " +
                          $"x {minX:F3}..{maxX:F3} y {minY:F3}..{maxY:F3}\n");
        }
    }
}
