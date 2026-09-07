using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetStore
{
    public void EnsureMe()
    {
        ReconcileAccountBadges();
        if (!session.IsSignedIn || me is not null || loadingMe)
        {
            return;
        }

        if (!meGate.TryPass())
        {
            return;
        }

        loadingMe = true;
        var epoch = accountEpoch;
        work.Run("profile load", async token =>
        {
            var status = 0;
            var refusal = AepFailure.None;
            var profile = await client.MeAsync(token, code => status = code, failure => refusal = failure)
                .ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            if (profile is not null)
            {
                me = profile;
                accessBlocked = false;
                regionBlocked = false;
            }
            else if (status == 403)
            {
                accessBlocked = true;
                regionBlocked = refusal.Code == FailureCodes.VelvetRegionBlocked;
            }
        }, () => loadingMe = false);
    }

    private void ReconcileAccountBadges()
    {
        var current = me;
        var signedInUser = session.CurrentUser;
        if (current is null || signedInUser is null || current.Badges == signedInUser.Badges)
        {
            return;
        }

        me = current with { Badges = signedInUser.Badges };
    }

    public void AcceptGate(int gateVersion, Action<bool> onComplete)
    {
        work.Run("gate accept", async token =>
        {
            var profile = await client.AcceptGateAsync(gateVersion, token).ConfigureAwait(false);
            if (profile is null)
            {
                return false;
            }

            me = profile;
            return true;
        }, onComplete);
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        work.Run("avatar update", async token =>
        {
            var result = await AvatarUpload.RunAsync(account, media, sourcePath, crop, token).ConfigureAwait(false);
            avatarFailure = result.Outcome;
            if (!result.Ok)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { AvatarUrl = result.PublicUrl };
            }

            return true;
        }, onComplete, () => avatarBusy = false);
    }

    public void AddCardPhoto(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (cardPhotoBusy)
        {
            return;
        }

        cardPhotoBusy = true;
        work.Run("card photo add", async token =>
        {
            var baked = ImageProcessor.BakeCroppedJpeg(sourcePath, crop, CardPhotoWidth, CardPhotoHeight);
            var upload = await media.UploadUrlAsync("image/jpeg", "velvet", token).ConfigureAwait(false);
            if (upload is null)
            {
                cardPhotoFailure = AvatarUploadOutcome.Unreachable;
                return false;
            }

            if (!await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token).ConfigureAwait(false))
            {
                cardPhotoFailure = AvatarUploadOutcome.Unreachable;
                return false;
            }

            var status = 0;
            var updated = await client.AddCardPhotoAsync(new AddVelvetCardPhotoRequest(upload.Key, CardPhotoWidth,
                CardPhotoHeight), token, code => status = code).ConfigureAwait(false);
            if (updated is null)
            {
                cardPhotoFailure = status is >= 400 and < 500
                    ? AvatarUploadOutcome.Rejected
                    : AvatarUploadOutcome.Unreachable;
                return false;
            }

            me = updated;
            return true;
        }, onComplete, () => cardPhotoBusy = false);
    }

    public void RemoveCardPhoto(string photoId)
    {
        if (me is { Photos: { } photos } current)
        {
            me = current with { Photos = WithoutPhoto(photos, photoId) };
        }

        work.Run("card photo remove", async token =>
        {
            var updated = await client.RemoveCardPhotoAsync(photoId, token).ConfigureAwait(false);
            if (updated is not null)
            {
                me = updated;
            }
        });
    }

    public void MakeCardPhotoCover(string photoId)
    {
        if (me is not { Photos: { Length: > 1 } photos } current)
        {
            return;
        }

        var order = CoverFirst(photos, photoId);
        if (order is null)
        {
            return;
        }

        var reordered = new VelvetCardPhotoDto[order.Length];
        for (var index = 0; index < order.Length; index++)
        {
            reordered[index] = Array.Find(photos, photo => photo.Id == order[index])!;
        }

        me = current with { Photos = reordered };
        work.Run("card photo cover", async token =>
        {
            var updated = await client.ReorderCardPhotosAsync(order, token).ConfigureAwait(false);
            if (updated is not null)
            {
                me = updated;
            }
        });
    }

    private static VelvetCardPhotoDto[] WithoutPhoto(VelvetCardPhotoDto[] photos, string photoId)
    {
        var kept = new List<VelvetCardPhotoDto>(photos.Length);
        for (var index = 0; index < photos.Length; index++)
        {
            if (photos[index].Id != photoId)
            {
                kept.Add(photos[index]);
            }
        }

        return kept.Count == photos.Length ? photos : kept.ToArray();
    }

    private static string[]? CoverFirst(VelvetCardPhotoDto[] photos, string photoId)
    {
        var order = new string[photos.Length];
        var cursor = 1;
        var found = false;
        for (var index = 0; index < photos.Length; index++)
        {
            if (photos[index].Id == photoId)
            {
                order[0] = photoId;
                found = true;
                continue;
            }

            if (cursor < order.Length)
            {
                order[cursor] = photos[index].Id;
            }

            cursor++;
        }

        return found ? order : null;
    }

    public void UpdateIdentity(string displayName, string handle, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        work.Run("identity update", async token =>
        {
            var request = new UpdateProfileRequest(displayName.Length > 0 ? displayName : null,
                handle.Length > 0 ? handle : null, null);
            var updated = await account.UpdateProfileAsync(request, token, null, onFailure).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { DisplayName = updated.DisplayName, Handle = updated.Handle };
            }

            return true;
        }, onComplete);
    }

    public void UpdateProfile(UpdateVelvetProfileRequest request, Action<bool> onComplete)
    {
        work.Run("profile update", async token =>
        {
            var updated = await client.UpdateProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            me = updated;
            return true;
        }, onComplete);
    }
}
