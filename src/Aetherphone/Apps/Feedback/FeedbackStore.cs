using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackStore : IDisposable
{
    private const int MaxImageDimension = 1600;

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();

    private volatile bool posting;

    public bool IsSignedIn => session.IsSignedIn;
    public bool Posting => posting;
    public UserDto? Me => session.CurrentUser;

    public FeedbackStore(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
    }

    public void Compose(string text, IReadOnlyList<string> imagePaths, Action<bool> onComplete)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var keys = await UploadImagesAsync(imagePaths, token).ConfigureAwait(false);
                if (keys is not null)
                {
                    var created = await client.CreateFeedbackAsync(trimmed, keys, token).ConfigureAwait(false);
                    succeeded = created is not null;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Feedback] compose failed: {exception.Message}");
            }
            finally
            {
                posting = false;
                onComplete(succeeded);
            }
        });
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
            var upload = await client.UploadUrlAsync("image/jpeg", "feedback", token).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
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
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
