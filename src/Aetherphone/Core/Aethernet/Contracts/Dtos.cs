namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record ChallengeRequest(string Name, string World);

internal sealed record ChallengeResponse(string ChallengeId, string Code, string Instructions);

internal sealed record VerifyRequest(string ChallengeId);

internal sealed record AuthResponse(string Token, UserDto User);

internal sealed record UserDto(
    string Id,
    string Name,
    string World,
    string DisplayName,
    int Followers,
    int Following,
    bool IsFollowing);

internal sealed record CreatePostRequest(string Text);

internal sealed record PostDto(
    string Id,
    string AuthorId,
    string AuthorName,
    string AuthorWorld,
    string Text,
    long CreatedAtUnix,
    int Likes,
    bool LikedByMe);

internal sealed record FeedPage(PostDto[] Items, string? NextCursor);

internal sealed record UserSearchResult(UserDto[] Users);
