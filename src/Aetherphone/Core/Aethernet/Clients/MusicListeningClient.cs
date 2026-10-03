using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class MusicListeningClient
{
    public const string ListeningPath = "/music/listening";
    public const string FriendsPath = "/music/listening/friends";

    private readonly AethernetTransport net;

    public MusicListeningClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<bool> PublishAsync(ListeningUpdateRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Put, ListeningPath, request,
            AethernetJsonContext.Default.ListeningUpdateRequest, token, null, onFailure);
    }

    public Task<bool> ClearAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, ListeningPath, token, null, onFailure);
    }

    public Task<ListeningFriendsPage?> FriendsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(FriendsPath, AethernetJsonContext.Default.ListeningFriendsPage, token, null, onFailure);
    }
}
