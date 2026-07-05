using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Contacts;

internal static unsafe class FriendListReader
{
    public static bool RequestServerData()
    {
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
            into.Add(new FriendEntry(entry->ContentId, entry->NameString, gameData.WorldName(entry->HomeWorld),
                entry->FCTagString, online ? gameData.JobAbbreviation(entry->Job) : string.Empty,
                online ? gameData.JobName(entry->Job) : string.Empty,
                online ? gameData.TerritoryName(entry->Location) : string.Empty, online, entry->HomeWorld,
                entry->CurrentWorld));
        }
    }
}
