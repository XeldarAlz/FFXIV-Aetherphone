using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Aethernet;

internal enum AvatarUploadOutcome : byte
{
    Uploaded,
    Unreachable,
    Rejected,
}

internal readonly struct AvatarUploadResult
{
    public readonly AvatarUploadOutcome Outcome;
    public readonly UserDto? User;
    public readonly string PublicUrl;

    public AvatarUploadResult(AvatarUploadOutcome outcome, UserDto? user, string publicUrl)
    {
        Outcome = outcome;
        User = user;
        PublicUrl = publicUrl;
    }

    public bool Ok => Outcome == AvatarUploadOutcome.Uploaded;

    public static readonly AvatarUploadResult Unreachable =
        new(AvatarUploadOutcome.Unreachable, null, string.Empty);

    public static readonly AvatarUploadResult Rejected =
        new(AvatarUploadOutcome.Rejected, null, string.Empty);
}

internal static class AvatarUpload
{
    public const int Size = 512;

    public static LocString Message(AvatarUploadOutcome outcome)
    {
        return outcome == AvatarUploadOutcome.Rejected ? L.Account.PhotoRejected : L.Account.CannotReach;
    }

    public static async Task<AvatarUploadResult> RunAsync(AccountClient account, MediaClient media, string sourcePath,
        WallpaperCrop crop, CancellationToken token)
    {
        var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, Size);
        var upload = await media.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
        if (upload is null)
        {
            return AvatarUploadResult.Unreachable;
        }

        if (!await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token).ConfigureAwait(false))
        {
            return AvatarUploadResult.Unreachable;
        }

        var status = 0;
        var updated = await account
            .UpdateProfileAsync(new UpdateProfileRequest(null, null, null, upload.PublicUrl), token,
                code => status = code)
            .ConfigureAwait(false);
        if (updated is not null)
        {
            return new AvatarUploadResult(AvatarUploadOutcome.Uploaded, updated, upload.PublicUrl);
        }

        return status is >= 400 and < 500 ? AvatarUploadResult.Rejected : AvatarUploadResult.Unreachable;
    }
}
