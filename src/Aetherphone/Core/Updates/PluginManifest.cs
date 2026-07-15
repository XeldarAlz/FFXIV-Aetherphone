using System.Text.Json.Serialization;

namespace Aetherphone.Core.Updates;

internal sealed class PluginManifestEntry
{
    [JsonPropertyName("InternalName")] public string InternalName { get; set; } = string.Empty;

    [JsonPropertyName("AssemblyVersion")] public string? AssemblyVersion { get; set; }

    [JsonPropertyName("TestingAssemblyVersion")]
    public string? TestingAssemblyVersion { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PluginManifestEntry[]), TypeInfoPropertyName = "ManifestEntries")]
internal sealed partial class PluginManifestJsonContext : JsonSerializerContext
{
}
