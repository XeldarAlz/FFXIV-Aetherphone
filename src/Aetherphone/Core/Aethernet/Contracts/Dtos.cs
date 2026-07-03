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
    bool IsMe,
    string? AvatarUrl,
    int Grams);

internal sealed record UpdateProfileRequest(string? DisplayName, string? Handle, string? Bio, string? AvatarUrl = null);

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
    int MyReaction,
    int Kind,
    string? MediaUrl,
    int MediaWidth,
    int MediaHeight,
    string? AuthorAvatarUrl,
    int CommentCount);

internal sealed record FeedPage(PostDto[] Items, string? NextCursor);

internal sealed record UserSearchResult(UserDto[] Users);

internal sealed record UploadUrlRequest(string ContentType, string Scope);

internal sealed record UploadUrlResponse(string Key, string UploadUrl, string PublicUrl);

internal sealed record CreateGramRequest(string Caption, string MediaKey, int Width, int Height);

internal sealed record CommentDto(
    string Id,
    string PostId,
    string AuthorId,
    string AuthorName,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Text,
    long CreatedAtUnix);

internal sealed record CreateCommentRequest(string Text);

internal sealed record CommentPage(CommentDto[] Items, string? NextCursor);

internal sealed record AnalyticsEventDto(string Type, string? AppId, DateTime? ClientTime, string? Props);

internal sealed record AnalyticsBatchRequest(
    string InstallId,
    string SessionId,
    string PluginVersion,
    string GameRegion,
    AnalyticsEventDto[] Events);

internal sealed record AnalyticsAckDto(int Accepted);

internal sealed record ReportRequest(string TargetType, string TargetId, string? Reason);
