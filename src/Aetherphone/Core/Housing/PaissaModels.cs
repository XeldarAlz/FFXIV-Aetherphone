using System.Text.Json.Serialization;

namespace Aetherphone.Core.Housing;

internal sealed class PaissaWorldSummary
{
    [JsonPropertyName("id")] public uint Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("datacenter_id")] public int DataCenterId { get; set; }
    [JsonPropertyName("datacenter_name")] public string? DataCenterName { get; set; }
}

internal sealed class PaissaOpenPlot
{
    [JsonPropertyName("world_id")] public uint WorldId { get; set; }
    [JsonPropertyName("district_id")] public uint DistrictId { get; set; }
    [JsonPropertyName("ward_number")] public int WardNumber { get; set; }
    [JsonPropertyName("plot_number")] public int PlotNumber { get; set; }
    [JsonPropertyName("size")] public int Size { get; set; }
    [JsonPropertyName("price")] public long Price { get; set; }
    [JsonPropertyName("last_updated_time")] public double LastUpdatedTime { get; set; }
    [JsonPropertyName("first_seen_time")] public double FirstSeenTime { get; set; }
    [JsonPropertyName("purchase_system")] public int PurchaseSystem { get; set; }
    [JsonPropertyName("lotto_entries")] public int? LottoEntries { get; set; }
    [JsonPropertyName("lotto_phase")] public int? LottoPhase { get; set; }
    [JsonPropertyName("lotto_phase_until")] public long? LottoPhaseUntil { get; set; }
}

internal sealed class PaissaDistrictDetail
{
    [JsonPropertyName("id")] public uint Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("num_open_plots")] public int NumOpenPlots { get; set; }
    [JsonPropertyName("oldest_plot_time")] public double OldestPlotTime { get; set; }
    [JsonPropertyName("open_plots")] public PaissaOpenPlot[]? OpenPlots { get; set; }
    [JsonPropertyName("proxy")] public PaissaProxyInfo? Proxy { get; set; }
}

internal sealed class PaissaProxyInfo
{
    [JsonPropertyName("cache_age")] public int? CacheAgeSeconds { get; set; }
    [JsonPropertyName("ttl")] public int? TtlSeconds { get; set; }
    [JsonPropertyName("upstream")] public string? Upstream { get; set; }
}

internal sealed class PaissaWorldDetail
{
    [JsonPropertyName("id")] public uint Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("num_open_plots")] public int NumOpenPlots { get; set; }
    [JsonPropertyName("oldest_plot_time")] public double OldestPlotTime { get; set; }
    [JsonPropertyName("districts")] public PaissaDistrictDetail[]? Districts { get; set; }
    [JsonPropertyName("proxy")] public PaissaProxyInfo? Proxy { get; set; }
}

internal sealed class HousingCacheFile
{
    public const int CurrentSchema = 1;

    [JsonPropertyName("schema")] public int Schema { get; set; } = CurrentSchema;
    [JsonPropertyName("provider")] public string? Provider { get; set; }
    [JsonPropertyName("cachedAt")] public long CachedAtUnix { get; set; }
    [JsonPropertyName("worldId")] public uint WorldId { get; set; }
    [JsonPropertyName("worldName")] public string? WorldName { get; set; }
    [JsonPropertyName("districtId")] public uint DistrictId { get; set; }
    [JsonPropertyName("districtName")] public string? DistrictName { get; set; }
    [JsonPropertyName("plots")] public HousingCachePlot[]? Plots { get; set; }
}

internal sealed class HousingCachePlot
{
    [JsonPropertyName("ward")] public int Ward { get; set; }
    [JsonPropertyName("plot")] public int Plot { get; set; }
    [JsonPropertyName("size")] public byte Size { get; set; }
    [JsonPropertyName("price")] public long Price { get; set; }
    [JsonPropertyName("eligibility")] public byte Eligibility { get; set; }
    [JsonPropertyName("mode")] public byte Mode { get; set; }
    [JsonPropertyName("phase")] public byte Phase { get; set; }
    [JsonPropertyName("phaseEnd")] public long PhaseEndUnix { get; set; }
    [JsonPropertyName("entries")] public int? Entries { get; set; }
    [JsonPropertyName("lastSeen")] public long LastSeenUnix { get; set; }
    [JsonPropertyName("firstSeen")] public long FirstSeenUnix { get; set; }
}

internal sealed class HousingWorldCacheFile
{
    [JsonPropertyName("schema")] public int Schema { get; set; } = HousingCacheFile.CurrentSchema;
    [JsonPropertyName("cachedAt")] public long CachedAtUnix { get; set; }
    [JsonPropertyName("worlds")] public HousingWorldCacheEntry[]? Worlds { get; set; }
}

internal sealed class HousingWorldCacheEntry
{
    [JsonPropertyName("id")] public uint Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("dcId")] public int DataCenterId { get; set; }
    [JsonPropertyName("dcName")] public string? DataCenterName { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PaissaWorldSummary[]))]
[JsonSerializable(typeof(PaissaWorldDetail))]
[JsonSerializable(typeof(PaissaDistrictDetail))]
[JsonSerializable(typeof(PaissaProxyInfo))]
[JsonSerializable(typeof(HousingCacheFile))]
[JsonSerializable(typeof(HousingWorldCacheFile))]
internal sealed partial class HousingJsonContext : JsonSerializerContext
{
}
