using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Muster;

/// <summary>Cross-world party invites by character name and home world. The game only routes invites
/// inside one data center, so callers gate on <see cref="CanInvite"/> first. Framework thread only.</summary>
internal static unsafe class MusterPartyInvite
{
    public static bool CanInvite(string worldName)
    {
        if (!MusterWorlds.TryResolve(worldName, out _, out var dataCenterId))
        {
            return false;
        }

        var myDataCenterId = MusterWorlds.CurrentDataCenterId();
        return myDataCenterId != 0 && myDataCenterId == dataCenterId;
    }

    public static bool Invite(string characterName, string worldName)
    {
        if (characterName.Length == 0 || !CanInvite(worldName)
            || !MusterWorlds.TryResolve(worldName, out var worldId, out _))
        {
            return false;
        }

        var proxy = InfoProxyPartyInvite.Instance();
        if (proxy == null)
        {
            return false;
        }

        proxy->InviteToParty(0, characterName, worldId);
        return true;
    }
}
