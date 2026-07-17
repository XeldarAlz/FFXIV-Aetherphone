using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class SafetyClient
{
    private readonly AethernetTransport net;

    public SafetyClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<bool> ReportAsync(string targetType, string targetId, string? reason, CancellationToken token, RevealedMessageDto[]? revealedMessages = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, "/reports", new ReportRequest(targetType, targetId, reason, revealedMessages), AethernetJsonContext.Default.ReportRequest, token);
    }

    public Task<bool> BlockAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/blocks/{Uri.EscapeDataString(userId)}", token);
    }

    public Task<bool> UnblockAsync(string userId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/blocks/{Uri.EscapeDataString(userId)}", token);
    }

    public Task<UserSearchResult?> BlockedUsersAsync(CancellationToken token)
    {
        return net.GetAsync("/blocks/", AethernetJsonContext.Default.UserSearchResult, token);
    }
}
