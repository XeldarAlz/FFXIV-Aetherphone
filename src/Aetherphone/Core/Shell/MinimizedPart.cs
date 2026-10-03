using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Shell;

internal enum MinimizedPart : byte
{
    Clock,
    Date,
    NowPlaying,
    Calls,
    Alerts,
    Badge,
    EorzeaClock,
    Weather,
    Resets,
    Gil,
    Coin,
    Ventures,
    Rings,
}

internal static class MinimizedParts
{
    public const int Count = 13;

    private static readonly MinimizedPart[] DefaultOrder =
    {
        MinimizedPart.Clock, MinimizedPart.Date, MinimizedPart.NowPlaying, MinimizedPart.Calls, MinimizedPart.Alerts,
        MinimizedPart.EorzeaClock, MinimizedPart.Weather, MinimizedPart.Resets, MinimizedPart.Gil, MinimizedPart.Coin,
        MinimizedPart.Ventures, MinimizedPart.Rings, MinimizedPart.Badge,
    };

    private static readonly string[] Ids =
    {
        "clock", "date", "nowPlaying", "calls", "alerts", "badge", "eorzeaClock", "weather", "resets", "gil", "coin",
        "ventures", "rings",
    };

    private static readonly bool[] Defaults =
    {
        true, true, true, true, true, true, true, true, false, false, false, false, false,
    };

    private static readonly LocString[] Labels =
    {
        L.Minimized.Clock, L.Minimized.Date, L.Minimized.NowPlaying, L.Minimized.Calls, L.Minimized.Banners,
        L.Minimized.Badge, L.Minimized.EorzeaClock, L.Minimized.Weather, L.Minimized.Resets, L.Minimized.Gil,
        L.Minimized.Coin, L.Minimized.Ventures, L.Minimized.Rings,
    };

    public static ReadOnlySpan<MinimizedPart> Default => DefaultOrder;

    public static string Id(MinimizedPart part) => Ids[(int)part];

    public static bool EnabledByDefault(MinimizedPart part) => Defaults[(int)part];

    public static LocString Label(MinimizedPart part) => Labels[(int)part];

    public static bool IsPage(MinimizedPart part) => part >= MinimizedPart.EorzeaClock;

    public static bool IsLive(MinimizedPart part) =>
        part is MinimizedPart.NowPlaying or MinimizedPart.Calls or MinimizedPart.Alerts;

    public static bool TryParse(string id, out MinimizedPart part)
    {
        for (var index = 0; index < Ids.Length; index++)
        {
            if (string.Equals(Ids[index], id, StringComparison.Ordinal))
            {
                part = (MinimizedPart)index;
                return true;
            }
        }

        part = MinimizedPart.Clock;
        return false;
    }
}
