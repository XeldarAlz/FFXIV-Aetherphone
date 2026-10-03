using Aetherphone.Core.Maps;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal readonly record struct JamLocation(uint TerritoryId, uint WorldId);

internal sealed partial class JamSession
{
    private readonly JamNearbyCadence nearbyCadence = new();
    private JamNearbyJam[] nearbyJams = Array.Empty<JamNearbyJam>();
    private long nearbyInterestAt;
    private bool nearbyWanted;

    public bool Discoverable { get; private set; }
    public ReadOnlySpan<JamNearbyJam> NearbyJams => nearbyJams;
    public int NearbyVersion { get; private set; }

    public void WantNearby()
    {
        nearbyInterestAt = Environment.TickCount64;
    }

    public void SetDiscoverable(bool discoverable)
    {
        if (Mode != JamMode.Hosting)
        {
            return;
        }

        signals.Settings(null, null, null, discoverable);
        nearbyCadence.Reset();
    }

    private void TickNearby(long now)
    {
        if (!JamNearbyCadence.Wanted(now, nearbyInterestAt, IsHost && Discoverable))
        {
            if (nearbyWanted)
            {
                nearbyWanted = false;
                nearbyCadence.Reset();
                SetNearby(Array.Empty<JamNearbyJam>());
            }

            return;
        }

        nearbyWanted = true;
        if (!nearbyCadence.MayReport(now) || !signals.Connected || session.CurrentUser is null
            || disconnectedSinceTicks != 0)
        {
            return;
        }

        var location = locate();
        if (!nearbyCadence.ShouldReport(now, location.TerritoryId, location.WorldId))
        {
            return;
        }

        signals.Nearby(location.TerritoryId, location.WorldId);
        nearbyCadence.MarkSent(now, location.TerritoryId, location.WorldId);
    }

    private void OnNearbyRoster(CallControl message)
    {
        if (nearbyWanted)
        {
            SetNearby(JamWire.ToNearby(message.NearbyJams, jamId));
        }
    }

    private void ApplyDiscoverable(bool? discoverable)
    {
        var next = discoverable ?? Discoverable;
        if (next == Discoverable)
        {
            return;
        }

        Discoverable = next;
        nearbyCadence.Reset();
    }

    private void SetNearby(JamNearbyJam[] jams)
    {
        if (jams.Length == 0 && nearbyJams.Length == 0)
        {
            return;
        }

        nearbyJams = jams;
        NearbyVersion++;
    }

    private static JamLocation LocateLocalPlayer()
    {
        return new JamLocation(Plugin.ClientState.TerritoryType, LocationShare.CurrentWorldId());
    }
}
