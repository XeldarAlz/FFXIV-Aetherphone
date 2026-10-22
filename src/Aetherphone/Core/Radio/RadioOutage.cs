namespace Aetherphone.Core.Radio;

internal struct RadioOutage
{
    public const long StopAfterMilliseconds = 15L * 60 * 1000;

    private bool lost;
    private long lostSinceTick;

    public void Observe(RadioPlaybackState state, long nowTick)
    {
        if (!IsLost(state))
        {
            lost = false;
            return;
        }

        if (lost)
        {
            return;
        }

        lost = true;
        lostSinceTick = nowTick;
    }

    public readonly bool Expired(long nowTick) => lost && nowTick - lostSinceTick >= StopAfterMilliseconds;

    public static bool IsLost(RadioPlaybackState state) =>
        state is RadioPlaybackState.Reconnecting or RadioPlaybackState.Failed;
}
