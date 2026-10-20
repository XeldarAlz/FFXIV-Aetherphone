using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class DmClient
{
    private readonly AethernetTransport net;
    private readonly string surface;

    public DmClient(AethernetTransport net, string surface)
    {
        this.net = net;
        this.surface = surface;
    }

    public Task<DmThreadPage?> ThreadsAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/{surface}/threads";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.DmThreadPage, token, null, onFailure);
    }

    public Task<DmMessagePage?> MessagesAsync(string threadId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/{surface}/threads/{Uri.EscapeDataString(threadId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.DmMessagePage, token, null, onFailure);
    }

    public Task<DmMessageDto?> SendMessageAsync(string threadId, string body, int kind, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0, string? storyId = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/{surface}/threads/{Uri.EscapeDataString(threadId)}/messages", new SendDmMessageRequest(body, kind, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, durationSecs, storyId), AethernetJsonContext.Default.SendDmMessageRequest, AethernetJsonContext.Default.DmMessageDto, token, null, onFailure);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/{surface}/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token, null, onFailure);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/{surface}/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token, null, onFailure);
    }

    public Task<DmMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/{surface}/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.DmMessageDto, token, null, onFailure);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/{surface}/messages/{Uri.EscapeDataString(messageId)}", token, null, onFailure);
    }

    public Task<bool> AcceptThreadAsync(string otherId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/{surface}/threads/{Uri.EscapeDataString(otherId)}/accept", token, null, onFailure);
    }

    public Task<bool> MarkThreadReadAsync(string otherId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/{surface}/threads/{Uri.EscapeDataString(otherId)}/read", token, null, onFailure);
    }

    public Task<bool> DeleteThreadAsync(string otherId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/{surface}/threads/{Uri.EscapeDataString(otherId)}", token, null, onFailure);
    }

    public Task<bool> SendTypingAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/{surface}/threads/{Uri.EscapeDataString(userId)}/typing", token, null, onFailure);
    }

    public Task<DmTypingDto?> TypingAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/{surface}/threads/{Uri.EscapeDataString(userId)}/typing", AethernetJsonContext.Default.DmTypingDto, token, null, onFailure);
    }

    public Task<DmMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/{surface}/media/dm/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.DmMediaUrlDto, token, null, onFailure);
    }
}
