using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class ChatClient
{
    private readonly AethernetTransport net;

    public ChatClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<ConversationPage?> ConversationsAsync(CancellationToken token)
    {
        return net.GetAsync("/chats/", AethernetJsonContext.Default.ConversationPage, token);
    }

    public Task<ConversationDetailDto?> ConversationAsync(string conversationId, CancellationToken token)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}", AethernetJsonContext.Default.ConversationDetailDto, token);
    }

    public Task<ConversationDetailDto?> CreateConversationAsync(CreateConversationRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        return net.PostAsync("/chats/", request, AethernetJsonContext.Default.CreateConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, token, statusSink);
    }

    public Task<ChatMessagePage?> MessagesAsync(string conversationId, string? cursor, CancellationToken token)
    {
        var path = $"/chats/{Uri.EscapeDataString(conversationId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ChatMessagePage, token);
    }

    public Task<ChatMessageDto?> SendMessageAsync(string conversationId, string body, int kind, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, string? forwardOfId = null, bool forwarded = false, int durationSecs = 0)
    {
        return net.PostAsync($"/chats/{Uri.EscapeDataString(conversationId)}/messages", new SendChatMessageRequest(body, kind, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, forwardOfId, forwarded, durationSecs), AethernetJsonContext.Default.SendChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, token);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token);
    }

    public Task<ChatMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/chats/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, token);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/chats/messages/{Uri.EscapeDataString(messageId)}", token);
    }

    public Task<bool> MuteConversationAsync(string conversationId, bool muted, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/mute", new MuteConversationRequest(muted), AethernetJsonContext.Default.MuteConversationRequest, token);
    }

    public Task<ConversationDetailDto?> AddMembersAsync(string conversationId, string[] memberIds, CancellationToken token)
    {
        return net.PostAsync($"/chats/{Uri.EscapeDataString(conversationId)}/members", new AddMembersRequest(memberIds), AethernetJsonContext.Default.AddMembersRequest, AethernetJsonContext.Default.ConversationDetailDto, token);
    }

    public Task<bool> RemoveMemberAsync(string conversationId, string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/chats/{Uri.EscapeDataString(conversationId)}/members/{Uri.EscapeDataString(userId)}", token);
    }

    public Task<ConversationDetailDto?> RenameConversationAsync(string conversationId, string title, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/chats/{Uri.EscapeDataString(conversationId)}", new RenameConversationRequest(title), AethernetJsonContext.Default.RenameConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, token);
    }

    public Task<bool> SendTypingAsync(string conversationId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/typing", token);
    }

    public Task<ChatTypingDto?> TypingAsync(string conversationId, CancellationToken token)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}/typing", AethernetJsonContext.Default.ChatTypingDto, token);
    }

    public Task<ChatMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/chats/media/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.ChatMediaUrlDto, token);
    }
}
