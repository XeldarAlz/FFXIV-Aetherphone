using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Game;

namespace Aetherphone.Core.Social;

internal static class RegionSync
{
    public static void Push(AethernetSession session, AccountClient client, GameData gameData,
        CancellationToken token)
    {
        var user = session.CurrentUser;
        if (!session.IsSignedIn || user is null)
        {
            return;
        }

        var region = SocialRegion.EffectiveCode(session, gameData);
        if (region.Length == 0 || string.Equals(user.Region, region, StringComparison.Ordinal))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await client.UpdateRegionAsync(region, token).ConfigureAwait(false);
                if (updated is not null)
                {
                    session.SetUser(updated);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Region update failed");
            }
        });
    }
}
