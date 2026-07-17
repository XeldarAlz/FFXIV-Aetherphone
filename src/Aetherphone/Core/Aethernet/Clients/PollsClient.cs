using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class PollsClient
{
    private readonly AethernetTransport net;

    public PollsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<PollPage?> ListAsync(CancellationToken token)
    {
        return net.GetAsync("/polls", AethernetJsonContext.Default.PollPage, token);
    }

    public Task<PollDto?> VoteAsync(string pollId, int option, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/polls/{pollId}/vote", new PollVoteRequest(option), AethernetJsonContext.Default.PollVoteRequest, AethernetJsonContext.Default.PollDto, token);
    }

    public Task<PollDto?> ClearVoteAsync(string pollId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/polls/{pollId}/vote", AethernetJsonContext.Default.PollDto, token);
    }
}
