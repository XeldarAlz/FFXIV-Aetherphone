using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class VelvetClient
{
    private readonly AethernetTransport net;

    public VelvetClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<VelvetProfileDto?> MeAsync(CancellationToken token, Action<int>? onStatus = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/velvet/me", AethernetJsonContext.Default.VelvetProfileDto, token, onStatus, onFailure);
    }

    public Task<VelvetProfileDto?> UpdateProfileAsync(UpdateVelvetProfileRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, "/velvet/me", request, AethernetJsonContext.Default.UpdateVelvetProfileRequest, AethernetJsonContext.Default.VelvetProfileDto, token, null, onFailure);
    }

    public Task<VelvetProfileDto?> AcceptGateAsync(int gateVersion, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Post, "/velvet/gate/accept", new GateAcceptRequest(gateVersion), AethernetJsonContext.Default.GateAcceptRequest, AethernetJsonContext.Default.VelvetProfileDto, token, null, onFailure);
    }

    public Task<VelvetProfileDto?> UserAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/velvet/users/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.VelvetProfileDto, token, null, onFailure);
    }

    public Task<VelvetProfileDto?> AddCardPhotoAsync(AddVelvetCardPhotoRequest request, CancellationToken token,
        Action<int>? onStatus = null, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Post, "/velvet/me/photos", request,
            AethernetJsonContext.Default.AddVelvetCardPhotoRequest, AethernetJsonContext.Default.VelvetProfileDto, token,
            onStatus, onFailure);
    }

    public Task<VelvetProfileDto?> RemoveCardPhotoAsync(string photoId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/velvet/me/photos/{Uri.EscapeDataString(photoId)}",
            AethernetJsonContext.Default.VelvetProfileDto, token, null, onFailure);
    }

    public Task<VelvetProfileDto?> ReorderCardPhotosAsync(string[] photoIds, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/velvet/me/photos/order", new ReorderVelvetCardPhotosRequest(photoIds),
            AethernetJsonContext.Default.ReorderVelvetCardPhotosRequest, AethernetJsonContext.Default.VelvetProfileDto,
            token, null, onFailure);
    }

    public Task<VelvetDiscoverPage?> DiscoverAsync(VelvetDiscoverFilter filter, string tags, string region,
        string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = new System.Text.StringBuilder("/velvet/discover");
        AppendFilter(path, filter, region);
        if (tags.Length > 0)
        {
            path.Append("&tags=").Append(Uri.EscapeDataString(tags));
        }

        AppendCursor(path, cursor);
        return net.GetAsync(path.ToString(), AethernetJsonContext.Default.VelvetDiscoverPage, token, null, onFailure);
    }

    private static void AppendFilter(System.Text.StringBuilder path, VelvetDiscoverFilter filter, string region)
    {
        path.Append("?lookingFor=").Append(filter.IntentInclude);
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
        AppendMask(path, "race", filter.RaceInclude);
        AppendMask(path, "raceExclude", filter.RaceExclude);
        AppendMask(path, "languages", filter.LanguagesInclude);
        AppendMask(path, "languagesExclude", filter.LanguagesExclude);
        AppendMask(path, "activeWithinDays", filter.ActiveWithinDays);
        if (filter.HasPhoto)
        {
            path.Append("&hasPhoto=true");
        }
        if (region.Length > 0)
        {
            path.Append("&region=").Append(Uri.EscapeDataString(region));
        }
    }

    private static void AppendCursor(System.Text.StringBuilder path, string? cursor)
    {
        if (cursor is not null)
        {
            path.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
        }
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

    public Task<bool> ConnectAsync(string userId, string intro, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/velvet/connect/{Uri.EscapeDataString(userId)}";
        if (!string.IsNullOrEmpty(intro))
        {
            path += "?intro=" + Uri.EscapeDataString(intro);
        }

        return net.SendAsync(HttpMethod.Post, path, token, null, onFailure);
    }

    public Task<bool> DisconnectAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/connect/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<bool> DeleteThreadAsync(string otherId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/threads/{Uri.EscapeDataString(otherId)}", token, null, onFailure);
    }

    public Task<VelvetConnectionPage?> RequestsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/velvet/requests", AethernetJsonContext.Default.VelvetConnectionPage, token, null, onFailure);
    }

    public Task<VelvetConnectionPage?> SentRequestsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/velvet/requests/sent", AethernetJsonContext.Default.VelvetConnectionPage, token, null, onFailure);
    }

    public Task<bool> DeclineRequestAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/requests/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<VelvetConnectionPage?> ConnectionsAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/velvet/connections";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetConnectionPage, token, null, onFailure);
    }

    public Task<VelvetFeedPage?> FeedAsync(string scope, VelvetDiscoverFilter filter, string region, string[] postTags,
        string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = new System.Text.StringBuilder("/velvet/feed");
        AppendFilter(path, filter, region);
        path.Append("&scope=").Append(Uri.EscapeDataString(scope));
        AppendCsv(path, "postTags", TokenCsv(postTags));
        AppendCursor(path, cursor);
        return net.GetAsync(path.ToString(), AethernetJsonContext.Default.VelvetFeedPage, token, null, onFailure);
    }

    public Task<VelvetPostDto?> PostAsync(string postId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}", AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<VelvetUserPostsPage?> UserPostsAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/velvet/users/{Uri.EscapeDataString(userId)}/posts";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetUserPostsPage, token, null, onFailure);
    }

    public Task<VelvetPostDto?> CreatePostAsync(CreateVelvetPostRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/velvet/posts", request, AethernetJsonContext.Default.CreateVelvetPostRequest, AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<bool> DeletePostAsync(string postId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}", token, null, onFailure);
    }

    public Task<VelvetPostDto?> SetPostAudienceAsync(string postId, int audience, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/velvet/posts/{Uri.EscapeDataString(postId)}/audience", new UpdateVelvetPostAudienceRequest(audience), AethernetJsonContext.Default.UpdateVelvetPostAudienceRequest, AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<VelvetPostDto?> EditCaptionAsync(string postId, string caption, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/velvet/posts/{Uri.EscapeDataString(postId)}/caption", new EditVelvetCaptionRequest(caption), AethernetJsonContext.Default.EditVelvetCaptionRequest, AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<VelvetPostDto?> ReactAsync(string postId, int kind, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction", new ReactRequest(kind), AethernetJsonContext.Default.ReactRequest, AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<VelvetPostDto?> RemoveReactionAsync(string postId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/reaction", AethernetJsonContext.Default.VelvetPostDto, token, null, onFailure);
    }

    public Task<UserListPage?> PostLikersAsync(string postId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/velvet/posts/{Uri.EscapeDataString(postId)}/reactions";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.UserListPage, token, null, onFailure);
    }

    public Task<VelvetCommentPage?> CommentsAsync(string postId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetCommentPage, token, null, onFailure);
    }

    public Task<VelvetCommentDto?> AddCommentAsync(string postId, string text, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/velvet/posts/{Uri.EscapeDataString(postId)}/comments", new CreateVelvetCommentRequest(text), AethernetJsonContext.Default.CreateVelvetCommentRequest, AethernetJsonContext.Default.VelvetCommentDto, token, null, onFailure);
    }

    public Task<bool> DeleteCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}", token, null, onFailure);
    }

    public Task<VelvetCommentDto?> LikeCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like", AethernetJsonContext.Default.VelvetCommentDto, token, null, onFailure);
    }

    public Task<VelvetCommentDto?> UnlikeCommentAsync(string postId, string commentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/velvet/posts/{Uri.EscapeDataString(postId)}/comments/{Uri.EscapeDataString(commentId)}/like", AethernetJsonContext.Default.VelvetCommentDto, token, null, onFailure);
    }

    public Task<VelvetThreadPage?> ThreadsAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/velvet/threads";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetThreadPage, token, null, onFailure);
    }

    public Task<VelvetMessagePage?> MessagesAsync(string threadId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.VelvetMessagePage, token, null, onFailure);
    }

    public Task<VelvetMessageDto?> SendMessageAsync(string threadId, string body, int kind, int? ttlSeconds, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/velvet/threads/{Uri.EscapeDataString(threadId)}/messages", new SendVelvetMessageRequest(body, kind, ttlSeconds, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, durationSecs), AethernetJsonContext.Default.SendVelvetMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, token, null, onFailure);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token, null, onFailure);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/velvet/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token, null, onFailure);
    }

    public Task<VelvetMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/velvet/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.VelvetMessageDto, token, null, onFailure);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/velvet/messages/{Uri.EscapeDataString(messageId)}", token, null, onFailure);
    }

    public Task<bool> SendTypingAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/velvet/threads/{Uri.EscapeDataString(userId)}/typing", token, null, onFailure);
    }

    public Task<VelvetTypingDto?> TypingAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/velvet/threads/{Uri.EscapeDataString(userId)}/typing", AethernetJsonContext.Default.VelvetTypingDto, token, null, onFailure);
    }

    public Task<bool> HeartbeatAsync(int? utcOffsetMinutes, bool? isLalafell, int raceId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = new System.Text.StringBuilder("/velvet/heartbeat");
        var separator = '?';
        if (utcOffsetMinutes is { } offset)
        {
            path.Append(separator).Append("utcOffsetMinutes=").Append(offset);
            separator = '&';
        }

        if (raceId > 0)
        {
            path.Append(separator).Append("race=").Append(raceId);
            separator = '&';
        }

        if (isLalafell is { } reported)
        {
            path.Append(separator).Append("lalafell=").Append(reported ? "true" : "false");
        }

        return net.SendAsync(HttpMethod.Post, path.ToString(), token, null, onFailure);
    }

    public Task<VelvetMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/velvet/media/dm/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.VelvetMediaUrlDto, token, null, onFailure);
    }
}
