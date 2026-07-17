using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackStore : IDisposable
{
    private const int MaxImageDimension = 1600;

    private readonly AethernetSession session;
    private readonly FeedbackClient client;
    private readonly MediaClient media;
    private readonly StoreWork work = new StoreWork("Feedback");

    private volatile bool posting;

    public bool IsSignedIn => session.IsSignedIn;
    public bool Posting => posting;
    public UserDto? Me => session.CurrentUser;

    public FeedbackStore(AethernetSession session, FeedbackClient client, MediaClient media)
    {
        this.session = session;
        this.client = client;
        this.media = media;
    }

    public void Compose(string text, IReadOnlyList<string> imagePaths, Action<bool> onComplete)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        work.Run("compose", async token =>
        {
            var keys = await UploadImagesAsync(imagePaths, token).ConfigureAwait(false);
            if (keys is null)
            {
                return false;
            }

            var created = await client.CreateAsync(trimmed, keys, token).ConfigureAwait(false);
            return created is not null;
        }, onComplete, () => posting = false);
    }

    private async Task<string[]?> UploadImagesAsync(IReadOnlyList<string> imagePaths, CancellationToken token)
    {
        if (imagePaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var keys = new string[imagePaths.Count];
        for (var index = 0; index < imagePaths.Count; index++)
        {
            var baked = ImageProcessor.BakeJpeg(imagePaths[index], MaxImageDimension);
            var upload = await media.UploadUrlAsync("image/jpeg", "feedback", token).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return null;
            }

            keys[index] = upload.Key;
        }

        return keys;
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
