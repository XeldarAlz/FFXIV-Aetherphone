using System.Text.Json.Serialization;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ChallengeRequest))]
[JsonSerializable(typeof(ChallengeResponse))]
[JsonSerializable(typeof(VerifyRequest))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UpdateProfileRequest))]
[JsonSerializable(typeof(CreatePostRequest))]
[JsonSerializable(typeof(ReactRequest))]
[JsonSerializable(typeof(PostDto))]
[JsonSerializable(typeof(FeedPage))]
[JsonSerializable(typeof(UserSearchResult))]
internal sealed partial class AethernetJsonContext : JsonSerializerContext
{
}
