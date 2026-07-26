using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AnnouncementsClient
{
    private readonly AethernetTransport net;

    public AnnouncementsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<AnnouncementPage?> ListAsync(CancellationToken token)
    {
        return net.GetAsync("/announcements", AethernetJsonContext.Default.AnnouncementPage, token);
    }
}
