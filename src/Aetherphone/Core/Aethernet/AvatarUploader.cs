using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Aethernet;

internal static class AvatarUploader
{
    public static void Upload(AccountClient account, MediaClient media, AethernetSession session, string sourcePath,
        WallpaperCrop crop, CancellationToken token, Action<AvatarUploadOutcome> onComplete)
    {
        _ = Task.Run(async () =>
        {
            var outcome = AvatarUploadOutcome.Unreachable;
            try
            {
                var result = await AvatarUpload.RunAsync(account, media, sourcePath, crop, token)
                    .ConfigureAwait(false);
                outcome = result.Outcome;
                if (result.User is { } user)
                {
                    session.SetUser(user);
                }
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
                onComplete(outcome);
            }
        });
    }
}
