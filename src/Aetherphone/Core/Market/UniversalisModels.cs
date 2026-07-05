using System.Text.Json.Serialization;

namespace Aetherphone.Core.Market;

internal sealed class UniversalisCurrentData
{
    [JsonPropertyName("itemID")] public uint ItemId { get; set; }
    [JsonPropertyName("lastUploadTime")] public long LastUploadTime { get; set; }
    [JsonPropertyName("listings")] public UniversalisListing[]? Listings { get; set; }
    [JsonPropertyName("recentHistory")] public UniversalisSale[]? RecentHistory { get; set; }
    [JsonPropertyName("averagePriceNQ")] public double AveragePriceNq { get; set; }
    [JsonPropertyName("averagePriceHQ")] public double AveragePriceHq { get; set; }
    [JsonPropertyName("minPriceNQ")] public long MinPriceNq { get; set; }
    [JsonPropertyName("minPriceHQ")] public long MinPriceHq { get; set; }
    [JsonPropertyName("maxPriceNQ")] public long MaxPriceNq { get; set; }
    [JsonPropertyName("maxPriceHQ")] public long MaxPriceHq { get; set; }
    [JsonPropertyName("nqSaleVelocity")] public double NqSaleVelocity { get; set; }
    [JsonPropertyName("hqSaleVelocity")] public double HqSaleVelocity { get; set; }
    [JsonPropertyName("unitsForSale")] public int UnitsForSale { get; set; }
    [JsonPropertyName("unitsSold")] public int UnitsSold { get; set; }
}

internal sealed class UniversalisListing
{
    [JsonPropertyName("pricePerUnit")] public long PricePerUnit { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("total")] public long Total { get; set; }
    [JsonPropertyName("hq")] public bool Hq { get; set; }
    [JsonPropertyName("worldName")] public string? WorldName { get; set; }
    [JsonPropertyName("retainerName")] public string? RetainerName { get; set; }
    [JsonPropertyName("lastReviewTime")] public long LastReviewTime { get; set; }
}

internal sealed class UniversalisSale
{
    [JsonPropertyName("pricePerUnit")] public long PricePerUnit { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("total")] public long Total { get; set; }
    [JsonPropertyName("hq")] public bool Hq { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("worldName")] public string? WorldName { get; set; }
    [JsonPropertyName("buyerName")] public string? BuyerName { get; set; }
}

internal sealed class UniversalisAggregatedResponse
{
    [JsonPropertyName("results")] public UniversalisAggregatedResult[]? Results { get; set; }
}

internal sealed class UniversalisAggregatedResult
{
    [JsonPropertyName("itemId")] public uint ItemId { get; set; }
    [JsonPropertyName("nq")] public UniversalisAggregatedQuality? Nq { get; set; }
    [JsonPropertyName("hq")] public UniversalisAggregatedQuality? Hq { get; set; }
}

internal sealed class UniversalisAggregatedQuality
{
    [JsonPropertyName("minListing")] public UniversalisAggregatedField? MinListing { get; set; }
}

internal sealed class UniversalisAggregatedField
{
    [JsonPropertyName("world")] public UniversalisAggregatedValue? World { get; set; }
    [JsonPropertyName("dc")] public UniversalisAggregatedValue? Dc { get; set; }
    [JsonPropertyName("region")] public UniversalisAggregatedValue? Region { get; set; }
}

internal sealed class UniversalisAggregatedValue
{
    [JsonPropertyName("price")] public long Price { get; set; }
    [JsonPropertyName("worldId")] public uint WorldId { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UniversalisCurrentData))]
[JsonSerializable(typeof(UniversalisAggregatedResponse))]
internal sealed partial class UniversalisJsonContext : JsonSerializerContext
{
}
