using System.Text.Json.Serialization;

namespace Aetherphone.Core.Telephony.Contracts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CallControl))]
[JsonSerializable(typeof(ParticipantInfo))]
[JsonSerializable(typeof(NearbyStreamInfo))]
[JsonSerializable(typeof(StreamQueueEntry))]
[JsonSerializable(typeof(StreamMember))]
[JsonSerializable(typeof(RadioChatMessage))]
[JsonSerializable(typeof(RadioSongRequest))]
[JsonSerializable(typeof(JamTrack))]
[JsonSerializable(typeof(JamQueueEntry))]
[JsonSerializable(typeof(JamMember))]
[JsonSerializable(typeof(JamNearbyInfo))]
[JsonSerializable(typeof(Aethernet.Contracts.ChatMessageDto))]
[JsonSerializable(typeof(CasinoPayload))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoRoomSnapshotDto))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoRoomEventDto))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoPrivateDto))]
[JsonSerializable(typeof(GamePayload))]
[JsonSerializable(typeof(Aethernet.Contracts.GameRoomSnapshotDto))]
[JsonSerializable(typeof(Aethernet.Contracts.GameRoomEventDto))]
[JsonSerializable(typeof(Aethernet.Contracts.GamePrivateDto))]
internal sealed partial class TelephonyJsonContext : JsonSerializerContext
{
}
