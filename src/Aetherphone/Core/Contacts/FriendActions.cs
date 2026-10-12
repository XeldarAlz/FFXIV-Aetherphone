using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Contacts;

internal static unsafe class FriendActions
{
    public static void OpenAdventurerPlate(ulong contentId)
    {
        if (!GameMemory.Attached)
        {
            GameMemory.SimulateAction($"open adventurer plate of friend {contentId}");
            return;
        }

        var agent = AgentCharaCard.Instance();
        if (agent != null)
        {
            agent->OpenCharaCard(contentId);
        }
    }

    public static void InviteToParty(ulong contentId, ushort worldId)
    {
        if (!GameMemory.Attached)
        {
            GameMemory.SimulateAction($"invite friend {contentId} on world {worldId} to the party");
            return;
        }

        var invite = InfoProxyPartyInvite.Instance();
        if (invite != null)
        {
            invite->InviteToPartyContentId(contentId, worldId);
        }
    }

    public static void VisitEstate(ulong contentId)
    {
        if (!GameMemory.Attached)
        {
            GameMemory.SimulateAction($"visit the estate of friend {contentId}");
            return;
        }

        var agent = AgentFriendlist.Instance();
        if (agent != null)
        {
            agent->OpenFriendEstateTeleportation(contentId);
        }
    }

    public static void OpenSearchInfo(ulong contentId)
    {
        if (!GameMemory.Attached)
        {
            GameMemory.SimulateAction($"open search info of friend {contentId}");
            return;
        }

        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return;
        }

        var count = proxy->EntryCount;
        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry != null && entry->ContentId == contentId)
            {
                AgentDetail.Instance()->OpenForCharacterData(entry, null, 0);
                return;
            }
        }
    }
}
