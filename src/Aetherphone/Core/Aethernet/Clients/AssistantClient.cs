using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AssistantClient
{
    private readonly AethernetTransport net;

    public AssistantClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<AssistantStatusResponse?> StatusAsync(CancellationToken token)
    {
        return net.GetAsync("/assistant/status", AethernetJsonContext.Default.AssistantStatusResponse, token);
    }

    public Task<AssistantAskResponse?> AskAsync(AssistantAskRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        return net.PostAsync("/assistant/ask", request, AethernetJsonContext.Default.AssistantAskRequest, AethernetJsonContext.Default.AssistantAskResponse, token, statusSink);
    }
}
