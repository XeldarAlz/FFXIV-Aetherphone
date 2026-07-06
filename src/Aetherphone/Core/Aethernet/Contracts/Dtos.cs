namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record ChallengeRequest(string Name, string World);

internal sealed record ChallengeResponse(string ChallengeId, string Code, string Instructions);

internal sealed record VerifyRequest(string ChallengeId);

internal sealed record AuthResponse(string Token, UserDto User);

internal sealed record VerifyResponse(bool Ok, string? Reason, string? Token, UserDto? User);

internal sealed record XivAuthStartRequest(string Name, string World);

internal sealed record XivAuthStartResponse(
    bool Ok,
    string? Reason,
    string? FlowId,
    string? UserCode,
    string? VerificationUri,
    string? VerificationUriComplete,
    int IntervalSeconds,
    int ExpiresInSeconds);

internal sealed record XivAuthPollRequest(string FlowId);

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
    int Grams,
    int? UtcOffsetMinutes = null,
    bool ShareTimeZone = true);

internal sealed record UpdateProfileRequest(string? DisplayName, string? Handle, string? Bio, string? AvatarUrl = null);

internal sealed record UpdateTimeZoneRequest(bool? ShareTimeZone, int? UtcOffsetMinutes);

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
    int CommentCount,
    bool IsFollowing);

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

internal sealed record VelvetProfileDto(
    string UserId,
    string DisplayName,
    string Handle,
    bool Verified,
    string Intro,
    string Pronouns,
    string Dynamic,
    string[] Tags,
    string[] Limits,
    int LookingFor,
    int RelationshipStatus,
    string DataCenter,
    string World,
    int ConnectionState,
    bool Discoverable,
    string? AvatarUrl,
    long GateAckAtUnix,
    bool ShareTimeZone = true,
    int? UtcOffsetMinutes = null);

internal sealed record UpdateVelvetProfileRequest(
    string? Intro,
    string? Pronouns,
    string? Dynamic,
    string[]? Tags,
    string[]? Limits,
    int? LookingFor,
    int? RelationshipStatus,
    bool? Discoverable);

internal sealed record GateAcceptRequest(int GateVersion);

internal sealed record VelvetPostDto(
    string Id,
    string OwnerId,
    string OwnerDisplayName,
    string OwnerHandle,
    string? OwnerAvatarUrl,
    string MediaId,
    string MediaUrl,
    string Caption,
    string[] Tags,
    int Visibility,
    bool Unlocked,
    int MediaWidth,
    int MediaHeight,
    long CreatedAtUnix,
    int[] ReactionCounts,
    int TotalReactions,
    int MyReaction,
    int CommentCount);

internal sealed record VelvetFeedPage(VelvetPostDto[] Items, string? NextCursor);

internal sealed record CreateVelvetPostRequest(
    string MediaKey,
    int Width,
    int Height,
    string Caption,
    string[] Tags,
    int Visibility);

internal sealed record VelvetCommentDto(
    string Id,
    string PostId,
    string AuthorId,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Text,
    long CreatedAtUnix);

internal sealed record VelvetCommentPage(VelvetCommentDto[] Items, string? NextCursor);

internal sealed record CreateVelvetCommentRequest(string Text);

internal sealed record VelvetDiscoverPage(VelvetProfileDto[] Users, string? NextCursor);

internal sealed record VelvetConnectionDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    int State,
    int Presence,
    long ConnectedAtUnix,
    int? UtcOffsetMinutes = null);

internal sealed record VelvetConnectionPage(VelvetConnectionDto[] Items, string? NextCursor);

internal sealed record VelvetThreadDto(
    string Id,
    string OtherUserId,
    string OtherDisplayName,
    string OtherHandle,
    string? OtherAvatarUrl,
    long LastMessageAtUnix,
    string LastMessagePreview,
    int UnreadCount,
    int Presence,
    int? UtcOffsetMinutes = null);

internal sealed record VelvetThreadPage(VelvetThreadDto[] Items, string? NextCursor);

internal sealed record VelvetMessageDto(
    string Id,
    string ThreadId,
    string SenderId,
    string Body,
    int Kind,
    long CreatedAtUnix,
    long? ExpiresAtUnix,
    int MediaWidth = 0,
    int MediaHeight = 0);

internal sealed record VelvetMessagePage(VelvetMessageDto[] Items, string? NextCursor);

internal sealed record SendVelvetMessageRequest(
    string Body,
    int Kind,
    int? TtlSeconds,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0);

internal sealed record VelvetMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record VelvetTypingDto(bool OtherTyping);

internal sealed record NotificationDto(
    string Id,
    int Type,
    string App,
    string? PostId,
    string ActorId,
    string ActorName,
    string ActorDisplayName,
    string ActorHandle,
    string? ActorAvatarUrl,
    string? Preview,
    long CreatedAtUnix);

internal sealed record NotificationPage(NotificationDto[] Items);

internal sealed record CreateFeedbackRequest(string Text, string[] ImageKeys);

internal sealed record PollTranslationDto(string Lang, string Question, string[] Options);

internal sealed record PollDto(
    string Id,
    string Question,
    string[] Options,
    PollTranslationDto[] Translations,
    int[] VoteCounts,
    int TotalVotes,
    int MyVote,
    long CreatedAtUnix,
    bool Closed);

internal sealed record PollPage(PollDto[] Items);

internal sealed record PollVoteRequest(int Option);

internal sealed record FeedbackDto(
    string Id,
    string AuthorId,
    string AuthorName,
    string AuthorWorld,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Text,
    long CreatedAtUnix);

internal sealed record DevBoardCardDto(
    string Id,
    string Title,
    string Body,
    int Status,
    int SortOrder,
    string CreatedById,
    string CreatedByDisplayName,
    string CreatedByHandle,
    string? CreatedByAvatarUrl,
    long CreatedAtUnix,
    long UpdatedAtUnix);

internal sealed record DevBoardCards(DevBoardCardDto[] Items);

internal sealed record CreateDevCardRequest(string Title, string Body);

internal sealed record UpdateDevCardRequest(string? Title, string? Body);

internal sealed record MoveDevCardRequest(int Status, string? BeforeId);

internal sealed record DevChatMessageDto(
    string Id,
    string SenderId,
    string SenderDisplayName,
    string SenderHandle,
    string? SenderAvatarUrl,
    string Body,
    int Kind,
    int MediaWidth,
    int MediaHeight,
    long CreatedAtUnix);

internal sealed record DevChatPage(DevChatMessageDto[] Items, string? NextCursor);

internal sealed record SendDevChatMessageRequest(string Body, string? MediaKey = null, int MediaWidth = 0, int MediaHeight = 0);

internal sealed record DevMediaUrlDto(string Url, long ExpiresAtUnix);
