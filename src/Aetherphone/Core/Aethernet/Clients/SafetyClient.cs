using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Report;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class SafetyClient
{
    private readonly AethernetTransport net;

    public SafetyClient(AethernetTransport net, BlockList blocks)
    {
        this.net = net;
        Blocks = blocks;
    }

    public BlockList Blocks { get; }

    public Task<bool> ReportAsync(string targetType, string targetId, ReportReason reason, CancellationToken token,
        RevealedMessageDto[]? revealedMessages = null, Action<AepFailure>? onFailure = null)
    {
        var request = new ReportRequest(targetType, targetId, reason.Details, revealedMessages, reason.Category);
        return net.SendJsonForStatusAsync(HttpMethod.Post, "/reports", request, AethernetJsonContext.Default.ReportRequest, token, null, onFailure);
    }

    public async Task<bool> BlockAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var blocked = await net.SendAsync(HttpMethod.Post, $"/blocks/{Uri.EscapeDataString(userId)}", token, null, onFailure)
            .ConfigureAwait(false);
        if (blocked)
        {
            Blocks.Add(userId);
        }

        return blocked;
    }

    public async Task<bool> UnblockAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var unblocked = await net.SendAsync(HttpMethod.Delete, $"/blocks/{Uri.EscapeDataString(userId)}", token, null, onFailure)
            .ConfigureAwait(false);
        if (unblocked)
        {
            Blocks.Remove(userId);
        }

        return unblocked;
    }

    public async Task<UserSearchResult?> BlockedUsersAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var epoch = Blocks.Epoch;
        var page = await net.GetAsync("/blocks/", AethernetJsonContext.Default.UserSearchResult, token, null, onFailure)
            .ConfigureAwait(false);
        if (page is not null)
        {
            var users = page.Users;
            var ids = new string[users.Length];
            for (var index = 0; index < users.Length; index++)
            {
                ids[index] = users[index].Id;
            }

            Blocks.Replace(ids, epoch, DateTime.UtcNow);
        }

        return page;
    }
}
