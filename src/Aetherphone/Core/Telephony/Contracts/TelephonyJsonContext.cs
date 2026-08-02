using System.Text.Json.Serialization;

namespace Aetherphone.Core.Telephony.Contracts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CallControl))]
[JsonSerializable(typeof(ParticipantInfo))]
[JsonSerializable(typeof(NearbyStreamInfo))]
internal sealed partial class TelephonyJsonContext : JsonSerializerContext
{
}
