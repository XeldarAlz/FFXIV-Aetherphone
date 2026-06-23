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

    public AethernetClient(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
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
        return http.GetJsonAsync(Url("/me"), AethernetJsonContext.Default.UserDto, session.Token, token);
    }

    public Task<FeedPage?> FeedAsync(string? cursor, CancellationToken token)
    {
        var path = cursor is null ? "/feed" : $"/feed?cursor={Uri.EscapeDataString(cursor)}";
        return http.GetJsonAsync(Url(path), AethernetJsonContext.Default.FeedPage, session.Token, token);
    }

    public Task<PostDto?> CreatePostAsync(string text, CancellationToken token)
    {
        return http.PostJsonAsync(Url("/posts"), new CreatePostRequest(text), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, session.Token, token);
    }

    public Task<UserSearchResult?> SearchAsync(string query, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/search?q={Uri.EscapeDataString(query)}"), AethernetJsonContext.Default.UserSearchResult, session.Token, token);
    }

    public Task<FeedPage?> UserPostsAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{userId}/posts"), AethernetJsonContext.Default.FeedPage, session.Token, token);
    }

    public Task<bool> FollowAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/follows/{userId}"), session.Token, token);
    }

    public Task<bool> UnfollowAsync(string userId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/follows/{userId}"), session.Token, token);
    }

    public Task<bool> LikeAsync(string postId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Post, Url($"/posts/{postId}/like"), session.Token, token);
    }

    public Task<bool> UnlikeAsync(string postId, CancellationToken token)
    {
        return http.SendAsync(HttpMethod.Delete, Url($"/posts/{postId}/like"), session.Token, token);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
