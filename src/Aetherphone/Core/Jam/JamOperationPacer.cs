namespace Aetherphone.Core.Jam;

internal sealed class JamOperationPacer
{
    // The server allows 8 queue operations per fixed 4 s window and silently drops the rest. A sliding
    // window of 7 over 4.25 s can never put more than 7 into any server window, leaving a slot of slack.
    internal const int MaxPerWindow = 7;
    internal const long WindowMilliseconds = 4_250;

    private readonly long[] sentAt = new long[MaxPerWindow];
    private int nextSlot;
    private int used;

    internal bool TryAcquire(long nowMilliseconds)
    {
        if (used == MaxPerWindow && nowMilliseconds - sentAt[nextSlot] < WindowMilliseconds)
        {
            return false;
        }

        sentAt[nextSlot] = nowMilliseconds;
        nextSlot = (nextSlot + 1) % MaxPerWindow;
        used = Math.Min(used + 1, MaxPerWindow);
        return true;
    }

    internal void Reset()
    {
        Array.Clear(sentAt);
        nextSlot = 0;
        used = 0;
    }
}
