using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Aethernet;

internal static class BannerUpload
{
    public const int Width = 1500;
    public const int Height = 500;
    public const float Aspect = (float)Width / Height;

    public static async Task<AvatarUploadResult> RunAsync(AccountClient account, MediaClient media, string sourcePath,
        WallpaperCrop crop, CancellationToken token)
    {
        var baked = ImageProcessor.BakeCropped(sourcePath, crop, Width, Height);
        var upload = await media.UploadUrlAsync(baked.ContentType, "banner", token).ConfigureAwait(false);
        if (upload is null)
        {
            return AvatarUploadResult.Unreachable;
        }

        if (!await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, baked.ContentType, token).ConfigureAwait(false))
        {
            return AvatarUploadResult.Unreachable;
        }

        var status = 0;
        var updated = await account
            .UpdateProfileAsync(new UpdateProfileRequest(null, null, null, null, upload.PublicUrl), token,
                code => status = code)
            .ConfigureAwait(false);
        if (updated is not null)
        {
            return new AvatarUploadResult(AvatarUploadOutcome.Uploaded, updated, upload.PublicUrl);
        }

        return status is >= 400 and < 500 ? AvatarUploadResult.Rejected : AvatarUploadResult.Unreachable;
    }
}
