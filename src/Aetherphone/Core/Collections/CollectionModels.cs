using System.Text.Json.Serialization;

namespace Aetherphone.Core.Collections;

internal enum CollectionCategory : byte
{
    Mounts,
    Minions,
    Emotes,
    Orchestrions,
    Hairstyles,
    Facewear,
    Achievements,
    TriadCards,
}

internal enum CollectionState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal enum OwnedState : byte
{
    Unknown,
    Loading,
    Ready,
    Private,
    Failed,
}

internal enum SummaryState : byte
{
    Unknown,
    Loading,
    Ready,
    Failed,
}

internal enum CollectionAccess : byte
{
    Public,
    Private,
    NotSynced,
}

internal sealed class CollectionSource
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

internal sealed class CollectionGroupRef
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal sealed class CollectionStatStrip
{
    [JsonPropertyName("top")] public int Top { get; set; }
    [JsonPropertyName("right")] public int Right { get; set; }
    [JsonPropertyName("bottom")] public int Bottom { get; set; }
    [JsonPropertyName("left")] public int Left { get; set; }
}

internal sealed class CollectionStats
{
    [JsonPropertyName("numeric")] public CollectionStatStrip? Numeric { get; set; }
}

internal sealed class CollectionItemDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("command")] public string? Command { get; set; }
    [JsonPropertyName("patch")] public string? Patch { get; set; }
    [JsonPropertyName("tradeable")] public bool? Tradeable { get; set; }
    [JsonPropertyName("owned")] public string? Owned { get; set; }
    [JsonPropertyName("points")] public int? Points { get; set; }
    [JsonPropertyName("stars")] public int? Stars { get; set; }
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("category")] public CollectionGroupRef? Category { get; set; }
    [JsonPropertyName("type")] public CollectionGroupRef? Type { get; set; }
    [JsonPropertyName("stats")] public CollectionStats? Stats { get; set; }
    [JsonPropertyName("sources")] public CollectionSource[]? Sources { get; set; }
}

internal sealed class CollectionResponse
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("results")] public CollectionItemDto[]? Results { get; set; }
}

internal sealed class OwnedItemDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
}

internal sealed class CharacterCollectionStat
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("public")] public bool? Public { get; set; }
}

internal sealed class CharacterSummaryDto
{
    [JsonPropertyName("mounts")] public CharacterCollectionStat? Mounts { get; set; }
    [JsonPropertyName("minions")] public CharacterCollectionStat? Minions { get; set; }
    [JsonPropertyName("emotes")] public CharacterCollectionStat? Emotes { get; set; }
    [JsonPropertyName("orchestrions")] public CharacterCollectionStat? Orchestrions { get; set; }
    [JsonPropertyName("hairstyles")] public CharacterCollectionStat? Hairstyles { get; set; }
    [JsonPropertyName("facewear")] public CharacterCollectionStat? Facewear { get; set; }
    [JsonPropertyName("achievements")] public CharacterCollectionStat? Achievements { get; set; }
    [JsonPropertyName("cards")] public CharacterCollectionStat? Cards { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CollectionResponse))]
[JsonSerializable(typeof(OwnedItemDto[]))]
[JsonSerializable(typeof(CharacterSummaryDto))]
internal sealed partial class CollectionJsonContext : JsonSerializerContext
{
}
