using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class SocialClient
{
    private readonly AethernetTransport net;

    public SocialClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, string? regions, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/feed?scope={scope}";
        if (regions is not null)
        {
            path += $"&regions={Uri.EscapeDataString(regions)}";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<PostDto?> CreatePostAsync(string text, string[]? mediaKeys, int mediaWidth, int mediaHeight, bool sensitive, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/posts", new CreatePostRequest(text, null, mediaKeys, mediaWidth, mediaHeight, sensitive), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<FeedPage?> UserPostsAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/users/{userId}/posts";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<FeedPage?> TagPostsAsync(string tag, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/tags/{Uri.EscapeDataString(tag)}/posts";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<PostDto?> PostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/posts/{Uri.EscapeDataString(postId)}", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<bool> DeletePostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{postId}", token, null, onFailure);
    }

    public Task<UserListPage?> FollowersAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/followers", cursor, token, onFailure);
    }

    public Task<UserListPage?> FollowingAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/following", cursor, token, onFailure);
    }

    public Task<UserListPage?> PostLikersAsync(string postId, string? cursor, CancellationToken token,
        int reactionKind = -1, Action<AepFailure>? onFailure = null)
    {
        var path = $"/posts/{Uri.EscapeDataString(postId)}/likers";
        if (reactionKind >= 0)
        {
            path += $"?kind={reactionKind}";
        }

        return UserListAsync(path, cursor, token, onFailure);
    }

    public Task<UserListPage?> MutualFollowersAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/mutual-followers", cursor, token, onFailure);
    }

    private Task<UserListPage?> UserListAsync(string path, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        if (cursor is not null)
        {
            path += path.Contains('?') ? "&" : "?";
            path += $"cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.UserListPage, token, null, onFailure);
    }

    public Task<FollowResultDto?> FollowAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/follows/{userId}", AethernetJsonContext.Default.FollowResultDto, token, null, onFailure);
    }

    public Task<bool> UnfollowAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/follows/{userId}", token, null, onFailure);
    }

    public Task<UserListPage?> RequestsAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return UserListAsync("/follows/requests", cursor, token, onFailure);
    }

    public Task<bool> AcceptFollowRequestAsync(string requesterId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/follows/requests/{Uri.EscapeDataString(requesterId)}/accept", token, null, onFailure);
    }

    public Task<bool> DeclineFollowRequestAsync(string requesterId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/follows/requests/{Uri.EscapeDataString(requesterId)}", token, null, onFailure);
    }

    public Task<bool> SavePostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}/save", token, null, onFailure);
    }

    public Task<bool> UnsavePostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{Uri.EscapeDataString(postId)}/save", token, null, onFailure);
    }

    public Task<PostDto?> SetSensitiveAsync(string postId, bool sensitive, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}/sensitive",
            new SetSensitiveRequest(sensitive), AethernetJsonContext.Default.SetSensitiveRequest,
            AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> EditCaptionAsync(string postId, string caption, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}/caption",
            new EditGramCaptionRequest(caption), AethernetJsonContext.Default.EditGramCaptionRequest,
            AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<TagSearchResult?> TagSearchAsync(string query, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = query.Length > 0 ? $"/tags/search?q={Uri.EscapeDataString(query)}" : "/tags/search";
        return net.GetAsync(path, AethernetJsonContext.Default.TagSearchResult, token, null, onFailure);
    }

    public Task<FeedPage?> LikedAsync(string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = "/me/liked";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<FeedPage?> SavedAsync(string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = "/me/saved";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<PostDto?> ReactAsync(string postId, int kind, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{postId}/reaction", new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> RemoveReactionAsync(string postId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/reaction", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> LikeAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/like", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> UnlikeAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/like", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> RepostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/repost", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> UnrepostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/repost", AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> QuotePostAsync(string text, string quotedPostId, string[]? mediaKeys, int mediaWidth, int mediaHeight, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/posts", new CreatePostRequest(text, quotedPostId, mediaKeys, mediaWidth, mediaHeight), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<CommentPage?> CommentsAsync(string postId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/posts/{postId}/comments";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CommentPage, token, null, onFailure);
    }

    public Task<CommentDto?> AddCommentAsync(string postId, string text, string? mediaKey, int mediaWidth,
        int mediaHeight, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/posts/{postId}/comments", new CreateCommentRequest(text, mediaKey, mediaWidth, mediaHeight), AethernetJsonContext.Default.CreateCommentRequest, AethernetJsonContext.Default.CommentDto, token, null, onFailure);
    }

    public Task<bool> DeleteCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{postId}/comments/{commentId}", token, null, onFailure);
    }

    public Task<CommentDto?> LikeCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/comments/{commentId}/like", AethernetJsonContext.Default.CommentDto, token, null, onFailure);
    }

    public Task<CommentDto?> UnlikeCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/comments/{commentId}/like", AethernetJsonContext.Default.CommentDto, token, null, onFailure);
    }
}
