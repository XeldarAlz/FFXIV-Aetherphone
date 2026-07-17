using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class DevClient
{
    private readonly AethernetTransport net;

    public DevClient(AethernetTransport net)
    {
        this.net = net;
    }

    public async Task<bool?> AccessAsync(CancellationToken token)
    {
        var status = 0;
        var granted = await net.SendAsync(HttpMethod.Get, "/devspace/access", token, statusCode => status = statusCode).ConfigureAwait(false);
        if (granted)
        {
            return true;
        }

        return status == 404 ? false : null;
    }

    public Task<DevBoardCards?> BoardCardsAsync(CancellationToken token, Action<int>? onStatus = null)
    {
        return net.GetAsync("/devspace/board/cards", AethernetJsonContext.Default.DevBoardCards, token, onStatus);
    }

    public Task<DevBoardCardDto?> CreateCardAsync(string title, string body, CancellationToken token)
    {
        return net.PostAsync("/devspace/board/cards", new CreateDevCardRequest(title, body), AethernetJsonContext.Default.CreateDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, token);
    }

    public Task<DevBoardCardDto?> UpdateCardAsync(string cardId, string? title, string? body, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/devspace/board/cards/{Uri.EscapeDataString(cardId)}", new UpdateDevCardRequest(title, body), AethernetJsonContext.Default.UpdateDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, token);
    }

    public Task<DevBoardCardDto?> MoveCardAsync(string cardId, int status, string? beforeId, CancellationToken token)
    {
        return net.PostAsync($"/devspace/board/cards/{Uri.EscapeDataString(cardId)}/move", new MoveDevCardRequest(status, beforeId), AethernetJsonContext.Default.MoveDevCardRequest, AethernetJsonContext.Default.DevBoardCardDto, token);
    }

    public Task<bool> DeleteCardAsync(string cardId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/devspace/board/cards/{Uri.EscapeDataString(cardId)}", token);
    }

    public Task<DevChatPage?> ChatMessagesAsync(long afterUnix, string? cursor, CancellationToken token, Action<int>? onStatus = null)
    {
        var path = "/devspace/chat/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }
        else if (afterUnix > 0)
        {
            path += $"?afterUnix={afterUnix}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.DevChatPage, token, onStatus);
    }

    public Task<DevChatMessageDto?> SendChatMessageAsync(string body, string? mediaKey, int mediaWidth, int mediaHeight, CancellationToken token)
    {
        return net.PostAsync("/devspace/chat/messages", new SendDevChatMessageRequest(body, mediaKey, mediaWidth, mediaHeight), AethernetJsonContext.Default.SendDevChatMessageRequest, AethernetJsonContext.Default.DevChatMessageDto, token);
    }

    public Task<DevMediaUrlDto?> ChatMediaUrlAsync(string messageId, CancellationToken token)
    {
        return net.GetAsync($"/devspace/chat/media/{Uri.EscapeDataString(messageId)}/url", AethernetJsonContext.Default.DevMediaUrlDto, token);
    }

    public Task<bool> DeleteChatMessageAsync(string messageId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/devspace/chat/messages/{Uri.EscapeDataString(messageId)}", token);
    }
}
