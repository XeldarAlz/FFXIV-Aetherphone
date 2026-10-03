using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class FeedbackClient
{
    private readonly AethernetTransport net;

    public FeedbackClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<FeedbackDto?> CreateAsync(string text, string[] imageKeys, string category, string context,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/feedback", new CreateFeedbackRequest(text, imageKeys, category, context),
            AethernetJsonContext.Default.CreateFeedbackRequest, AethernetJsonContext.Default.FeedbackDto, token, null,
            onFailure);
    }

    public Task<MyFeedbackPage?> MineAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = cursor is null ? "/feedback/mine" : $"/feedback/mine?cursor={Uri.EscapeDataString(cursor)}";
        return net.GetAsync(path, AethernetJsonContext.Default.MyFeedbackPage, token, null, onFailure);
    }
}
