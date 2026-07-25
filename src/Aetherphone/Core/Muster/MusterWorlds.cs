using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Muster;

/// <summary>World sheet lookups for muster reachability: home-world names arrive as strings on the wire,
/// while data-center scoping and party invites need row ids. Read on the framework thread only.</summary>
internal static class MusterWorlds
{
    private static Dictionary<string, (ushort WorldId, int DataCenterId)>? byName;

    public static uint CurrentWorldId()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return 0;
        }

        var player = Plugin.ObjectTable.LocalPlayer;
        return player?.CurrentWorld.RowId ?? 0;
    }

    public static int DataCenterIdForWorld(uint worldId)
    {
        if (worldId == 0 || !Plugin.DataManager.GetExcelSheet<World>().TryGetRow(worldId, out var world))
        {
            return 0;
        }

        return (int)world.DataCenter.RowId;
    }

    public static int CurrentDataCenterId()
    {
        return DataCenterIdForWorld(CurrentWorldId());
    }

    public static bool TryResolve(string worldName, out ushort worldId, out int dataCenterId)
    {
        worldId = 0;
        dataCenterId = 0;
        if (worldName.Length == 0)
        {
            return false;
        }

        var lookup = byName ??= BuildLookup();
        if (!lookup.TryGetValue(worldName, out var entry))
        {
            return false;
        }

        worldId = entry.WorldId;
        dataCenterId = entry.DataCenterId;
        return true;
    }

    private static Dictionary<string, (ushort WorldId, int DataCenterId)> BuildLookup()
    {
        var lookup = new Dictionary<string, (ushort, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            var name = world.Name.ExtractText();
            if (name.Length == 0 || world.DataCenter.RowId == 0)
            {
                continue;
            }

            lookup[name] = ((ushort)world.RowId, (int)world.DataCenter.RowId);
        }

        return lookup;
    }
}
