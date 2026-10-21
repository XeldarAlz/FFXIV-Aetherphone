using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Campaigns;

internal sealed class CampaignClient
{
    private readonly AethernetTransport net;

    public CampaignClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CampaignDto?> GetAsync(string slug, CancellationToken token, Action<int>? onStatus = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(PathFor(slug), AethernetJsonContext.Default.CampaignDto, token, onStatus, onFailure);
    }

    public Task<CampaignPostPage?> PostsAsync(string slug, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = PathFor(slug) + "/posts";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CampaignPostPage, token, null, onFailure);
    }

    public Task<CampaignMeDto?> MeAsync(string slug, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(PathFor(slug) + "/me", AethernetJsonContext.Default.CampaignMeDto, token, null, onFailure);
    }

    private static string PathFor(string slug) => $"/campaigns/{Uri.EscapeDataString(slug)}";
}
