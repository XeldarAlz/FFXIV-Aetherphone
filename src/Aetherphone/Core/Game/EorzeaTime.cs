using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Aetherphone.Core.Game;

internal readonly record struct EorzeaTime(int Hour, int Minute)
{
    public const int MinutesPerDay = 1440;
    private const long SecondsPerDay = 86400;

    public static EorzeaTime Now()
    {
        var seconds = CurrentSeconds();
        return new EorzeaTime((int)(seconds / 3600 % 24), (int)(seconds / 60 % 60));
    }

    public static unsafe long CurrentSeconds()
    {
        var framework = Framework.Instance();
        if (framework == null)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 144 / 7;
        }

        var clock = framework->ClientTime;
        return RenderedSeconds(clock.EorzeaTime, clock.EorzeaTimeOverride, clock.IsEorzeaTimeOverridden);
    }

    public static long RenderedSeconds(long naturalSeconds, long overrideSeconds, bool overridden) =>
        overridden ? overrideSeconds : naturalSeconds + overrideSeconds;

    public static bool HasStrayOverrideOffset(long overrideSeconds, bool overridden) =>
        !overridden && overrideSeconds != 0;

    public static void ReleaseOverride(ref long overrideSeconds, ref bool overridden)
    {
        overrideSeconds = 0;
        overridden = false;
    }

    public static int CurrentMinuteOfDay() => (int)(CurrentSeconds() % SecondsPerDay / 60);
    public string Formatted => $"{Hour:D2}:{Minute:D2}";
}
