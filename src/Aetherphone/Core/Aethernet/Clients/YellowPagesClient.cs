using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class YellowPagesClient
{
    private readonly AethernetTransport net;

    public YellowPagesClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<AdDto?> CreateAsync(CreateAdRequest request, CancellationToken token, Action<int>? statusSink = null)
    {
        return net.PostAsync("/ads/", request, AethernetJsonContext.Default.CreateAdRequest,
            AethernetJsonContext.Default.AdDto, token, statusSink);
    }

    public Task<AdDto?> UpdateAsync(string adId, CreateAdRequest request, CancellationToken token,
        Action<int>? statusSink = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/ads/{Uri.EscapeDataString(adId)}", request,
            AethernetJsonContext.Default.CreateAdRequest, AethernetJsonContext.Default.AdDto, token, statusSink);
    }

    public Task<bool> DeleteAsync(string adId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/ads/{Uri.EscapeDataString(adId)}", token);
    }

    public Task<AdDto?> RenewAsync(string adId, CancellationToken token, Action<int>? statusSink = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/ads/{Uri.EscapeDataString(adId)}/renew",
            AethernetJsonContext.Default.AdDto, token, statusSink);
    }

    public Task<SetAdOpenResult?> OpenAsync(string adId, bool open, int minutes, CancellationToken token)
    {
        return net.PostAsync($"/ads/{Uri.EscapeDataString(adId)}/open", new SetAdOpenRequest(open, minutes),
            AethernetJsonContext.Default.SetAdOpenRequest, AethernetJsonContext.Default.SetAdOpenResult, token);
    }

    public Task<bool> SaveAsync(string adId, bool saved, CancellationToken token)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/ads/{Uri.EscapeDataString(adId)}/save",
            new SetAdSavedRequest(saved), AethernetJsonContext.Default.SetAdSavedRequest, token);
    }

    public Task<AdPage?> DirectoryAsync(int categories, int regions, int dataCenterId, bool openNow, bool afterDark,
        string? search, string? cursor, CancellationToken token)
    {
        var path = $"/ads/?categories={categories}&regions={regions}&dc={dataCenterId}"
            + $"&openNow={(openNow ? "true" : "false")}&afterDark={(afterDark ? "true" : "false")}";
        if (!string.IsNullOrEmpty(search))
        {
            path += $"&search={Uri.EscapeDataString(search)}";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdPage, token);
    }

    public Task<AdPage?> MineAsync(CancellationToken token)
    {
        return net.GetAsync("/ads/mine", AethernetJsonContext.Default.AdPage, token);
    }

    public Task<AdPage?> SavedAsync(string? cursor, CancellationToken token)
    {
        var path = "/ads/saved";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdPage, token);
    }

    public Task<AdDto?> GetAsync(string adId, CancellationToken token)
    {
        return net.GetAsync($"/ads/{Uri.EscapeDataString(adId)}", AethernetJsonContext.Default.AdDto, token);
    }
}
