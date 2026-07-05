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

    public Task<FeedPage?> UserPostsAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{userId}/posts"), AethernetJsonContext.Default.FeedPage, session.Token, token, authStatusSink);
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

    public Task<PostDto?> CreateGramAsync(string caption, string mediaKey, int width, int height, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/grams"), new CreateGramRequest(caption, mediaKey, width, height), AethernetJsonContext.Default.CreateGramRequest, AethernetJsonContext.Default.PostDto, session.Token, token, authStatusSink);
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

    public Task<bool> DeletePostAsync(string postId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/posts/{postId}"), session.Token, token, authStatusSink);
    }

    public Task<bool> ReportAsync(string targetType, string targetId, string? reason, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url("/reports"), new ReportRequest(targetType, targetId, reason), AethernetJsonContext.Default.ReportRequest, session.Token, token, authStatusSink);
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

    public Task<VelvetConnectionPage?> VelvetConnectionsAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/connections";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.VelvetConnectionPage, session.Token, token, authStatusSink);
    }

    public Task<VelvetFeedPage?> VelvetFeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/feed?scope={scope}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
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

    public Task<VelvetMessageDto?> SendVelvetMessageAsync(string threadId, string body, int kind, int? ttlSeconds, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0)
    {
        return http.PostJsonAsync(Url($"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages"), new SendVelvetMessageRequest(body, kind, ttlSeconds, mediaKey, mediaWidth, mediaHeight), AethernetJsonContext.Default.SendVelvetMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, session.Token, token, authStatusSink);
    }

    public Task<VelvetMediaUrlDto?> VelvetDmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/velvet/media/dm/{Uri.EscapeDataString(messageId)}/url"), AethernetJsonContext.Default.VelvetMediaUrlDto, session.Token, token, authStatusSink);
    }

    public Task<bool> BlockAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/blocks/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<bool> UnblockAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/blocks/{Uri.EscapeDataString(userId)}"), session.Token, token, authStatusSink);
    }

    public Task<FeedbackDto?> CreateFeedbackAsync(string text, string[] imageKeys, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/feedback"), new CreateFeedbackRequest(text, imageKeys), AethernetJsonContext.Default.CreateFeedbackRequest, AethernetJsonContext.Default.FeedbackDto, session.Token, token, authStatusSink);
    }

    public Task<NotificationPage?> NotificationsAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/notifications"), AethernetJsonContext.Default.NotificationPage, session.Token, token, authStatusSink);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
