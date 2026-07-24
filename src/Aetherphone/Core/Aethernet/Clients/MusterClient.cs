using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class MusterClient
{
    private readonly AethernetTransport net;

    public MusterClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<MusterDto?> CreateAsync(CreateMusterRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        return net.PostAsync("/musters/", request, AethernetJsonContext.Default.CreateMusterRequest,
            AethernetJsonContext.Default.MusterDto, token, statusSink);
    }

    public Task<bool> EndAsync(string musterId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/musters/{Uri.EscapeDataString(musterId)}/end", token);
    }

    public Task<MusterRsvpResult?> RsvpAsync(string musterId, bool going, CancellationToken token)
    {
        return net.PostAsync($"/musters/{Uri.EscapeDataString(musterId)}/rsvp", new SetMusterRsvpRequest(going),
            AethernetJsonContext.Default.SetMusterRsvpRequest, AethernetJsonContext.Default.MusterRsvpResult, token);
    }

    public Task<MusterPage?> DirectoryAsync(int categories, int regions, string? cursor, CancellationToken token)
    {
        var path = $"/musters/?categories={categories}&regions={regions}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.MusterPage, token);
    }

    public Task<MusterSync?> SyncAsync(CancellationToken token)
    {
        return net.GetAsync("/musters/sync", AethernetJsonContext.Default.MusterSync, token);
    }

    public Task<MusterDto?> GetAsync(string musterId, CancellationToken token)
    {
        return net.GetAsync($"/musters/{Uri.EscapeDataString(musterId)}", AethernetJsonContext.Default.MusterDto, token);
    }
}
