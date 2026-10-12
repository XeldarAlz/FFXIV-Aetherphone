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
        var framework = GameMemory.Attached ? Framework.Instance() : null;
        if (framework == null)
        {
            var natural = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 144 / 7;
            var minute = GameMemory.Simulated?.EorzeaMinuteOverride ?? -1;
            return minute < 0 ? natural : natural - natural % SecondsPerDay + minute * 60L;
        }

        var clock = framework->ClientTime;
        return clock.IsEorzeaTimeOverridden ? clock.EorzeaTimeOverride : clock.EorzeaTime;
    }

    public static int CurrentMinuteOfDay() => (int)(CurrentSeconds() % SecondsPerDay / 60);
    public string Formatted => $"{Hour:D2}:{Minute:D2}";
}
