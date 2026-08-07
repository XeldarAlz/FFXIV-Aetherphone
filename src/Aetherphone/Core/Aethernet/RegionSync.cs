using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Game;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.Aethernet;

internal static class RegionSync
{
    public static void Push(AethernetSession session, AccountClient client, Configuration configuration,
        GameData gameData, CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var region = SocialRegion.EffectiveCode(configuration, gameData);
        if (region.Length == 0 || string.Equals(session.CurrentUser?.Region, region, StringComparison.Ordinal))
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
                AepLog.Warning($"Region update failed: {exception.Message}");
            }
        });
    }
}
