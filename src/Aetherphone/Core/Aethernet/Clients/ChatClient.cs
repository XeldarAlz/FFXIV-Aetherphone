using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class ChatClient
{
    private readonly AethernetTransport net;

    public ChatClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<ConversationPage?> ConversationsAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/chats/";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ConversationPage, token, null, onFailure);
    }

    public Task<ConversationDetailDto?> ConversationAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}", AethernetJsonContext.Default.ConversationDetailDto, token, null, onFailure);
    }

    public Task<ConversationDetailDto?> CreateConversationAsync(CreateConversationRequest request, CancellationToken token, Action<int>? statusSink = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/chats/", request, AethernetJsonContext.Default.CreateConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, token, statusSink, onFailure);
    }

    public Task<ChatMessagePage?> MessagesAsync(string conversationId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/chats/{Uri.EscapeDataString(conversationId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ChatMessagePage, token, null, onFailure);
    }

    public Task<bool> MarkReadAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/read", token, null, onFailure);
    }

    public Task<ChatMessageDto?> SendMessageAsync(string conversationId, string body, int kind, CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0, int encVersion = 0, string? commitmentTag = null, string? replyToId = null, string? forwardOfId = null, bool forwarded = false, int durationSecs = 0,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/chats/{Uri.EscapeDataString(conversationId)}/messages", new SendChatMessageRequest(body, kind, mediaKey, mediaWidth, mediaHeight, encVersion, commitmentTag, replyToId, forwardOfId, forwarded, durationSecs), AethernetJsonContext.Default.SendChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, token, null, onFailure);
    }

    public Task<bool> SetReactionAsync(string messageId, string reactionToken, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions", new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token, null, onFailure);
    }

    public Task<ReactionListDto?> ReactionsAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/chats/messages/{Uri.EscapeDataString(messageId)}/reactions", AethernetJsonContext.Default.ReactionListDto, token, null, onFailure);
    }

    public Task<ChatMessageDto?> EditMessageAsync(string messageId, string body, CancellationToken token, int encVersion = 0, string? commitmentTag = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/chats/messages/{Uri.EscapeDataString(messageId)}", new EditChatMessageRequest(body, encVersion, commitmentTag), AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.ChatMessageDto, token, null, onFailure);
    }

    public Task<bool> DeleteMessageAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/chats/messages/{Uri.EscapeDataString(messageId)}", token, null, onFailure);
    }

    public Task<bool> DeleteConversationAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/chats/{Uri.EscapeDataString(conversationId)}", token, null, onFailure);
    }

    public Task<bool> MuteConversationAsync(string conversationId, bool muted, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/mute", new MuteConversationRequest(muted), AethernetJsonContext.Default.MuteConversationRequest, token, null, onFailure);
    }

    public Task<ConversationDetailDto?> AddMembersAsync(string conversationId, string[] memberIds, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/chats/{Uri.EscapeDataString(conversationId)}/members", new AddMembersRequest(memberIds), AethernetJsonContext.Default.AddMembersRequest, AethernetJsonContext.Default.ConversationDetailDto, token, null, onFailure);
    }

    public Task<bool> RemoveMemberAsync(string conversationId, string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/chats/{Uri.EscapeDataString(conversationId)}/members/{Uri.EscapeDataString(userId)}", token, null, onFailure);
    }

    public Task<ConversationDetailDto?> UpdateConversationAsync(string conversationId, UpdateConversationRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/chats/{Uri.EscapeDataString(conversationId)}", request, AethernetJsonContext.Default.UpdateConversationRequest, AethernetJsonContext.Default.ConversationDetailDto, token, null, onFailure);
    }

    public Task<ConversationDetailDto?> SetMemberRoleAsync(string conversationId, string userId, int role,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/members/{Uri.EscapeDataString(userId)}/role", new SetMemberRoleRequest(role), AethernetJsonContext.Default.SetMemberRoleRequest, AethernetJsonContext.Default.ConversationDetailDto, token, null, onFailure);
    }

    public Task<bool> SendTypingAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/typing", token, null, onFailure);
    }

    public Task<ChatTypingDto?> TypingAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}/typing", AethernetJsonContext.Default.ChatTypingDto, token, null, onFailure);
    }

    public Task<ChatMediaUrlDto?> DmMediaUrlAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/chats/media/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.ChatMediaUrlDto, token, null, onFailure);
    }
}
