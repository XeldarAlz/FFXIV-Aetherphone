using Aetherphone.Core.GameChat;
using Aetherphone.Core.Muster;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Aetherphone.Core.Game;

internal readonly record struct PlayerActionAvailability(bool Invite, bool FriendRequest, bool Blacklist,
    bool AdventurerPlate, bool Target)
{
    public static readonly PlayerActionAvailability None = default;
}

internal static unsafe class PlayerActions
{
    private const string FriendRequestCommand = "/friendlist add <t>";
    private const string BlacklistCommand = "/blacklist add <t>";

    public static PlayerActionAvailability Resolve(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return PlayerActionAvailability.None;
        }

        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return PlayerActionAvailability.None;
        }

        try
        {
            if (!Plugin.ClientState.IsLoggedIn)
            {
                return PlayerActionAvailability.None;
            }

            MusterWorlds.TryResolve(worldName, out var worldId, out var dataCenterId);
            if (IsLocalPlayer(playerName, worldId))
            {
                return PlayerActionAvailability.None;
            }

            var nearby = FindNearby(playerName, worldId);
            var targetable = nearby is not null && nearby.IsTargetable && TargetSystem.Instance() != null;
            var invite = InfoProxyPartyInvite.Instance() != null && (nearby is not null
                ? worldId != 0 || nearby.HomeWorld.RowId != 0u
                : PartyInvite.IsSameDataCenter(dataCenterId));
            var plate = AgentCharaCard.Instance() != null &&
                        (nearby is not null || FriendContentId(playerName, worldId) != 0ul);
            return new PlayerActionAvailability(invite, targetable, targetable, plate, targetable);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] availability failed");
            return PlayerActionAvailability.None;
        }
    }

    public static bool InviteToParty(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var worldId = WorldId(worldName);
            if (worldId == 0)
            {
                var nearby = FindNearby(playerName, 0);
                worldId = nearby is not null ? (ushort)nearby.HomeWorld.RowId : (ushort)0;
            }

            return PartyInvite.Invite(playerName, worldId);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] party invite failed");
            return false;
        }
    }

    public static bool SendFriendRequest(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        return SendTargetedCommand(FriendRequestCommand, name, world, "[PlayerActions] friend request failed");
    }

    public static bool AddToBlacklist(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        return SendTargetedCommand(BlacklistCommand, name, world, "[PlayerActions] blacklist failed");
    }

    public static bool OpenAdventurerPlate(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var agent = AgentCharaCard.Instance();
            if (agent == null)
            {
                return false;
            }

            var worldId = WorldId(worldName);
            var nearby = FindNearby(playerName, worldId);
            if (nearby is not null)
            {
                agent->OpenCharaCard((GameObjectStruct*)nearby.Address);
                return true;
            }

            var contentId = FriendContentId(playerName, worldId);
            if (contentId == 0ul)
            {
                return false;
            }

            agent->OpenCharaCard(contentId);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] adventurer plate failed");
            return false;
        }
    }

    public static bool TargetPlayer(string name, string world)
    {
        if (!GameMemory.Attached)
        {
            return false;
        }

        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var nearby = FindNearby(playerName, WorldId(worldName));
            if (nearby is null || !nearby.IsTargetable)
            {
                return false;
            }

            var targetSystem = TargetSystem.Instance();
            if (targetSystem == null)
            {
                return false;
            }

            targetSystem->Target = (GameObjectStruct*)nearby.Address;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] target failed");
            return false;
        }
    }

    private static bool SendTargetedCommand(string command, string name, string world, string failureMessage)
    {
        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var nearby = FindNearby(playerName, WorldId(worldName));
            if (nearby is null || !nearby.IsTargetable)
            {
                return false;
            }

            var targetSystem = TargetSystem.Instance();
            if (targetSystem == null)
            {
                return false;
            }

            var previous = targetSystem->Target;
            targetSystem->Target = (GameObjectStruct*)nearby.Address;
            var sent = ChatSender.TrySend(command);
            targetSystem->Target = previous;
            if (!sent)
            {
                AepLog.Warning(failureMessage);
            }

            return sent;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, failureMessage);
            return false;
        }
    }

    private static ushort WorldId(string worldName)
    {
        return MusterWorlds.TryResolve(worldName, out var worldId, out _) ? worldId : (ushort)0;
    }

    private static bool IsLocalPlayer(string name, ushort worldId)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local is null || !string.Equals(local.Name.TextValue, name, StringComparison.Ordinal))
        {
            return false;
        }

        return worldId == 0 || local.HomeWorld.RowId == worldId;
    }

    private static IPlayerCharacter? FindNearby(string name, ushort worldId)
    {
        var table = Plugin.ObjectTable;
        for (var index = 0; index < table.Length; index++)
        {
            if (table[index] is not IPlayerCharacter player)
            {
                continue;
            }

            if (!string.Equals(player.Name.TextValue, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (worldId != 0 && player.HomeWorld.RowId != worldId)
            {
                continue;
            }

            return player;
        }

        return null;
    }

    private static ulong FriendContentId(string name, ushort worldId)
    {
        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return 0ul;
        }

        var count = proxy->EntryCount;
        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry == null || !string.Equals(entry->NameString, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (worldId != 0 && entry->HomeWorld != worldId)
            {
                continue;
            }

            return entry->ContentId;
        }

        return 0ul;
    }
}
