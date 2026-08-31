using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Radio;

internal static class StationArtworkUpload
{
    public const int Size = 512;

    public static async Task<bool> RunAsync(MediaClient media, CommunityRadioService community,
        UpdateCommunityStationRequest current, string sourcePath, WallpaperCrop crop, CancellationToken token)
    {
        try
        {
            var baked = ImageProcessor.BakeSquare(sourcePath, crop, Size);
            var upload = await media.UploadUrlAsync(baked.ContentType, "radio", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            if (!await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, baked.ContentType, token)
                    .ConfigureAwait(false))
            {
                return false;
            }

            return await community.SaveMineAsync(current with { MediaKey = upload.Key }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] artwork upload failed");
            return false;
        }
    }
}
