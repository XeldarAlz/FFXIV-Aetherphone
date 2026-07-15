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
    bool ShareTimeZone = true,
    bool ShareReadReceipts = true,
    bool SharePresence = true) : IIdentified;

internal sealed record UpdateProfileRequest(string? DisplayName, string? Handle, string? Bio, string? AvatarUrl = null);

internal sealed record UpdateTimeZoneRequest(bool? ShareTimeZone, int? UtcOffsetMinutes);

internal sealed record UpdateChatPrivacyRequest(bool? ShareReadReceipts, bool? SharePresence);

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
    bool IsFollowing,
    string ScanStatus = "clean",
    string[]? MediaUrls = null) : IIdentified;

internal sealed record FeedPage(PostDto[] Items, string? NextCursor);

internal sealed record UserSearchResult(UserDto[] Users);

internal sealed record UserListPage(UserDto[] Items, string? NextCursor);

internal sealed record UploadUrlRequest(string ContentType, string Scope);

internal sealed record UploadUrlResponse(string Key, string UploadUrl, string PublicUrl);

internal sealed record CreateGramRequest(
    string Caption,
    string MediaKey,
    int Width,
    int Height,
    string[]? MediaKeys = null);

internal sealed record CreateStoryRequest(string Caption, string MediaKey, int Width, int Height);

internal sealed record StoryDto(
    string Id,
    string AuthorId,
    string Caption,
    string MediaUrl,
    int MediaWidth,
    int MediaHeight,
    long CreatedAtUnix,
    long ExpiresAtUnix,
    bool Seen,
    int ViewCount,
    string ScanStatus = "clean") : IIdentified;

internal sealed record StoryRingDto(
    string AuthorId,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    bool IsMe,
    bool HasUnseen,
    int Count,
    long LatestAtUnix);

internal sealed record StoryTray(StoryRingDto[] Rings);

internal sealed record StoryGroup(StoryDto[] Items);

internal sealed record StoryViewerDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    long ViewedAtUnix);

internal sealed record StoryViewersPage(StoryViewerDto[] Items, int Total);

internal sealed record CommentDto(
    string Id,
    string PostId,
    string AuthorId,
    string AuthorName,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Text,
    long CreatedAtUnix,
    int LikeCount,
    bool Liked) : IIdentified;

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

internal sealed record RevealedMessageDto(string MessageId, string PlainText, string? FrankingKey);

internal sealed record ReportRequest(
    string TargetType,
    string TargetId,
    string? Reason,
    RevealedMessageDto[]? RevealedMessages = null);

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
    int? UtcOffsetMinutes = null,
    int WhoCanMessage = 0);

internal sealed record UpdateVelvetProfileRequest(
    string? Intro,
    string? Pronouns,
    string? Dynamic,
    string[]? Tags,
    string[]? Limits,
    int? LookingFor,
    int? RelationshipStatus,
    bool? Discoverable,
    int? WhoCanMessage = null);

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
    int CommentCount,
    string ScanStatus = "clean",
    string[]? MediaUrls = null) : IIdentified;

internal sealed record VelvetFeedPage(VelvetPostDto[] Items, string? NextCursor);

internal sealed record CreateVelvetPostRequest(
    string MediaKey,
    int Width,
    int Height,
    string Caption,
    string[] Tags,
    int Visibility,
    string[]? MediaKeys = null);

internal sealed record VelvetCommentDto(
    string Id,
    string PostId,
    string AuthorId,
    string AuthorDisplayName,
    string AuthorHandle,
    string? AuthorAvatarUrl,
    string Text,
    long CreatedAtUnix,
    int LikeCount,
    bool Liked) : IIdentified;

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
    int? UtcOffsetMinutes = null,
    string Intro = "");

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
    int? UtcOffsetMinutes = null,
    int LastMessageEncVersion = 0,
    string LastMessageSenderId = "") : IIdentified;

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
    int MediaHeight = 0,
    long? ReadAtUnix = null,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    string? ReplySenderId = null,
    string? ReplyBody = null,
    int ReplyKind = 0,
    int ReplyEncVersion = 0,
    bool Deleted = false,
    int DurationSecs = 0,
    ReactionSummaryDto[]? Reactions = null,
    long? EditedAtUnix = null) : IIdentified;

internal sealed record VelvetMessagePage(VelvetMessageDto[] Items, string? NextCursor);

internal sealed record SendVelvetMessageRequest(
    string Body,
    int Kind,
    int? TtlSeconds,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    int DurationSecs = 0);

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
    long CreatedAtUnix) : IIdentified;

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
    bool Closed) : IIdentified;

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
    long CreatedAtUnix) : IIdentified;

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
    long UpdatedAtUnix) : IIdentified;

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
    long CreatedAtUnix) : IIdentified;

internal sealed record DevChatPage(DevChatMessageDto[] Items, string? NextCursor);

internal sealed record SendDevChatMessageRequest(string Body, string? MediaKey = null, int MediaWidth = 0, int MediaHeight = 0);

internal sealed record DevMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record ContactDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    string PhoneNumber,
    string Alias,
    bool IsMutual,
    long CreatedAtUnix);

internal sealed record ContactListResult(ContactDto[] Contacts, string MyNumber);

internal sealed record AddContactRequest(string Number, string? Alias);

internal sealed record NumberChangeStatusDto(string Status, long CreatedAtUnix, long? ResolvedAtUnix);

internal sealed record NumberChangeStatusResult(NumberChangeStatusDto? Request);

internal sealed record CreateNumberChangeRequest(string Reason);

internal sealed record ConversationDto(
    string Id,
    bool IsGroup,
    string Title,
    string? AvatarUrl,
    int MemberCount,
    string OtherUserId,
    string OtherDisplayName,
    string OtherHandle,
    string? OtherAvatarUrl,
    string LastMessagePreview,
    int LastMessageKind,
    long LastMessageAtUnix,
    int UnreadCount,
    int Presence,
    int? UtcOffsetMinutes = null,
    int LastMessageEncVersion = 0,
    string LastMessageSenderId = "",
    bool Muted = false,
    long? LastSeenAtUnix = null) : IIdentified;

internal sealed record ConversationMemberDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    int Role,
    bool IsActive,
    long? LastReadAtUnix = null);

internal sealed record ChatMessageDto(
    string Id,
    string ConversationId,
    string SenderId,
    string SenderDisplayName,
    string SenderHandle,
    string? SenderAvatarUrl,
    string Body,
    int Kind,
    long CreatedAtUnix,
    int MediaWidth = 0,
    int MediaHeight = 0,
    long? ReadAtUnix = null,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    string? ReplySenderId = null,
    string? ReplySenderName = null,
    string? ReplyBody = null,
    int ReplyKind = 0,
    int ReplyEncVersion = 0,
    bool Deleted = false,
    bool Forwarded = false,
    int DurationSecs = 0,
    ReactionSummaryDto[]? Reactions = null,
    long? EditedAtUnix = null) : IIdentified;

internal sealed record ReactionSummaryDto(string Token, int Count, bool Mine);

internal sealed record SetReactionRequest(string Token);

internal sealed record MuteConversationRequest(bool Muted);

internal sealed record EditChatMessageRequest(string Body, int EncVersion = 0, string? CommitmentTag = null);

internal sealed record ReactorDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    string Token,
    long CreatedAtUnix);

internal sealed record ReactionListDto(ReactorDto[] Items);

internal sealed record ConversationPage(ConversationDto[] Items, string? NextCursor);

internal sealed record ChatMessagePage(ChatMessageDto[] Items, string? NextCursor);

internal sealed record ConversationDetailDto(ConversationDto Conversation, ConversationMemberDto[] Members);

internal sealed record CreateConversationRequest(string? TargetUserId, string? Title, string[]? MemberIds);

internal sealed record SendChatMessageRequest(
    string Body,
    int Kind,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    string? ForwardOfId = null,
    bool Forwarded = false,
    int DurationSecs = 0);

internal sealed record AddMembersRequest(string[] MemberIds);

internal sealed record RenameConversationRequest(string Title);

internal sealed record ChatTypingDto(string[] TypingUserIds);

internal sealed record ChatMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record WrappedPrivateKeyDto(string Salt, int Iterations, string Nonce, string Ciphertext);

internal sealed record PutMyKeysRequest(string PublicKey, WrappedPrivateKeyDto? PrivateKey = null);

internal sealed record MyKeysDto(
    string PublicKey,
    WrappedPrivateKeyDto? PrivateKey,
    int KeyVersion,
    long CreatedAtUnix,
    long? RotatedAtUnix);

internal sealed record UserPublicKeyDto(string UserId, string PublicKey, int KeyVersion);

internal sealed record PublicKeysRequest(string[] UserIds);

internal sealed record PublicKeysDto(UserPublicKeyDto[] Items);

internal sealed record KeyWrapDto(
    int Generation,
    string WrappedKey,
    string CreatedById,
    int RecipientKeyVersion,
    long CreatedAtUnix);

internal sealed record NewWrapDto(string RecipientUserId, int RecipientKeyVersion, string WrappedKey);

internal sealed record CreateGenerationRequest(int Generation, NewWrapDto[] Wraps);

internal sealed record AddWrapsRequest(int Generation, NewWrapDto[] Wraps);

internal sealed record ConversationKeysDto(
    string ConversationId,
    int CurrentGeneration,
    KeyWrapDto[] MyWraps,
    UserPublicKeyDto[] MemberKeys,
    string[] MembersWithoutKeys,
    string[] StaleWrapUserIds,
    string[] MissingWrapUserIds,
    bool NeedsNewGeneration);

internal sealed record ConversationWrapsDto(string ConversationId, int CurrentGeneration, KeyWrapDto[] Wraps);

internal sealed record MyConversationKeysDto(ConversationWrapsDto[] Items);

internal sealed record AssistantTurnDto(string Role, string Text);

internal sealed record AssistantAskRequest(string Question, AssistantTurnDto[]? History = null, string? ConversationId = null);

internal sealed record AssistantSourceDto(string Title, string Url);

internal sealed record AssistantAskResponse(
    string Status,
    string? Answer,
    AssistantSourceDto[] Sources,
    int RemainingToday,
    int DailyLimit);

internal sealed record AssistantStatusResponse(bool Ready, int RemainingToday, int DailyLimit);
