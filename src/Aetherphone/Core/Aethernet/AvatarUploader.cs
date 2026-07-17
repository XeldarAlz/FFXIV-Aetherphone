using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Aethernet;

internal static class AvatarUploader
{
    public const int Size = 512;

    public static void Upload(AccountClient account, MediaClient media, AethernetSession session, string sourcePath,
        WallpaperCrop crop, CancellationToken token, Action<bool> onComplete)
    {
        _ = Task.Run(async () =>
        {
            var uploaded = false;
            try
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, Size);
                var upload = await media.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return;
                }

                if (!await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                        .ConfigureAwait(false))
                {
                    return;
                }

                var updated = await account
                    .UpdateProfileAsync(new UpdateProfileRequest(null, null, null, upload.PublicUrl), token)
                    .ConfigureAwait(false);
                if (updated is null)
                {
                    return;
                }

                session.SetUser(updated);
                uploaded = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet avatar update failed: {exception.Message}");
            }
            finally
            {
                onComplete(uploaded);
            }
        });
    }
}
