namespace Aetherphone.Core.Radio;

internal sealed class RadioLauncher
{
    private string? pendingStationId;

    public void RequestStation(string stationId)
    {
        pendingStationId = stationId;
    }

    public bool TryConsumeStation(out string stationId)
    {
        if (pendingStationId is null)
        {
            stationId = string.Empty;
            return false;
        }

        stationId = pendingStationId;
        pendingStationId = null;
        return true;
    }
}
