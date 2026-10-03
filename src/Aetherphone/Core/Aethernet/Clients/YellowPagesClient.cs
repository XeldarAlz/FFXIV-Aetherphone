using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class YellowPagesClient
{
    private readonly AethernetTransport net;

    public YellowPagesClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<AdDto?> CreateAsync(CreateAdRequest request, CancellationToken token, Action<int>? statusSink = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/ads/", request, AethernetJsonContext.Default.CreateAdRequest,
            AethernetJsonContext.Default.AdDto, token, statusSink, onFailure);
    }

    public Task<AdDto?> UpdateAsync(string adId, CreateAdRequest request, CancellationToken token,
        Action<int>? statusSink = null, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/ads/{Uri.EscapeDataString(adId)}", request,
            AethernetJsonContext.Default.CreateAdRequest, AethernetJsonContext.Default.AdDto, token, statusSink,
            onFailure);
    }

    public Task<bool> DeleteAsync(string adId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/ads/{Uri.EscapeDataString(adId)}", token, null, onFailure);
    }

    public Task<AdDto?> RenewAsync(string adId, CancellationToken token, Action<int>? statusSink = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/ads/{Uri.EscapeDataString(adId)}/renew",
            AethernetJsonContext.Default.AdDto, token, statusSink, onFailure);
    }

    public Task<SetAdOpenResult?> OpenAsync(string adId, bool open, int minutes, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/ads/{Uri.EscapeDataString(adId)}/open", new SetAdOpenRequest(open, minutes),
            AethernetJsonContext.Default.SetAdOpenRequest, AethernetJsonContext.Default.SetAdOpenResult, token, null,
            onFailure);
    }

    public Task<bool> SaveAsync(string adId, bool saved, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post, $"/ads/{Uri.EscapeDataString(adId)}/save",
            new SetAdSavedRequest(saved), AethernetJsonContext.Default.SetAdSavedRequest, token, null, onFailure);
    }

    public Task<AdPage?> DirectoryAsync(int categories, int regions, int dataCenterId, bool openNow, bool afterDark,
        int direction, string? search, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/ads/?categories={categories}&regions={regions}&dc={dataCenterId}"
            + $"&openNow={(openNow ? "true" : "false")}&afterDark={(afterDark ? "true" : "false")}"
            + $"&direction={direction}";
        if (!string.IsNullOrEmpty(search))
        {
            path += $"&search={Uri.EscapeDataString(search)}";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdPage, token, null, onFailure);
    }

    public Task<AdPage?> MineAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/ads/mine", AethernetJsonContext.Default.AdPage, token, null, onFailure);
    }

    public Task<AdPage?> SavedAsync(string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = "/ads/saved";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdPage, token, null, onFailure);
    }

    public Task<AdDto?> GetAsync(string adId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/ads/{Uri.EscapeDataString(adId)}", AethernetJsonContext.Default.AdDto, token, null,
            onFailure);
    }

    public Task<AdInquiryDto?> OpenInquiryAsync(string adId, SendAdInquiryRequest request, CancellationToken token,
        Action<int>? statusSink = null, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/ads/{Uri.EscapeDataString(adId)}/inquiries", request,
            AethernetJsonContext.Default.SendAdInquiryRequest, AethernetJsonContext.Default.AdInquiryDto, token,
            statusSink, onFailure);
    }

    public Task<AdInquiryPage?> InquiriesAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/ads/inquiries";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdInquiryPage, token, null, onFailure);
    }

    public Task<AdInquiryMessagePage?> InquiryMessagesAsync(string inquiryId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}/messages";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.AdInquiryMessagePage, token, null, onFailure);
    }

    public Task<AdInquiryMessageDto?> SendInquiryAsync(string inquiryId, SendAdInquiryRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync($"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}/messages", request,
            AethernetJsonContext.Default.SendAdInquiryRequest,
            AethernetJsonContext.Default.AdInquiryMessageDto, token, null, onFailure);
    }

    public Task<AdInquiryMessageDto?> EditInquiryMessageAsync(string messageId, string body, int encVersion,
        string? commitmentTag, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, $"/ads/inquiries/messages/{Uri.EscapeDataString(messageId)}",
            new EditChatMessageRequest(body, encVersion, commitmentTag),
            AethernetJsonContext.Default.EditChatMessageRequest, AethernetJsonContext.Default.AdInquiryMessageDto,
            token, null, onFailure);
    }

    public Task<bool> DeleteInquiryMessageAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/ads/inquiries/messages/{Uri.EscapeDataString(messageId)}", token,
            null, onFailure);
    }

    public Task<bool> ClearInquiryAsync(string inquiryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}", token, null,
            onFailure);
    }

    public Task<bool> SetInquiryReactionAsync(string messageId, string reactionToken, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonForStatusAsync(HttpMethod.Post,
            $"/ads/inquiries/messages/{Uri.EscapeDataString(messageId)}/reactions",
            new SetReactionRequest(reactionToken), AethernetJsonContext.Default.SetReactionRequest, token, null,
            onFailure);
    }

    public Task<ReactionListDto?> InquiryReactionsAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/ads/inquiries/messages/{Uri.EscapeDataString(messageId)}/reactions",
            AethernetJsonContext.Default.ReactionListDto, token, null, onFailure);
    }

    public Task<bool> SendInquiryTypingAsync(string inquiryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}/typing", token,
            null, onFailure);
    }

    public Task<AdInquiryTypingDto?> InquiryTypingAsync(string inquiryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}/typing",
            AethernetJsonContext.Default.AdInquiryTypingDto, token, null, onFailure);
    }

    public Task<AdInquiryMediaUrlDto?> InquiryMediaUrlAsync(string messageId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/ads/inquiries/media/{Uri.EscapeDataString(messageId)}/url",
            AethernetJsonContext.Default.AdInquiryMediaUrlDto, token, null, onFailure);
    }

    public Task<bool> MarkInquiryReadAsync(string inquiryId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/ads/inquiries/{Uri.EscapeDataString(inquiryId)}/read", token, null,
            onFailure);
    }
}
