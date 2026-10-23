using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Contacts;

internal static unsafe class FriendListReader
{
    public static bool RequestServerData()
    {
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is not null;
        }

        var gameMain = GameMain.Instance();
        if (gameMain == null || gameMain->CurrentContentFinderConditionId != 0)
        {
            return false;
        }

        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return false;
        }

        return proxy->RequestData();
    }

    public static void Read(List<FriendEntry> into, GameData gameData)
    {
        if (!GameMemory.Attached)
        {
            ReadSimulated(into, gameData);
            return;
        }

        into.Clear();
        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return;
        }

        var count = proxy->EntryCount;
        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry == null)
            {
                continue;
            }

            var online = entry->State.HasFlag(InfoProxyCommonList.CharacterData.OnlineStatus.Online);
            into.Add(Entry(gameData, entry->ContentId, entry->NameString, entry->FCTagString, entry->Job,
                entry->Location, online, entry->HomeWorld, entry->CurrentWorld));
        }
    }

    private static void ReadSimulated(List<FriendEntry> into, GameData gameData)
    {
        if (GameMemory.Simulated is not { } simulated)
        {
            return;
        }

        into.Clear();
        for (var index = 0; index < simulated.Friends.Length; index++)
        {
            var friend = simulated.Friends[index];
            into.Add(Entry(gameData, friend.ContentId, friend.Name, friend.FreeCompanyTag, friend.ClassJobId,
                friend.TerritoryId, friend.Online, friend.HomeWorldId, friend.CurrentWorldId));
        }
    }

    private static FriendEntry Entry(GameData gameData, ulong contentId, string name, string freeCompanyTag,
        byte classJobId, ushort territoryId, bool online, ushort homeWorldId, ushort currentWorldId) =>
        new(contentId, name, gameData.WorldName(homeWorldId), freeCompanyTag,
            online ? gameData.JobAbbreviation(classJobId) : string.Empty,
            online ? gameData.JobName(classJobId) : string.Empty,
            online ? gameData.TerritoryName(territoryId) : string.Empty, online, homeWorldId, currentWorldId);
}
