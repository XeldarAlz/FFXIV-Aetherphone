using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Timers;

internal sealed class TimerText
{
    public CachedText Countdown;
    public CachedText End;
    public CachedText Detail;

    public string CountdownFor(long remainingSeconds) =>
        WidgetText.Countdown(ref Countdown, TimeSpan.FromSeconds(Math.Max(0L, remainingSeconds)));

    public string EndFor(long endUnix) => TimerLabels.End(ref End, endUnix);
}

internal static class TimerLabels
{
    private const int DayShift = 40;

    public static string End(ref CachedText cache, long endUnix)
    {
        if (endUnix <= 0)
        {
            return string.Empty;
        }

        var today = DateTime.Now.Date;
        var key = (endUnix / 60) ^ ((long)today.DayOfYear << DayShift);
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var local = DateTimeOffset.FromUnixTimeSeconds(endUnix).ToLocalTime();
        var label = local.Date == today
            ? Loc.T(L.Timers.At, TimeText.Clock(local))
            : TimeText.FutureMoment(endUnix);
        return cache.Store(key, label);
    }

    public static string Seen(ref CachedText cache, long seenUnix, string prefix)
    {
        var minutesAgo = Math.Max(0L, (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seenUnix) / 60);
        var key = (minutesAgo << 1) ^ prefix.GetHashCode();
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var seen = Loc.T(L.Timers.SeenAgo, TimeText.Ago(seenUnix));
        return cache.Store(key, prefix.Length > 0 ? string.Concat(prefix, " · ", seen) : seen);
    }
}
