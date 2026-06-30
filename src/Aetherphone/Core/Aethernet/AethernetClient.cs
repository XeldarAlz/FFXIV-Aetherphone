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

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Patch, Url("/me"), request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, session.Token, token);
    }

    public Task<UserDto?> UserAsync(string userId, CancellationToken token)
    {
        return http.GetJsonAsync(Url($"/users/{Uri.EscapeDataString(userId)}"), AethernetJsonContext.Default.UserDto, session.Token, token);
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/feed?scope={scope}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

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

    public Task<PostDto?> ReactAsync(string postId, int kind, CancellationToken token)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url($"/posts/{postId}/reaction"), new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.PostDto, session.Token, token);
    }

    public Task<PostDto?> RemoveReactionAsync(string postId, CancellationToken token)
    {
        return http.RequestJsonAsync(HttpMethod.Delete, Url($"/posts/{postId}/reaction"), AethernetJsonContext.Default.PostDto, session.Token, token);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
