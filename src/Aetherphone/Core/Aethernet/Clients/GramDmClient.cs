using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GramDmClient
{
    private readonly AethernetTransport net;

    public GramDmClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<GramThreadPage?> ThreadsAsync(string? cursor, CancellationToken token)
    {
        var path = "/gram/threads";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.GramThreadPage, token);
    }

    public Task<GramMessagePage?> MessagesAsync(string threadId, string? cursor, CancellationToken token)
    {
        var path = $"/gram/threads/{Uri.EscapeDataString(threadId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.GramMessagePage, token);
    }

    public Task<GramMessageDto?> SendMessageAsync(string threadId, string body, int kind, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0)
    {
        return net.PostAsync($"/gram/threads/{Uri.EscapeDataString(threadId)}/messages", new SendGramMessageRequest(body, kind, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, durationSecs), AethernetJsonContext.Default.SendGramMessageRequest, AethernetJsonContext.Default.GramMessageDto, token);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/gram/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/gram/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token);
    }

    public Task<GramMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/gram/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.GramMessageDto, token);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/gram/messages/{Uri.EscapeDataString(messageId)}", token);
    }

    public Task<bool> SendTypingAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/gram/threads/{Uri.EscapeDataString(userId)}/typing", token);
    }

    public Task<GramTypingDto?> TypingAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/gram/threads/{Uri.EscapeDataString(userId)}/typing", AethernetJsonContext.Default.GramTypingDto, token);
    }

    public Task<GramMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/gram/media/dm/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.GramMediaUrlDto, token);
    }
}
