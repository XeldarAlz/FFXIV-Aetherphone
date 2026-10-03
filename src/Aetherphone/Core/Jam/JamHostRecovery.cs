namespace Aetherphone.Core.Jam;

internal enum JamAdvanceStep : byte
{
    Wait,
    Retry,
    PlayPopped,
    GiveUp,
}

internal enum JamQueueWaitStep : byte
{
    Wait,
    Advance,
    Stop,
}

internal static class JamHostRecovery
{
    public const long AdvanceTimeoutMilliseconds = 6_000;
    public const long QueueWaitLimitMilliseconds = 15_000;

    // Covers the server round trip for a queue add that already left the pacer.
    public const long AddEchoMilliseconds = 4_000;

    public static JamAdvanceStep Advance(long elapsedMilliseconds, bool retried, bool popped)
    {
        if (elapsedMilliseconds <= AdvanceTimeoutMilliseconds)
        {
            return JamAdvanceStep.Wait;
        }

        if (popped)
        {
            return JamAdvanceStep.PlayPopped;
        }

        return retried ? JamAdvanceStep.GiveUp : JamAdvanceStep.Retry;
    }

    public static JamQueueWaitStep QueueWait(int queueCount, bool addInFlight, long elapsedMilliseconds)
    {
        if (queueCount > 0)
        {
            return JamQueueWaitStep.Advance;
        }

        if (!addInFlight || elapsedMilliseconds > QueueWaitLimitMilliseconds)
        {
            return JamQueueWaitStep.Stop;
        }

        return JamQueueWaitStep.Wait;
    }

    public static bool AddInFlight(bool addQueued, long lastAddSentAt, long nowMilliseconds)
    {
        return addQueued || (lastAddSentAt != 0 && nowMilliseconds - lastAddSentAt < AddEchoMilliseconds);
    }
}
