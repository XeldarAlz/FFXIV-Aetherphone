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
    string Handle,
    string Bio,
    int Followers,
    int Following,
    int Posts,
    bool IsFollowing,
    bool IsMe);

internal sealed record UpdateProfileRequest(string? DisplayName, string? Handle, string? Bio);

internal sealed record CreatePostRequest(string Text);

internal sealed record ReactRequest(int Kind);

internal sealed record PostDto(
    string Id,
    string AuthorId,
    string AuthorName,
    string AuthorWorld,
    string AuthorDisplayName,
    string AuthorHandle,
    string Text,
    long CreatedAtUnix,
    int[] ReactionCounts,
    int TotalReactions,
    int MyReaction);

internal sealed record FeedPage(PostDto[] Items, string? NextCursor);

internal sealed record UserSearchResult(UserDto[] Users);
