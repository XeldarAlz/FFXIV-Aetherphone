namespace Aetherphone.Core.Radio;

internal static class RadioRoomPacing
{
    private const long JitterMarginMilliseconds = 250;

    public const int ChatBurst = 3;

    // Server buckets live in Aethernet Realtime/Connection.cs (chat refill 1500, reaction 250,
    // request 2000, attach 1000); the client waits a jitter margin longer so a send it allows is
    // never refused by latency variance.
    public const long ChatRefillMilliseconds = 1500 + JitterMarginMilliseconds;
    public const long ReactionMilliseconds = 250 + JitterMarginMilliseconds;
    public const long RequestMilliseconds = 2000 + JitterMarginMilliseconds;
    public const long AttachMilliseconds = 1000 + JitterMarginMilliseconds;
}

internal sealed class RadioChatPacer
{
    private int tokens = RadioRoomPacing.ChatBurst;
    private long budgetTick;

    public int Tokens => tokens;

    public bool CanSend(long nowTick)
    {
        Refill(nowTick);
        return tokens > 0;
    }

    public void Take(long nowTick)
    {
        Refill(nowTick);
        if (tokens == 0)
        {
            return;
        }

        if (tokens == RadioRoomPacing.ChatBurst)
        {
            budgetTick = nowTick;
        }

        tokens--;
    }

    public void Reset()
    {
        tokens = RadioRoomPacing.ChatBurst;
        budgetTick = 0;
    }

    private void Refill(long nowTick)
    {
        if (tokens >= RadioRoomPacing.ChatBurst)
        {
            return;
        }

        var refilled = (nowTick - budgetTick) / RadioRoomPacing.ChatRefillMilliseconds;
        if (refilled <= 0)
        {
            return;
        }

        tokens = (int)Math.Min(RadioRoomPacing.ChatBurst, tokens + refilled);
        budgetTick += refilled * RadioRoomPacing.ChatRefillMilliseconds;
    }
}

internal sealed class RadioCooldownGate
{
    private readonly long intervalMilliseconds;
    private long lastTick;
    private bool used;

    public RadioCooldownGate(long intervalMilliseconds)
    {
        this.intervalMilliseconds = intervalMilliseconds;
    }

    public bool IsOpen(long nowTick)
    {
        return !used || nowTick - lastTick >= intervalMilliseconds;
    }

    public long OpensAt => used ? lastTick + intervalMilliseconds : 0;

    public void Take(long nowTick)
    {
        lastTick = nowTick;
        used = true;
    }

    public void Reset()
    {
        used = false;
        lastTick = 0;
    }
}
