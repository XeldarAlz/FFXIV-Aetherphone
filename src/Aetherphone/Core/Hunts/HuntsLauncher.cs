namespace Aetherphone.Core.Hunts;

internal sealed class HuntsLauncher
{
    private string? pendingMobId;
    private string? pendingWorldId;
    private int pendingZoneInstance;

    public void RequestDetail(string mobId, string worldId, int zoneInstance)
    {
        pendingMobId = mobId;
        pendingWorldId = worldId;
        pendingZoneInstance = zoneInstance;
    }

    public bool TryConsumeDetail(out string mobId, out string worldId, out int zoneInstance)
    {
        if (pendingMobId is null || pendingWorldId is null)
        {
            mobId = string.Empty;
            worldId = string.Empty;
            zoneInstance = 0;
            return false;
        }

        mobId = pendingMobId;
        worldId = pendingWorldId;
        zoneInstance = pendingZoneInstance;
        pendingMobId = null;
        pendingWorldId = null;
        pendingZoneInstance = 0;
        return true;
    }
}
