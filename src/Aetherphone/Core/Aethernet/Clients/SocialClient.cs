using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class SocialClient
{
    private readonly AethernetTransport net;

    public SocialClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/feed?scope={scope}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<PostDto?> CreatePostAsync(string text, CancellationToken token)
    {
        return net.PostAsync("/posts", new CreatePostRequest(text), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, token);
    }

    public Task<FeedPage?> UserPostsAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/users/{userId}/posts", AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<PostDto?> PostAsync(string postId, CancellationToken token)
    {
        return net.GetAsync($"/posts/{Uri.EscapeDataString(postId)}", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<bool> DeletePostAsync(string postId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{postId}", token);
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

    public Task<UserListPage?> MutualFollowersAsync(string userId, string? cursor, CancellationToken token)
    {
        return UserListAsync($"/users/{Uri.EscapeDataString(userId)}/mutual-followers", cursor, token);
    }

    private Task<UserListPage?> UserListAsync(string path, string? cursor, CancellationToken token)
    {
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.UserListPage, token);
    }

    public Task<FollowResultDto?> FollowAsync(string userId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/follows/{userId}", AethernetJsonContext.Default.FollowResultDto, token);
    }

    public Task<bool> UnfollowAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/follows/{userId}", token);
    }

    public Task<UserListPage?> RequestsAsync(string? cursor, CancellationToken token)
    {
        return UserListAsync("/follows/requests", cursor, token);
    }

    public Task<bool> AcceptFollowRequestAsync(string requesterId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/follows/requests/{Uri.EscapeDataString(requesterId)}/accept", token);
    }

    public Task<bool> DeclineFollowRequestAsync(string requesterId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/follows/requests/{Uri.EscapeDataString(requesterId)}", token);
    }

    public Task<bool> SavePostAsync(string postId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}/save", token);
    }

    public Task<bool> UnsavePostAsync(string postId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{Uri.EscapeDataString(postId)}/save", token);
    }

    public Task<FeedPage?> SavedAsync(string? cursor, CancellationToken token)
    {
        var path = "/me/saved";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<PostDto?> ReactAsync(string postId, int kind, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{postId}/reaction", new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> RemoveReactionAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/reaction", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> LikeAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/like", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> UnlikeAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/like", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> RepostAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/repost", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> UnrepostAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/repost", AethernetJsonContext.Default.PostDto, token);
    }

    public Task<PostDto?> QuotePostAsync(string text, string quotedPostId, CancellationToken token)
    {
        return net.PostAsync("/posts", new CreatePostRequest(text, quotedPostId), AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, token);
    }

    public Task<CommentPage?> CommentsAsync(string postId, string? cursor, CancellationToken token)
    {
        var path = $"/posts/{postId}/comments";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CommentPage, token);
    }

    public Task<CommentDto?> AddCommentAsync(string postId, string text, CancellationToken token)
    {
        return net.PostAsync($"/posts/{postId}/comments", new CreateCommentRequest(text), AethernetJsonContext.Default.CreateCommentRequest, AethernetJsonContext.Default.CommentDto, token);
    }

    public Task<bool> DeleteCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/posts/{postId}/comments/{commentId}", token);
    }

    public Task<CommentDto?> LikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/posts/{postId}/comments/{commentId}/like", AethernetJsonContext.Default.CommentDto, token);
    }

    public Task<CommentDto?> UnlikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/posts/{postId}/comments/{commentId}/like", AethernetJsonContext.Default.CommentDto, token);
    }
}
