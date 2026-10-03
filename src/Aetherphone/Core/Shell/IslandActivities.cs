using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Shell;

internal enum IslandActivity : byte
{
    None,
    Call,
    Session,
    Playback,
    PcMedia,
    Timer,
    Muster,
    Notice,
    GameTimer,
    Fishing,
}

internal enum IslandNotice : byte
{
    DoNotDisturb,
    LockPosition,
}

internal readonly record struct IslandSignals(bool Call, bool Session, bool Playback, bool Timer, bool Muster,
    bool PcMedia = false, bool GameTimer = false, bool Fishing = false);

internal static class IslandActivities
{
    public const long MusterHorizonSeconds = 3600;
    public const long MusterStartedGraceSeconds = 300;

    public static IslandActivity Select(in IslandSignals signals)
    {
        if (signals.Call)
        {
            return IslandActivity.Call;
        }

        if (signals.Session)
        {
            return IslandActivity.Session;
        }

        if (signals.Playback)
        {
            return IslandActivity.Playback;
        }

        if (signals.PcMedia)
        {
            return IslandActivity.PcMedia;
        }

        if (signals.Timer)
        {
            return IslandActivity.Timer;
        }

        if (signals.Muster)
        {
            return IslandActivity.Muster;
        }

        if (signals.GameTimer)
        {
            return IslandActivity.GameTimer;
        }

        return signals.Fishing ? IslandActivity.Fishing : IslandActivity.None;
    }

    public static string OwnerAppId(IslandActivity activity)
    {
        switch (activity)
        {
            case IslandActivity.Call:
                return "message";
            case IslandActivity.Session:
                return "aetherstream";
            case IslandActivity.Playback:
            case IslandActivity.PcMedia:
                return "music";
            case IslandActivity.Timer:
                return "clock";
            case IslandActivity.Muster:
                return "muster";
            case IslandActivity.GameTimer:
                return "timers";
            case IslandActivity.Fishing:
                return "fishing";
            default:
                return string.Empty;
        }
    }

    public static bool MusterInWindow(long startsAtUnix, long nowUnix)
    {
        var untilStart = startsAtUnix - nowUnix;
        return untilStart <= MusterHorizonSeconds && untilStart > -MusterStartedGraceSeconds;
    }

    public static MusterDto? SoonestMuster(MusterDto[] going, MusterDto? mine, long nowUnix)
    {
        MusterDto? soonest = null;
        if (mine is not null && MusterInWindow(mine.StartsAtUnix, nowUnix))
        {
            soonest = mine;
        }

        for (var index = 0; index < going.Length; index++)
        {
            var candidate = going[index];
            if (!MusterInWindow(candidate.StartsAtUnix, nowUnix))
            {
                continue;
            }

            if (soonest is null || candidate.StartsAtUnix < soonest.StartsAtUnix)
            {
                soonest = candidate;
            }
        }

        return soonest;
    }
}
