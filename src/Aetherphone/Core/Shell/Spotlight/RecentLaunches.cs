namespace Aetherphone.Core.Shell.Spotlight;

internal sealed class RecentLaunches
{
    public const int Capacity = 6;

    private readonly string[] slots = new string[Capacity];
    private int count;

    public RecentLaunches()
    {
        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = string.Empty;
        }
    }

    public int Count => count;

    public string this[int index] => index >= 0 && index < count ? slots[index] : string.Empty;

    public void Note(string appId)
    {
        if (string.IsNullOrEmpty(appId))
        {
            return;
        }

        var existing = IndexOf(appId);
        if (existing == 0)
        {
            return;
        }

        var shiftFrom = existing > 0 ? existing : Math.Min(count, Capacity - 1);
        for (var slot = shiftFrom; slot > 0; slot--)
        {
            slots[slot] = slots[slot - 1];
        }

        slots[0] = appId;
        if (existing < 0 && count < Capacity)
        {
            count++;
        }
    }

    private int IndexOf(string appId)
    {
        for (var slot = 0; slot < count; slot++)
        {
            if (string.Equals(slots[slot], appId, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return -1;
    }
}
