using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class KeysClient
{
    private readonly AethernetTransport net;

    public KeysClient(AethernetTransport net)
    {
        this.net = net;
    }

    public async Task<(MyKeysDto? Keys, int Status)> PutMyKeysAsync(PutMyKeysRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var status = 0;
        var keys = await net.SendJsonAsync(HttpMethod.Put, "/keys/me", request, AethernetJsonContext.Default.PutMyKeysRequest, AethernetJsonContext.Default.MyKeysDto, token, statusCode => status = statusCode, onFailure).ConfigureAwait(false);
        return (keys, status);
    }

    public async Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var status = 0;
        var keys = await net.GetAsync("/keys/me", AethernetJsonContext.Default.MyKeysDto, token, statusCode => status = statusCode, onFailure).ConfigureAwait(false);
        return (keys, status);
    }

    public Task<ArchivedEscrowsDto?> MyKeyEscrowsAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/keys/me/escrows", AethernetJsonContext.Default.ArchivedEscrowsDto, token, null, onFailure);
    }

    public Task<DeviceLinkTicketDto?> StartDeviceLinkAsync(string ephemeralPublicKey, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/keys/link/requests", new StartDeviceLinkRequest(ephemeralPublicKey),
            AethernetJsonContext.Default.StartDeviceLinkRequest, AethernetJsonContext.Default.DeviceLinkTicketDto,
            token, null, onFailure);
    }

    public Task<PendingDeviceLinksDto?> PendingDeviceLinksAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/keys/link/requests", AethernetJsonContext.Default.PendingDeviceLinksDto, token, null,
            onFailure);
    }

    public Task<DeviceLinkStatusDto?> DeviceLinkStatusAsync(string id, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/keys/link/requests/{Uri.EscapeDataString(id)}",
            AethernetJsonContext.Default.DeviceLinkStatusDto, token, null, onFailure);
    }

    public Task<bool> ApproveDeviceLinkAsync(string id, string wrappedIdentityKey, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post,
            $"/keys/link/requests/{Uri.EscapeDataString(id)}/approve", new ApproveDeviceLinkRequest(wrappedIdentityKey),
            AethernetJsonContext.Default.ApproveDeviceLinkRequest, token, null, onFailure);
    }

    public Task<bool> CancelDeviceLinkAsync(string id, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/keys/link/requests/{Uri.EscapeDataString(id)}", token,
            null, onFailure);
    }

    public Task<PublicKeysDto?> PublicKeysAsync(string[] userIds, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/keys/users", new PublicKeysRequest(userIds), AethernetJsonContext.Default.PublicKeysRequest, AethernetJsonContext.Default.PublicKeysDto, token, null, onFailure);
    }

    public Task<MyConversationKeysDto?> MyConversationKeysAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/keys/conversations", AethernetJsonContext.Default.MyConversationKeysDto, token, null, onFailure);
    }

    public Task<ConversationKeysDto?> ConversationKeysAsync(string conversationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}/keys", AethernetJsonContext.Default.ConversationKeysDto, token, null, onFailure);
    }

    public async Task<(bool Ok, int Status)> CreateConversationGenerationAsync(string conversationId, CreateGenerationRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var status = 0;
        var ok = await net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/keys", request, AethernetJsonContext.Default.CreateGenerationRequest, token, statusCode => status = statusCode, onFailure).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddConversationWrapsAsync(string conversationId, AddWrapsRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/keys/wraps", request, AethernetJsonContext.Default.AddWrapsRequest, token, null, onFailure);
    }

    public Task<MyConversationKeysDto?> PairKeysAsync(string surface, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/{surface}/keys", AethernetJsonContext.Default.MyConversationKeysDto, token, null,
            onFailure);
    }

    public Task<ConversationKeysDto?> PairThreadKeysAsync(string surface, string otherId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/{surface}/threads/{Uri.EscapeDataString(otherId)}/keys",
            AethernetJsonContext.Default.ConversationKeysDto, token, null, onFailure);
    }

    public async Task<(bool Ok, int Status)> CreatePairGenerationAsync(string surface, string otherId,
        CreateGenerationRequest request, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var status = 0;
        var ok = await net.SendJsonForStatusAsync(HttpMethod.Post,
            $"/{surface}/threads/{Uri.EscapeDataString(otherId)}/keys", request,
            AethernetJsonContext.Default.CreateGenerationRequest, token, statusCode => status = statusCode, onFailure)
            .ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddPairWrapsAsync(string surface, string otherId, AddWrapsRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post,
            $"/{surface}/threads/{Uri.EscapeDataString(otherId)}/keys/wraps", request,
            AethernetJsonContext.Default.AddWrapsRequest, token, null, onFailure);
    }
}
