namespace Aetherphone.Core.Aethernet;

internal enum NetworkNotice : byte
{
    None,
    Restarting,
    Lost,
    BackOnline,
}

internal sealed class NetworkOutageTracker
{
    public const long LostAfterMilliseconds = 20_000;
    public const long StableAfterMilliseconds = 3_000;
    public const long NoticeGapMilliseconds = 60_000;
    public const long RestartWithoutDropMilliseconds = 120_000;

    private bool everOnline;
    private bool online;
    private long changedAt;
    private bool restartPending;
    private bool awaitingDrop;
    private long restartAt;
    private bool outageAnnounced;
    private long lastNoticeAt = -NoticeGapMilliseconds;

    public bool Online => online;

    public void Connected(long now)
    {
        if (online)
        {
            return;
        }

        online = true;
        everOnline = true;
        changedAt = now;
    }

    public void Disconnected(long now)
    {
        if (!online)
        {
            return;
        }

        online = false;
        changedAt = now;
        awaitingDrop = false;
    }

    public void RestartAnnounced(long now)
    {
        if (!everOnline || outageAnnounced || restartPending)
        {
            return;
        }

        restartPending = true;
        awaitingDrop = online;
        restartAt = now;
    }

    public void Reset()
    {
        everOnline = false;
        online = false;
        restartPending = false;
        awaitingDrop = false;
        outageAnnounced = false;
        lastNoticeAt = -NoticeGapMilliseconds;
    }

    public NetworkNotice Tick(long now)
    {
        if (restartPending)
        {
            restartPending = false;
            outageAnnounced = true;
            lastNoticeAt = now;
            return NetworkNotice.Restarting;
        }

        if (!everOnline)
        {
            return NetworkNotice.None;
        }

        return online ? TickOnline(now) : TickOffline(now);
    }

    private NetworkNotice TickOnline(long now)
    {
        if (!outageAnnounced)
        {
            return NetworkNotice.None;
        }

        if (awaitingDrop)
        {
            if (now - restartAt >= RestartWithoutDropMilliseconds)
            {
                awaitingDrop = false;
                outageAnnounced = false;
            }

            return NetworkNotice.None;
        }

        if (now - changedAt < StableAfterMilliseconds)
        {
            return NetworkNotice.None;
        }

        outageAnnounced = false;
        lastNoticeAt = now;
        return NetworkNotice.BackOnline;
    }

    private NetworkNotice TickOffline(long now)
    {
        if (outageAnnounced || now - changedAt < LostAfterMilliseconds || now - lastNoticeAt < NoticeGapMilliseconds)
        {
            return NetworkNotice.None;
        }

        outageAnnounced = true;
        lastNoticeAt = now;
        return NetworkNotice.Lost;
    }
}
