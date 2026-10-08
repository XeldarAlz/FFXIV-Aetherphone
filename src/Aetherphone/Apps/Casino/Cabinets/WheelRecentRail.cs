using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class WheelRecentRail
{
    public const int Capacity = 12;

    private readonly int[] spots = new int[Capacity];
    private readonly int[] merged = new int[Capacity];

    private int count;
    private int landed = -1;

    public int Count => landed >= 0 ? Math.Min(Capacity, count + 1) : count;

    public bool HasLanded => landed >= 0;

    public int SpotAt(int index)
    {
        if (landed < 0)
        {
            return spots[index];
        }

        return index == 0 ? landed : spots[index - 1];
    }

    public void Reset()
    {
        count = 0;
        landed = -1;
    }

    public void Land(int spot)
    {
        if (WheelRules.IsSpot(spot))
        {
            landed = spot;
        }
    }

    public void Sync(int[]? server)
    {
        if (server is null || Matches(server, 0))
        {
            return;
        }

        landed = -1;
        var shift = count == 0 ? -1 : ShiftOf(server);
        if (shift < 0)
        {
            Replace(server);
            return;
        }

        var written = 0;
        for (var index = 0; index < shift && written < Capacity; index++)
        {
            if (WheelRules.IsSpot(server[index]))
            {
                merged[written] = server[index];
                written++;
            }
        }

        for (var index = 0; index < count && written < Capacity; index++)
        {
            merged[written] = spots[index];
            written++;
        }

        Array.Copy(merged, spots, written);
        count = written;
    }

    private int ShiftOf(int[] server)
    {
        for (var shift = 1; shift < server.Length; shift++)
        {
            if (Matches(server, shift))
            {
                return shift;
            }
        }

        return -1;
    }

    private bool Matches(int[] server, int shift)
    {
        var overlap = Math.Min(count, server.Length - shift);
        if (overlap <= 0)
        {
            return count == 0 && server.Length == shift;
        }

        for (var index = 0; index < overlap; index++)
        {
            if (server[shift + index] != spots[index])
            {
                return false;
            }
        }

        return true;
    }

    private void Replace(int[] server)
    {
        count = 0;
        for (var index = 0; index < server.Length && count < Capacity; index++)
        {
            if (WheelRules.IsSpot(server[index]))
            {
                spots[count] = server[index];
                count++;
            }
        }
    }
}
