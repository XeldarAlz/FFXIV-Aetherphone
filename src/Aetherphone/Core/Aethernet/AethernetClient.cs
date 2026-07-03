using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetClient
{
    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly Action<int> authStatusSink;

    public AethernetClient(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
        authStatusSink = session.ReportAuthStatus;
    }

    public Task<ChallengeResponse?> ChallengeAsync(string name, string world, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/auth/challenge"), new ChallengeRequest(name, world), AethernetJsonContext.Default.ChallengeRequest, AethernetJsonContext.Default.ChallengeResponse, null, token);
    }

    public Task<AuthResponse?> VerifyAsync(string challengeId, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/auth/verify"), new VerifyRequest(challengeId), AethernetJsonContext.Default.VerifyRequest, AethernetJsonContext.Default.AuthResponse, null, token);
    }

    public Task<UserDto?> MeAsync(CancellationToken token)
    {
        return http.GetJsonAsync(Url("/me"), AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
    }

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url("/me"), request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, session.Token, token, authStatusSink);
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

    public Task<bool> ReportAsync(string targetType, string targetId, string? reason, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, Url("/reports"), new ReportRequest(targetType, targetId, reason), AethernetJsonContext.Default.ReportRequest, session.Token, token, authStatusSink);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
