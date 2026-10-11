namespace Aetherphone.Apps.Casino.Stage;

internal sealed class RealityCheck
{
    public const int RoundsPerCheck = 100;
    public const long MillisecondsPerCheck = 30L * 60L * 1000L;

    private long sessionStartMs = -1;
    private long lastCheckMs;
    private int roundsSinceCheck;

    public int Rounds { get; private set; }

    public long Net { get; private set; }

    public bool Due { get; private set; }

    public long SessionMilliseconds(long nowMs) => sessionStartMs < 0 ? 0 : nowMs - sessionStartMs;

    public void Tick(long nowMs)
    {
        if (sessionStartMs < 0)
        {
            sessionStartMs = nowMs;
            lastCheckMs = nowMs;
            return;
        }

        if (!Due && roundsSinceCheck > 0 && nowMs - lastCheckMs >= MillisecondsPerCheck)
        {
            Due = true;
        }
    }

    public void Record(long stake, long payout, long nowMs)
    {
        if (sessionStartMs < 0)
        {
            Tick(nowMs);
        }

        Rounds++;
        roundsSinceCheck++;
        Net += payout - stake;
        if (roundsSinceCheck >= RoundsPerCheck || nowMs - lastCheckMs >= MillisecondsPerCheck)
        {
            Due = true;
        }
    }

    public void Acknowledge(long nowMs)
    {
        Due = false;
        roundsSinceCheck = 0;
        lastCheckMs = nowMs;
    }

    public void Reset()
    {
        sessionStartMs = -1;
        lastCheckMs = 0;
        roundsSinceCheck = 0;
        Rounds = 0;
        Net = 0;
        Due = false;
    }
}
