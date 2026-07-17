using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetClient
{
    private readonly ScopedHttp http;
    private readonly AethernetSession session;
    private readonly Action<int> authStatusSink;

    public AethernetClient(HttpService http, AethernetSession session, string appScope = "")
    {
        this.http = new ScopedHttp(http, appScope);
        this.session = session;
        authStatusSink = session.ReportAuthStatus;
    }

    public Task<ChallengeResponse?> ChallengeAsync(string name, string world, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/auth/challenge"), new ChallengeRequest(name, world), AethernetJsonContext.Default.ChallengeRequest, AethernetJsonContext.Default.ChallengeResponse, null, token);
    }

    public async Task<VerifyResult> VerifyAsync(string challengeId, CancellationToken token)
    {
        var status = 0;
        var response = await http.PostJsonAsync(Url("/auth/verify"), new VerifyRequest(challengeId), AethernetJsonContext.Default.VerifyRequest, AethernetJsonContext.Default.VerifyResponse, null, token, statusCode => status = statusCode).ConfigureAwait(false);
        if (response is not null)
        {
            if (response.Ok && response.Token is not null && response.User is not null)
            {
                return VerifyResult.Success(new AuthResponse(response.Token, response.User));
            }

            return VerifyResult.Failure(response.Reason ?? VerifyFailure.CodeNotFound);
        }

        return VerifyResult.Failure(status == 429 ? VerifyFailure.RateLimited : VerifyFailure.Network);
    }

    public Task<XivAuthStartResponse?> StartXivAuthAsync(string name, string world, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/auth/xivauth/start"), new XivAuthStartRequest(name, world), AethernetJsonContext.Default.XivAuthStartRequest, AethernetJsonContext.Default.XivAuthStartResponse, null, token);
    }

    public async Task<VerifyResult> PollXivAuthAsync(string flowId, CancellationToken token)
    {
        var status = 0;
        var response = await http.PostJsonAsync(Url("/auth/xivauth/poll"), new XivAuthPollRequest(flowId), AethernetJsonContext.Default.XivAuthPollRequest, AethernetJsonContext.Default.VerifyResponse, null, token, statusCode => status = statusCode).ConfigureAwait(false);
        if (response is not null)
        {
            if (response.Ok && response.Token is not null && response.User is not null)
            {
                return VerifyResult.Success(new AuthResponse(response.Token, response.User));
            }

            return VerifyResult.Failure(response.Reason ?? VerifyFailure.Pending);
        }

        return VerifyResult.Failure(status == 429 ? VerifyFailure.RateLimited : VerifyFailure.Network);
    }

    public Task<UserDto?> MeAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/me"), AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public void EnsureCurrentUser()
    {
        if (!session.IsSignedIn || session.CurrentUser is not null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var user = await MeAsync(CancellationToken.None).ConfigureAwait(false);
                if (user is not null)
                {
                    session.SetUser(user);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet account load failed: {exception.Message}");
            }
        });
    }

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url("/me"), request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UpdateTimeZoneAsync(UpdateTimeZoneRequest request, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/me/timezone"), request, AethernetJsonContext.Default.UpdateTimeZoneRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UserAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{Uri.EscapeDataString(userId)}"), AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/feed?scope={scope}";

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
    }

    public Task<PostDto?> CreatePostAsync(string text, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/posts"), new CreatePostRequest(text), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<UserSearchResult?> SearchAsync(string query, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/search?q={Uri.EscapeDataString(query)}"), AethernetJsonContext.Default.UserSearchResult, session.Token, token, authStatusSink);
    }

    public Task<MentionSuggestResult?> MentionSuggestAsync(string query, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/mention-suggest?q={Uri.EscapeDataString(query)}"), AethernetJsonContext.Default.MentionSuggestResult, session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UpdateMentionPrivacyAsync(int policy, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/me/mention-privacy"), new UpdateMentionPrivacyRequest(policy), AethernetJsonContext.Default.UpdateMentionPrivacyRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<ContactListResult?> ContactsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/contacts/"), AethernetJsonContext.Default.ContactListResult, session.Token, token, authStatusSink);
    }

    public Task<ContactDto?> AddContactAsync(string number, string? alias, CancellationToken token, Action<int>? statusSink = null)
    {
        var sink = statusSink is null
            ? authStatusSink
            : status =>
            {
                authStatusSink(status);
                statusSink(status);
            };
        return http.PostJsonAsync(Url("/contacts/"), new AddContactRequest(number, alias), AethernetJsonContext.Default.AddContactRequest, AethernetJsonContext.Default.ContactDto, session.Token, token, sink);
    }

    public Task<bool> RemoveContactAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/contacts/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<NumberChangeStatusResult?> NumberChangeStatusAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/contacts/number-change"), AethernetJsonContext.Default.NumberChangeStatusResult, session.Token, token, authStatusSink);
    }

    public Task<NumberChangeStatusResult?> RequestNumberChangeAsync(string reason, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/contacts/number-change"), new CreateNumberChangeRequest(reason), AethernetJsonContext.Default.CreateNumberChangeRequest, AethernetJsonContext.Default.NumberChangeStatusResult, session.Token, token, authStatusSink);
    }

    public Task<ConversationPage?> ConversationsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/chats/"), AethernetJsonContext.Default.ConversationPage, session.Token, token, authStatusSink);
    }

    public Task<ConversationDetailDto?> ConversationAsync(string conversationId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/chats/{Uri.EscapeDataString(conversationId)}"), AethernetJsonContext.Default.ConversationDetailDto, session.Token, token, authStatusSink);
    }

    public Task<ConversationDetailDto?> CreateConversationAsync(CreateConversationRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        var sink = statusSink is null
            ? authStatusSink
            : status =>
            {
                authStatusSink(status);
                statusSink(status);
            };
        return http.PostJsonAsync(Url("/chats/"), request, AethernetJsonContext.Default.CreateConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, session.Token, token, sink);
    }

    public Task<ChatMessagePage?> ChatMessagesAsync(string conversationId, string? cursor, CancellationToken token)
    {
        var path = $"/chats/{Uri.EscapeDataString(conversationId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.ChatMessagePage, session.Token, token, authStatusSink);
    }

    public Task<ChatMessageDto?> SendChatMessageAsync(string conversationId, string body, int kind, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, string? forwardOfId = null, bool forwarded = false, int durationSecs = 0)
    {
        return http.PostJsonAsync(Url($"/chats/{Uri.EscapeDataString(conversationId)}/messages"), new SendChatMessageRequest(body, kind, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, forwardOfId, forwarded, durationSecs), AethernetJsonContext.Default.SendChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, session.Token, token, authStatusSink);
    }

    public Task<bool> SetChatReactionAsync(string messageId, string reactionToken, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions"), new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, session.Token, token, authStatusSink);
    }

    public Task<ReactionListDto?> ChatReactionsAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions"), AethernetJsonContext.Default.ReactionListDto, session.Token, token, authStatusSink);
    }

    public Task<ChatMessageDto?> EditChatMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url($"/chats/messages/{Uri.EscapeDataString(messageId)}"), new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UpdateChatPrivacyAsync(UpdateChatPrivacyRequest request, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/me/chat-privacy"), request, AethernetJsonContext.Default.UpdateChatPrivacyRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteChatMessageAsync(string messageId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/chats/messages/{Uri.EscapeDataString(messageId)}"), session.Token, token, authStatusSink);
    }

    public Task<bool> MuteConversationAsync(string conversationId, bool muted, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/chats/{Uri.EscapeDataString(conversationId)}/mute"), new MuteConversationRequest(muted), AethernetJsonContext.Default.MuteConversationRequest, session.Token, token, authStatusSink);
    }

    public Task<MyKeysDto?> PutMyKeysAsync(PutMyKeysRequest request, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url("/keys/me"), request, AethernetJsonContext.Default.PutMyKeysRequest, AethernetJsonContext.Default.MyKeysDto, session.Token, token, authStatusSink);
    }

    public async Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token)
    {
        var status = 0;
        var keys = await http.GetJsonAsync(Url("/keys/me"), AethernetJsonContext.Default.MyKeysDto, session.Token, token, Combine(statusCode => status = statusCode)).ConfigureAwait(false);
        return (keys, status);
    }

    public Task<PublicKeysDto?> PublicKeysAsync(string[] userIds, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/keys/users"), new PublicKeysRequest(userIds), AethernetJsonContext.Default.PublicKeysRequest, AethernetJsonContext.Default.PublicKeysDto, session.Token, token, authStatusSink);
    }

    public Task<MyConversationKeysDto?> MyConversationKeysAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/keys/conversations"), AethernetJsonContext.Default.MyConversationKeysDto, session.Token, token, authStatusSink);
    }

    public Task<ConversationKeysDto?> ConversationKeysAsync(string conversationId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/chats/{Uri.EscapeDataString(conversationId)}/keys"), AethernetJsonContext.Default.ConversationKeysDto, session.Token, token, authStatusSink);
    }

    public async Task<(bool Ok, int Status)> CreateConversationGenerationAsync(string conversationId, CreateGenerationRequest request, CancellationToken token)
    {
        var status = 0;
        var ok = await http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/chats/{Uri.EscapeDataString(conversationId)}/keys"), request, AethernetJsonContext.Default.CreateGenerationRequest, session.Token, token, Combine(statusCode => status = statusCode)).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddConversationWrapsAsync(string conversationId, AddWrapsRequest request, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/chats/{Uri.EscapeDataString(conversationId)}/keys/wraps"), request, AethernetJsonContext.Default.AddWrapsRequest, session.Token, token, authStatusSink);
    }

    public Task<MyConversationKeysDto?> VelvetKeysAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/velvet/keys"), AethernetJsonContext.Default.MyConversationKeysDto, session.Token, token, authStatusSink);
    }

    public Task<ConversationKeysDto?> VelvetThreadKeysAsync(string otherId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys"), AethernetJsonContext.Default.ConversationKeysDto, session.Token, token, authStatusSink);
    }

    public async Task<(bool Ok, int Status)> CreateVelvetGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token)
    {
        var status = 0;
        var ok = await http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys"), request, AethernetJsonContext.Default.CreateGenerationRequest, session.Token, token, Combine(statusCode => status = statusCode)).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddVelvetWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys/wraps"), request, AethernetJsonContext.Default.AddWrapsRequest, session.Token, token, authStatusSink);
    }

    public Task<ConversationDetailDto?> AddChatMembersAsync(string conversationId, string[] memberIds, CancellationToken token)
    {
        return http.PostJsonAsync(Url($"/chats/{Uri.EscapeDataString(conversationId)}/members"), new AddMembersRequest(memberIds), AethernetJsonContext.Default.AddMembersRequest, AethernetJsonContext.Default.ConversationDetailDto, session.Token, token, authStatusSink);
    }

    public Task<bool> RemoveChatMemberAsync(string conversationId, string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/chats/{Uri.EscapeDataString(conversationId)}/members/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<ConversationDetailDto?> RenameConversationAsync(string conversationId, string title, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url($"/chats/{Uri.EscapeDataString(conversationId)}"), new RenameConversationRequest(title), AethernetJsonContext.Default.RenameConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, session.Token, token, authStatusSink);
    }

    public Task<bool> SendChatTypingAsync(string conversationId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/chats/{Uri.EscapeDataString(conversationId)}/typing"), session.Token, token, authStatusSink);
    }

    public Task<ChatTypingDto?> ChatTypingAsync(string conversationId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/chats/{Uri.EscapeDataString(conversationId)}/typing"), AethernetJsonContext.Default.ChatTypingDto, session.Token, token, authStatusSink);
    }

    public Task<ChatMediaUrlDto?> ChatDmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/chats/media/{Uri.EscapeDataString(messageId)}/url"), AethernetJsonContext.Default.ChatMediaUrlDto, session.Token, token, authStatusSink);
    }

    public Task<FeedPage?> UserPostsAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{userId}/posts"), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
    }

    public Task<UserListPage?> FollowersAsync(string userId, string? cursor, CancellationToken token)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/followers", cursor, token);
    }

    public Task<UserListPage?> FollowingAsync(string userId, string? cursor, CancellationToken token)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/following", cursor, token);
    }

    public Task<UserListPage?> PostLikersAsync(string postId, string? cursor, CancellationToken token)
    {
        return UserListAsync($"/posts/{Uri.EscapeDataString(postId)}/likers", cursor, token);
    }

    public Task<UserListPage?> VelvetPostLikersAsync(string postId, string? cursor, CancellationToken token)
    {
        return UserListAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}/reactions", cursor, token);
    }

    private Task<UserListPage?> UserListAsync(string path, string? cursor, CancellationToken token)
    {
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.UserListPage, session.Token, token, authStatusSink);
    }

    public Task<PostDto?> PostAsync(string postId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/posts/{Uri.EscapeDataString(postId)}"), AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetPostDto?> VelvetPostAsync(string postId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/posts/{Uri.EscapeDataString(postId)}"), AethernetJsonContext.Default.VelvetPostDto, session.Token, token, authStatusSink);
    }

    public Task<bool> FollowAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/follows/{userId}"), session.Token, token, authStatusSink);
    }

    public Task<bool> UnfollowAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/follows/{userId}"), session.Token, token, authStatusSink);
    }

    public Task<PostDto?> ReactAsync(string postId, int kind, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url($"/posts/{postId}/reaction"), new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<PostDto?> RemoveReactionAsync(string postId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/posts/{postId}/reaction"), AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<PostDto?> LikeAsync(string postId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Post, Url($"/posts/{postId}/like"), AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<PostDto?> UnlikeAsync(string postId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/posts/{postId}/like"), AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<UploadUrlResponse?> UploadUrlAsync(string contentType, string scope, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/media/upload-url"), new UploadUrlRequest(contentType, scope), AethernetJsonContext.Default.UploadUrlRequest, AethernetJsonContext.Default.UploadUrlResponse, session.Token, token, authStatusSink);
    }

    public Task<bool> UploadImageAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken token)
    {
        return http.PutBytesAsync(new Uri(uploadUrl), bytes, contentType, token);
    }

    public Task<PostDto?> CreateGramAsync(string caption, string[] mediaKeys, int width, int height, PhotoTagInput[]? photoTags, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/grams"), new CreateGramRequest(caption, mediaKeys[0], width, height, mediaKeys, photoTags), AethernetJsonContext.Default.CreateGramRequest, AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
    }

    public Task<FeedPage?> UserTaggedGramsAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{Uri.EscapeDataString(userId)}/tagged"), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
    }

    public Task<PhotoTagPage?> PendingPhotoTagsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/phototags/pending"), AethernetJsonContext.Default.PhotoTagPage, session.Token, token, authStatusSink);
    }

    public Task<bool> ApprovePhotoTagAsync(string tagId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/phototags/{Uri.EscapeDataString(tagId)}/approve"), session.Token, token, authStatusSink);
    }

    public Task<bool> RemovePhotoTagAsync(string tagId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/phototags/{Uri.EscapeDataString(tagId)}"), session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UpdateTagPrivacyAsync(int? tagPolicy, bool? requireApproval, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/me/tag-privacy"), new UpdateTagPrivacyRequest(tagPolicy, requireApproval), AethernetJsonContext.Default.UpdateTagPrivacyRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<FeedPage?> GramFeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/feed?scope={scope}&kind=1";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
    }

    public Task<FeedPage?> UserGramsAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{Uri.EscapeDataString(userId)}/posts?kind=1"), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
    }

    public Task<StoryDto?> CreateStoryAsync(string caption, string mediaKey, int width, int height, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/stories"), new CreateStoryRequest(caption, mediaKey, width, height), AethernetJsonContext.Default.CreateStoryRequest, AethernetJsonContext.Default.StoryDto, session.Token, token, authStatusSink);
    }

    public Task<StoryTray?> StoryTrayAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/stories"), AethernetJsonContext.Default.StoryTray, session.Token, token, authStatusSink);
    }

    public Task<StoryGroup?> UserStoriesAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/stories/{Uri.EscapeDataString(userId)}"), AethernetJsonContext.Default.StoryGroup, session.Token, token, authStatusSink);
    }

    public Task<StoryViewersPage?> StoryViewersAsync(string storyId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/stories/{Uri.EscapeDataString(storyId)}/views"), AethernetJsonContext.Default.StoryViewersPage, session.Token, token, authStatusSink);
    }

    public Task<bool> MarkStoryViewedAsync(string storyId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/stories/{Uri.EscapeDataString(storyId)}/view"), session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteStoryAsync(string storyId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/stories/{Uri.EscapeDataString(storyId)}"), session.Token, token, authStatusSink);
    }

    public Task<CommentPage?> CommentsAsync(string postId, string? cursor, CancellationToken token)
    {
        var path = $"/posts/{postId}/comments";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.CommentPage, session.Token, token, authStatusSink);
    }

    public Task<CommentDto?> AddCommentAsync(string postId, string text, CancellationToken token)
    {
        return http.PostJsonAsync(Url($"/posts/{postId}/comments"), new CreateCommentRequest(text), AethernetJsonContext.Default.CreateCommentRequest, AethernetJsonContext.Default.CommentDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/posts/{postId}/comments/{commentId}"), session.Token, token, authStatusSink);
    }

    public Task<CommentDto?> LikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Post, Url($"/posts/{postId}/comments/{commentId}/like"), AethernetJsonContext.Default.CommentDto, session.Token, token, authStatusSink);
    }

    public Task<CommentDto?> UnlikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/posts/{postId}/comments/{commentId}/like"), AethernetJsonContext.Default.CommentDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeletePostAsync(string postId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/posts/{postId}"), session.Token, token, authStatusSink);
    }

    public Task<bool> ReportAsync(string targetType, string targetId, string? reason, CancellationToken token, RevealedMessageDto[]? revealedMessages = null)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url("/reports"), new ReportRequest(targetType, targetId, reason, revealedMessages), AethernetJsonContext.Default.ReportRequest, session.Token, token, authStatusSink);
    }

    public Task<VelvetProfileDto?> VelvetMeAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/velvet/me"), AethernetJsonContext.Default.VelvetProfileDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetProfileDto?> UpdateVelvetProfileAsync(UpdateVelvetProfileRequest request, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url("/velvet/me"), request, AethernetJsonContext.Default.UpdateVelvetProfileRequest, AethernetJsonContext.Default.VelvetProfileDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetProfileDto?> AcceptGateAsync(int gateVersion, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Post, Url("/velvet/gate/accept"), new GateAcceptRequest(gateVersion), AethernetJsonContext.Default.GateAcceptRequest, AethernetJsonContext.Default.VelvetProfileDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetCommentPage?> VelvetCommentsAsync(string postId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments"), AethernetJsonContext.Default.VelvetCommentPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetCommentDto?> AddVelvetCommentAsync(string postId, string text, CancellationToken token)
    {
        return http.PostJsonAsync(Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments"), new CreateVelvetCommentRequest(text), AethernetJsonContext.Default.CreateVelvetCommentRequest, AethernetJsonContext.Default.VelvetCommentDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteVelvetCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetCommentDto?> LikeVelvetCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Post, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like"), AethernetJsonContext.Default.VelvetCommentDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetCommentDto?> UnlikeVelvetCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like"), AethernetJsonContext.Default.VelvetCommentDto, session.Token, token, authStatusSink);
    }

    public Task<bool> SendVelvetTypingAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/velvet/threads/{Uri.EscapeDataString(userId)}/typing"), session.Token, token, authStatusSink);
    }

    public Task<VelvetTypingDto?> VelvetTypingAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/threads/{Uri.EscapeDataString(userId)}/typing"), AethernetJsonContext.Default.VelvetTypingDto, session.Token, token, authStatusSink);
    }

    public Task<bool> VelvetHeartbeatAsync(int? utcOffsetMinutes, CancellationToken token)
    {
        var path = utcOffsetMinutes is { } offset
            ? $"/velvet/heartbeat?utcOffsetMinutes={offset}"
            : "/velvet/heartbeat";
        return http.SendAsync(HttpMethod.Post, Url(path), session.Token, token, authStatusSink);
    }

    public Task<VelvetProfileDto?> VelvetUserAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/users/{Uri.EscapeDataString(userId)}"), AethernetJsonContext.Default.VelvetProfileDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetDiscoverPage?> VelvetDiscoverAsync(int lookingFor, string tags, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/discover?lookingFor={lookingFor}";
        if (tags.Length > 0)
        {
            path += $"&tags={Uri.EscapeDataString(tags)}";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetDiscoverPage, session.Token, token, authStatusSink);
    }

    public Task<bool> ConnectAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/velvet/connect/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<bool> ConnectAsync(string userId, string intro, CancellationToken token)
    {
        var url = Url($"/velvet/connect/{Uri.EscapeDataString(userId)}");
        if (!string.IsNullOrEmpty(intro))
        {
            url += "?intro=" + Uri.EscapeDataString(intro);
        }

        return http.SendAsync(HttpMethod.Post, url, session.Token, token, authStatusSink);
    }

    public Task<bool> DisconnectAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/velvet/connect/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetConnectionPage?> VelvetRequestsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/velvet/requests"), AethernetJsonContext.Default.VelvetConnectionPage, session.Token, token, authStatusSink);
    }

    public Task<bool> DeclineRequestAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/velvet/requests/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetConnectionPage?> VelvetSentRequestsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/velvet/requests/sent"), AethernetJsonContext.Default.VelvetConnectionPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetConnectionPage?> VelvetConnectionsAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/connections";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetConnectionPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetFeedPage?> VelvetFeedAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/feed";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetFeedPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetPostDto?> CreateVelvetPostAsync(CreateVelvetPostRequest request, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/velvet/posts"), request, AethernetJsonContext.Default.CreateVelvetPostRequest, AethernetJsonContext.Default.VelvetPostDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteVelvetPostAsync(string postId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetPostDto?> VelvetReactAsync(string postId, int kind, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction"), new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.VelvetPostDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetPostDto?> VelvetRemoveReactionAsync(string postId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction"), AethernetJsonContext.Default.VelvetPostDto, session.Token, token, authStatusSink);
    }

    public Task<bool> GrantMediaAsync(string mediaId, string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/velvet/media/{Uri.EscapeDataString(mediaId)}/grant/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetMediaUrlDto?> VelvetMediaUrlAsync(string mediaId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/media/{Uri.EscapeDataString(mediaId)}/url"), AethernetJsonContext.Default.VelvetMediaUrlDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetThreadPage?> VelvetThreadsAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/threads";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetThreadPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetMessagePage?> VelvetMessagesAsync(string threadId, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetMessagePage, session.Token, token, authStatusSink);
    }

    public Task<VelvetMessageDto?> SendVelvetMessageAsync(string threadId, string body, int kind, int? ttlSeconds, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0)
    {
        return http.PostJsonAsync(Url($"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages"), new SendVelvetMessageRequest(body, kind, ttlSeconds, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, durationSecs), AethernetJsonContext.Default.SendVelvetMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, session.Token, token, authStatusSink);
    }

    public Task<bool> SetVelvetReactionAsync(string messageId, string reactionToken, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url($"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions"), new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, session.Token, token, authStatusSink);
    }

    public Task<ReactionListDto?> VelvetReactionsAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions"), AethernetJsonContext.Default.ReactionListDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetMessageDto?> EditVelvetMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url($"/velvet/messages/{Uri.EscapeDataString(messageId)}"), new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteVelvetMessageAsync(string messageId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/velvet/messages/{Uri.EscapeDataString(messageId)}"), session.Token, token, authStatusSink);
    }

    public Task<VelvetMediaUrlDto?> VelvetDmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/media/dm/{Uri.EscapeDataString(messageId)}/url"), AethernetJsonContext.Default.VelvetMediaUrlDto, session.Token, token, authStatusSink);
    }

    public Task<bool> RevokeTokenAsync(string bearer, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url("/auth/token"), bearer, token, authStatusSink);
    }

    public Task<bool> DeleteAccountAsync(CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url("/me"), session.Token, token, authStatusSink);
    }

    public Task<bool> BlockAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/blocks/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<bool> UnblockAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/blocks/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<UserSearchResult?> BlockedUsersAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/blocks/"), AethernetJsonContext.Default.UserSearchResult, session.Token, token, authStatusSink);
    }

    public Task<FeedbackDto?> CreateFeedbackAsync(string text, string[] imageKeys, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/feedback"), new CreateFeedbackRequest(text, imageKeys), AethernetJsonContext.Default.CreateFeedbackRequest, AethernetJsonContext.Default.FeedbackDto, session.Token, token, authStatusSink);
    }

    public Task<NotificationPage?> NotificationsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/notifications"), AethernetJsonContext.Default.NotificationPage, session.Token, token, authStatusSink);
    }

    public async Task<bool?> DevAccessAsync(CancellationToken token)
    {
        var status = 0;
        var granted = await http.SendAsync(HttpMethod.Get, Url("/devspace/access"), session.Token, token, statusCode =>
        {
            status = statusCode;
            authStatusSink(statusCode);
        }).ConfigureAwait(false);
        if (granted)
        {
            return true;
        }

        return status == 404 ? false : null;
    }

    public Task<DevBoardCards?> DevBoardCardsAsync(CancellationToken token, Action<int>? onStatus = null)
    {
        return http.GetJsonAsync(Url("/devspace/board/cards"), AethernetJsonContext.Default.DevBoardCards, session.Token, token, Combine(onStatus));
    }

    public Task<DevBoardCardDto?> CreateDevCardAsync(string title, string body, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/devspace/board/cards"), new CreateDevCardRequest(title, body), AethernetJsonContext.Default.CreateDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, session.Token, token, authStatusSink);
    }

    public Task<DevBoardCardDto?> UpdateDevCardAsync(string cardId, string? title, string? body, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url($"/devspace/board/cards/{Uri.EscapeDataString(cardId)}"), new UpdateDevCardRequest(title, body), AethernetJsonContext.Default.UpdateDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, session.Token, token, authStatusSink);
    }

    public Task<DevBoardCardDto?> MoveDevCardAsync(string cardId, int status, string? beforeId, CancellationToken token)
    {
        return http.PostJsonAsync(Url($"/devspace/board/cards/{Uri.EscapeDataString(cardId)}/move"), new MoveDevCardRequest(status, beforeId), AethernetJsonContext.Default.MoveDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteDevCardAsync(string cardId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/devspace/board/cards/{Uri.EscapeDataString(cardId)}"), session.Token, token, authStatusSink);
    }

    public Task<DevChatPage?> DevChatMessagesAsync(long afterUnix, string? cursor, CancellationToken token, Action<int>? onStatus = null)
    {
        var path = "/devspace/chat/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }
        else if (afterUnix > 0)
        {
            path += $"?afterUnix={afterUnix}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.DevChatPage, session.Token, token, Combine(onStatus));
    }

    public Task<DevChatMessageDto?> SendDevChatMessageAsync(string body, string? mediaKey, int mediaWidth, int mediaHeight, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/devspace/chat/messages"), new SendDevChatMessageRequest(body, mediaKey, mediaWidth, mediaHeight), AethernetJsonContext.Default.SendDevChatMessageRequest, AethernetJsonContext.Default.DevChatMessageDto, session.Token, token, authStatusSink);
    }

    public Task<DevMediaUrlDto?> DevChatMediaUrlAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/devspace/chat/media/{Uri.EscapeDataString(messageId)}/url"), AethernetJsonContext.Default.DevMediaUrlDto, session.Token, token, authStatusSink);
    }

    public Task<bool> DeleteDevChatMessageAsync(string messageId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/devspace/chat/messages/{Uri.EscapeDataString(messageId)}"), session.Token, token, authStatusSink);
    }

    private Action<int> Combine(Action<int>? onStatus)
    {
        if (onStatus is null)
        {
            return authStatusSink;
        }

        var sink = authStatusSink;
        return statusCode =>
        {
            sink(statusCode);
            onStatus(statusCode);
        };
    }

    public Task<PollPage?> PollsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/polls"), AethernetJsonContext.Default.PollPage, session.Token, token, authStatusSink);
    }

    public Task<PollDto?> VoteAsync(string pollId, int option, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url($"/polls/{pollId}/vote"), new PollVoteRequest(option), AethernetJsonContext.Default.PollVoteRequest, AethernetJsonContext.Default.PollDto, session.Token, token, authStatusSink);
    }

    public Task<PollDto?> ClearVoteAsync(string pollId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/polls/{pollId}/vote"), AethernetJsonContext.Default.PollDto, session.Token, token, authStatusSink);
    }

    public Task<AssistantStatusResponse?> AssistantStatusAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/assistant/status"), AethernetJsonContext.Default.AssistantStatusResponse, session.Token, token, authStatusSink);
    }

    public Task<AssistantAskResponse?> AssistantAskAsync(AssistantAskRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        var sink = statusSink is null
            ? authStatusSink
            : status =>
            {
                authStatusSink(status);
                statusSink(status);
            };
        return http.PostJsonAsync(Url("/assistant/ask"), request, AethernetJsonContext.Default.AssistantAskRequest, AethernetJsonContext.Default.AssistantAskResponse, session.Token, token, sink);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
