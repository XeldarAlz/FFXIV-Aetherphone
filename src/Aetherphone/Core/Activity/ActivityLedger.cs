using Newtonsoft.Json;

namespace Aetherphone.Core.Activity;

internal sealed class ActivityLedger
{
    [JsonProperty("days")] public List<ActivityDay> Days { get; set; } = new();
}
