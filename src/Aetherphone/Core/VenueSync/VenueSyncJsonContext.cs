using System.Text.Json.Serialization;

namespace Aetherphone.Core.VenueSync;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(VenueSyncShiftsResponse))]
[JsonSerializable(typeof(VenueSyncClockResult))]
[JsonSerializable(typeof(VenueSyncShiftIdRequest))]
[JsonSerializable(typeof(VenueSyncServicesResponse))]
[JsonSerializable(typeof(VenueSyncTransactionRequest))]
[JsonSerializable(typeof(VenueSyncTransactionResult))]
[JsonSerializable(typeof(VenueSyncVenuesResponse))]
[JsonSerializable(typeof(VenueSyncLinkCharacterRequest))]
[JsonSerializable(typeof(VenueSyncLinkCharacterResponse))]
[JsonSerializable(typeof(VenueSyncPatronVisitRequest))]
[JsonSerializable(typeof(VenueSyncActiveEventResponse))]
[JsonSerializable(typeof(VenueSyncPatronVisitResult))]
internal sealed partial class VenueSyncJsonContext : JsonSerializerContext
{
}
