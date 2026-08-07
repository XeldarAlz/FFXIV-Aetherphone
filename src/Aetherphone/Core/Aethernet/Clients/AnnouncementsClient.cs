using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AnnouncementsClient
{
    private readonly AethernetTransport net;

    public AnnouncementsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<AnnouncementPage?> ListAsync(string? cursor, CancellationToken token)
    {
        var path = "/announcements";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AnnouncementPage, token);
    }
}
