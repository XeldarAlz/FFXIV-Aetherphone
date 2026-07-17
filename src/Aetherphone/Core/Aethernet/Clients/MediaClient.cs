using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class MediaClient
{
    private readonly AethernetTransport net;

    public MediaClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<UploadUrlResponse?> UploadUrlAsync(string contentType, string scope, CancellationToken token)
    {
        return net.PostAsync("/media/upload-url", new UploadUrlRequest(contentType, scope), AethernetJsonContext.Default.UploadUrlRequest, AethernetJsonContext.Default.UploadUrlResponse, token);
    }

    public Task<bool> UploadImageAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken token)
    {
        return net.PutBytesAsync(new Uri(uploadUrl), bytes, contentType, token);
    }
}
