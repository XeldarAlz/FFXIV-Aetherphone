using Aetherphone.Core.Muster;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Game;

internal static unsafe class PartyInvite
{
    public static bool IsSameDataCenter(int dataCenterId)
    {
        if (dataCenterId == 0)
        {
            return false;
        }

        var current = MusterWorlds.CurrentDataCenterId();
        return current != 0 && current == dataCenterId;
    }

    public static bool CanInvite(string worldName)
    {
        return MusterWorlds.TryResolve(worldName, out _, out var dataCenterId) && IsSameDataCenter(dataCenterId);
    }

    public static bool Invite(string characterName, string worldName)
    {
        return MusterWorlds.TryResolve(worldName, out var worldId, out _) && Invite(characterName, worldId);
    }

    public static bool Invite(string characterName, ushort worldId)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        if (characterName.Length == 0 || worldId == 0)
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
