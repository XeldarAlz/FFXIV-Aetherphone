using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal static class SocialTimeZone
{
    public const int MinOffsetMinutes = -720;

    public const int MaxOffsetMinutes = 840;

    public const int StepMinutes = 15;

    public static int DeviceOffsetMinutes()
    {
        var offset = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);
        return Math.Clamp(offset, MinOffsetMinutes, MaxOffsetMinutes);
    }

    public static int EffectiveOffsetMinutes(Configuration configuration)
    {
        if (configuration.TimeZoneManual)
        {
            return Math.Clamp(configuration.ManualUtcOffsetMinutes, MinOffsetMinutes, MaxOffsetMinutes);
        }

        return DeviceOffsetMinutes();
    }

    public static string FormatOffset(int minutes)
    {
        if (minutes == 0)
        {
            return "UTC";
        }

        var sign = minutes < 0 ? "-" : "+";
        var absolute = Math.Abs(minutes);
        var hours = absolute / 60;
        var remainder = absolute % 60;
        return remainder == 0
            ? $"UTC{sign}{hours}"
            : $"UTC{sign}{hours}:{remainder:D2}";
    }

    public static string ClockLabel(int minutes)
    {
        var localTime = DateTime.UtcNow.AddMinutes(minutes);
        return localTime.ToString("t", Loc.Culture);
    }

    public static string Describe(int minutes) => $"{ClockLabel(minutes)} · {FormatOffset(minutes)}";
}
