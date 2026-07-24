using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class VelvetClient
{
    private readonly AethernetTransport net;

    public VelvetClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<VelvetProfileDto?> MeAsync(CancellationToken token)
    {
        return net.GetAsync("/velvet/me", AethernetJsonContext.Default.VelvetProfileDto, token);
    }

    public Task<VelvetProfileDto?> UpdateProfileAsync(UpdateVelvetProfileRequest request, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Patch, "/velvet/me", request, AethernetJsonContext.Default.UpdateVelvetProfileRequest, AethernetJsonContext.Default.VelvetProfileDto, token);
    }

    public Task<VelvetProfileDto?> AcceptGateAsync(int gateVersion, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Post, "/velvet/gate/accept", new GateAcceptRequest(gateVersion), AethernetJsonContext.Default.GateAcceptRequest, AethernetJsonContext.Default.VelvetProfileDto, token);
    }

    public Task<VelvetProfileDto?> UserAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/users/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.VelvetProfileDto, token);
    }

    public Task<VelvetDiscoverPage?> DiscoverAsync(VelvetDiscoverFilter filter, string tags, string region,
        string? cursor, CancellationToken token)
    {
        var path = new System.Text.StringBuilder("/velvet/discover?lookingFor=").Append(filter.IntentInclude);
        AppendMask(path, "lookingForExclude", filter.IntentExclude);
        AppendMask(path, "gender", filter.GenderInclude);
        AppendMask(path, "genderExclude", filter.GenderExclude);
        AppendMask(path, "sexuality", filter.SexualityInclude);
        AppendMask(path, "sexualityExclude", filter.SexualityExclude);
        AppendCsv(path, "relationship", StatusCsv(filter.RelationshipInclude));
        AppendCsv(path, "relationshipExclude", StatusCsv(filter.RelationshipExclude));
        AppendCsv(path, "roles", TokenCsv(filter.RolesInclude));
        AppendCsv(path, "rolesExclude", TokenCsv(filter.RolesExclude));
        AppendCsv(path, "kinks", TokenCsv(filter.KinksInclude));
        AppendCsv(path, "kinksExclude", TokenCsv(filter.KinksExclude));
        AppendCsv(path, "limits", TokenCsv(filter.LimitsInclude));
        AppendCsv(path, "limitsExclude", TokenCsv(filter.LimitsExclude));
        AppendCsv(path, "profileTags", TokenCsv(filter.TagsInclude));
        AppendCsv(path, "profileTagsExclude", TokenCsv(filter.TagsExclude));
        if (filter.IncludeLalafell)
        {
            path.Append("&includeLalafell=true");
        }

        if (tags.Length > 0)
        {
            path.Append("&tags=").Append(Uri.EscapeDataString(tags));
        }

        if (region.Length > 0)
        {
            path.Append("&region=").Append(Uri.EscapeDataString(region));
        }

        if (cursor is not null)
        {
            path.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
        }

        return net.GetAsync(path.ToString(), AethernetJsonContext.Default.VelvetDiscoverPage, token);
    }

    private static void AppendMask(System.Text.StringBuilder path, string name, int mask)
    {
        if (mask > 0)
        {
            path.Append('&').Append(name).Append('=').Append(mask);
        }
    }

    private static void AppendCsv(System.Text.StringBuilder path, string name, string csv)
    {
        if (csv.Length > 0)
        {
            path.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(csv));
        }
    }

    private static string StatusCsv(int mask)
    {
        if (mask == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        for (var status = 0; status < 32; status++)
        {
            if ((mask & (1 << status)) == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(status);
        }

        return builder.ToString();
    }

    private static string TokenCsv(string[] tokens) => tokens.Length == 0 ? string.Empty : string.Join(',', tokens);

    public Task<bool> ConnectAsync(string userId, string intro, CancellationToken token)
    {
        var path = $"/velvet/connect/{Uri.EscapeDataString(userId)}";
        if (!string.IsNullOrEmpty(intro))
        {
            path += "?intro=" + Uri.EscapeDataString(intro);
        }

        return net.SendAsync(HttpMethod.Post, path, token);
    }

    public Task<bool> DisconnectAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/connect/{Uri.EscapeDataString(userId)}", token);
    }

    public Task<VelvetConnectionPage?> RequestsAsync(CancellationToken token)
    {
        return net.GetAsync("/velvet/requests", AethernetJsonContext.Default.VelvetConnectionPage, token);
    }

    public Task<VelvetConnectionPage?> SentRequestsAsync(CancellationToken token)
    {
        return net.GetAsync("/velvet/requests/sent", AethernetJsonContext.Default.VelvetConnectionPage, token);
    }

    public Task<bool> DeclineRequestAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/requests/{Uri.EscapeDataString(userId)}", token);
    }

    public Task<VelvetConnectionPage?> ConnectionsAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/connections";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetConnectionPage, token);
    }

    public Task<VelvetFeedPage?> FeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/feed?scope={Uri.EscapeDataString(scope)}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetFeedPage, token);
    }

    public Task<VelvetPostDto?> PostAsync(string postId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}", AethernetJsonContext.Default.VelvetPostDto, token);
    }

    public Task<VelvetPostDto?> CreatePostAsync(CreateVelvetPostRequest request, CancellationToken token)
    {
        return net.PostAsync("/velvet/posts", request, AethernetJsonContext.Default.CreateVelvetPostRequest, AethernetJsonContext.Default.VelvetPostDto, token);
    }

    public Task<bool> DeletePostAsync(string postId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}", token);
    }

    public Task<VelvetPostDto?> ReactAsync(string postId, int kind, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction", new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.VelvetPostDto, token);
    }

    public Task<VelvetPostDto?> RemoveReactionAsync(string postId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction", AethernetJsonContext.Default.VelvetPostDto, token);
    }

    public Task<UserListPage?> PostLikersAsync(string postId, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/posts/{Uri.EscapeDataString(postId)}/reactions";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.UserListPage, token);
    }

    public Task<VelvetCommentPage?> CommentsAsync(string postId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments", AethernetJsonContext.Default.VelvetCommentPage, token);
    }

    public Task<VelvetCommentDto?> AddCommentAsync(string postId, string text, CancellationToken token)
    {
        return net.PostAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments", new CreateVelvetCommentRequest(text), AethernetJsonContext.Default.CreateVelvetCommentRequest, AethernetJsonContext.Default.VelvetCommentDto, token);
    }

    public Task<bool> DeleteCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}", token);
    }

    public Task<VelvetCommentDto?> LikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like", AethernetJsonContext.Default.VelvetCommentDto, token);
    }

    public Task<VelvetCommentDto?> UnlikeCommentAsync(string postId, string commentId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like", AethernetJsonContext.Default.VelvetCommentDto, token);
    }

    public Task<VelvetThreadPage?> ThreadsAsync(string? cursor, CancellationToken token)
    {
        var path = "/velvet/threads";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetThreadPage, token);
    }

    public Task<VelvetMessagePage?> MessagesAsync(string threadId, string? cursor, CancellationToken token)
    {
        var path = $"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetMessagePage, token);
    }

    public Task<VelvetMessageDto?> SendMessageAsync(string threadId, string body, int kind, int? ttlSeconds, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0)
    {
        return net.PostAsync($"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages", new SendVelvetMessageRequest(body, kind, ttlSeconds, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, durationSecs), AethernetJsonContext.Default.SendVelvetMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, token);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token);
    }

    public Task<VelvetMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/velvet/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, token);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/messages/{Uri.EscapeDataString(messageId)}", token);
    }

    public Task<bool> SendTypingAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/velvet/threads/{Uri.EscapeDataString(userId)}/typing", token);
    }

    public Task<VelvetTypingDto?> TypingAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/threads/{Uri.EscapeDataString(userId)}/typing", AethernetJsonContext.Default.VelvetTypingDto, token);
    }

    public Task<bool> HeartbeatAsync(int? utcOffsetMinutes, string region, bool isLalafell, CancellationToken token)
    {
        var path = new System.Text.StringBuilder("/velvet/heartbeat?");
        if (utcOffsetMinutes is { } offset)
        {
            path.Append("utcOffsetMinutes=").Append(offset).Append('&');
        }

        path.Append("region=").Append(Uri.EscapeDataString(region));
        path.Append("&lalafell=").Append(isLalafell ? "true" : "false");
        return net.SendAsync(HttpMethod.Post, path.ToString(), token);
    }

    public Task<VelvetUserPostsPage?> UserPostsAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/users/{Uri.EscapeDataString(userId)}/posts",
            AethernetJsonContext.Default.VelvetUserPostsPage, token);
    }

    public Task<VelvetMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/media/dm/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.VelvetMediaUrlDto, token);
    }
}
