namespace Aetherphone.Core.Jam;

internal sealed class JamNearbyCadence
{
    // Same cadence the AetherStream party list uses for stream.nearby while it is on screen.
    public const long IntervalMilliseconds = 5_000;
    public const long MinimumGapMilliseconds = 1_000;
    public const long InterestMilliseconds = 1_500;

    private bool sent;
    private long sentAt;
    private uint territory;
    private uint world;

    public static bool Wanted(long nowMilliseconds, long interestAt, bool hostingDiscoverable)
    {
        return hostingDiscoverable || (interestAt != 0 && nowMilliseconds - interestAt <= InterestMilliseconds);
    }

    public bool MayReport(long nowMilliseconds)
    {
        return !sent || nowMilliseconds - sentAt >= MinimumGapMilliseconds;
    }

    public bool ShouldReport(long nowMilliseconds, uint territoryId, uint worldId)
    {
        if (territoryId == 0 || worldId == 0)
        {
            return false;
        }

        if (!sent)
        {
            return true;
        }

        var elapsed = nowMilliseconds - sentAt;
        if (elapsed >= IntervalMilliseconds)
        {
            return true;
        }

        return elapsed >= MinimumGapMilliseconds && (territoryId != territory || worldId != world);
    }

    public void MarkSent(long nowMilliseconds, uint territoryId, uint worldId)
    {
        sent = true;
        sentAt = nowMilliseconds;
        territory = territoryId;
        world = worldId;
    }

    public void Reset()
    {
        sent = false;
        sentAt = 0;
        territory = 0;
        world = 0;
    }
}
