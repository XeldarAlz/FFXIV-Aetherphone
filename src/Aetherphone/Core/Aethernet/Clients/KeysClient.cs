using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class KeysClient
{
    private readonly AethernetTransport net;

    public KeysClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<MyKeysDto?> PutMyKeysAsync(PutMyKeysRequest request, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/keys/me", request, AethernetJsonContext.Default.PutMyKeysRequest, AethernetJsonContext.Default.MyKeysDto, token);
    }

    public async Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token)
    {
        var status = 0;
        var keys = await net.GetAsync("/keys/me", AethernetJsonContext.Default.MyKeysDto, token, statusCode => status = statusCode).ConfigureAwait(false);
        return (keys, status);
    }

    public Task<PublicKeysDto?> PublicKeysAsync(string[] userIds, CancellationToken token)
    {
        return net.PostAsync("/keys/users", new PublicKeysRequest(userIds), AethernetJsonContext.Default.PublicKeysRequest, AethernetJsonContext.Default.PublicKeysDto, token);
    }

    public Task<MyConversationKeysDto?> MyConversationKeysAsync(CancellationToken token)
    {
        return net.GetAsync("/keys/conversations", AethernetJsonContext.Default.MyConversationKeysDto, token);
    }

    public Task<ConversationKeysDto?> ConversationKeysAsync(string conversationId, CancellationToken token)
    {
        return net.GetAsync($"/chats/{Uri.EscapeDataString(conversationId)}/keys", AethernetJsonContext.Default.ConversationKeysDto, token);
    }

    public async Task<(bool Ok, int Status)> CreateConversationGenerationAsync(string conversationId, CreateGenerationRequest request, CancellationToken token)
    {
        var status = 0;
        var ok = await net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/keys", request, AethernetJsonContext.Default.CreateGenerationRequest, token, statusCode => status = statusCode).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddConversationWrapsAsync(string conversationId, AddWrapsRequest request, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/chats/{Uri.EscapeDataString(conversationId)}/keys/wraps", request, AethernetJsonContext.Default.AddWrapsRequest, token);
    }

    public Task<MyConversationKeysDto?> VelvetKeysAsync(CancellationToken token)
    {
        return net.GetAsync("/velvet/keys", AethernetJsonContext.Default.MyConversationKeysDto, token);
    }

    public Task<ConversationKeysDto?> VelvetThreadKeysAsync(string otherId, CancellationToken token)
    {
        return net.GetAsync($"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys", AethernetJsonContext.Default.ConversationKeysDto, token);
    }

    public async Task<(bool Ok, int Status)> CreateVelvetGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token)
    {
        var status = 0;
        var ok = await net.SendJsonForStatusAsync(HttpMethod.Post, $"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys", request, AethernetJsonContext.Default.CreateGenerationRequest, token, statusCode => status = statusCode).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddVelvetWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/velvet/threads/{Uri.EscapeDataString(otherId)}/keys/wraps", request, AethernetJsonContext.Default.AddWrapsRequest, token);
    }

    public Task<MyConversationKeysDto?> GramKeysAsync(CancellationToken token)
    {
        return net.GetAsync("/gram/keys", AethernetJsonContext.Default.MyConversationKeysDto, token);
    }

    public Task<ConversationKeysDto?> GramThreadKeysAsync(string otherId, CancellationToken token)
    {
        return net.GetAsync($"/gram/threads/{Uri.EscapeDataString(otherId)}/keys", AethernetJsonContext.Default.ConversationKeysDto, token);
    }

    public async Task<(bool Ok, int Status)> CreateGramGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token)
    {
        var status = 0;
        var ok = await net.SendJsonForStatusAsync(HttpMethod.Post, $"/gram/threads/{Uri.EscapeDataString(otherId)}/keys", request, AethernetJsonContext.Default.CreateGenerationRequest, token, statusCode => status = statusCode).ConfigureAwait(false);
        return (ok, status);
    }

    public Task<bool> AddGramWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/gram/threads/{Uri.EscapeDataString(otherId)}/keys/wraps", request, AethernetJsonContext.Default.AddWrapsRequest, token);
    }
}
