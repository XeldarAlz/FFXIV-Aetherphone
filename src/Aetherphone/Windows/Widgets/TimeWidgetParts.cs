using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal static class TimeWidgetParts
{
    private const int SplitCacheCapacity = 128;
    private const int DaysPerWeek = 7;
    private static readonly Vector4 DarkFace = new(0.17f, 0.17f, 0.19f, 1f);

    private readonly struct ClockSplit
    {
        public readonly string Head;
        public readonly string Tail;

        public ClockSplit(string head, string tail)
        {
            Head = head;
            Tail = tail;
        }
    }

    private static readonly Dictionary<string, ClockSplit> Splits = new(StringComparer.Ordinal);

    public static ClockFacePaint ClockPaint(in WidgetInk ink, float hours, Vector4 accent)
    {
        switch (ink.Mode)
        {
            case WidgetMode.Tinted:
            case WidgetMode.Clear:
                return new ClockFacePaint(ink.Fill, ink.Primary, ink.Accent(accent), 0f);
            case WidgetMode.Dark:
                return new ClockFacePaint(ink.Fade(DarkFace), ink.Primary, ink.Accent(accent), 0f);
        }

        var paint = AnalogClock.DayNight(hours, accent);
        return new ClockFacePaint(ink.Fade(paint.Face), ink.Fade(paint.Ink), ink.Fade(paint.Seconds), paint.Sheen);
    }

    public static float ClockWidth(string clock, in TextStyle style, in TextStyle suffixStyle)
    {
        var split = Split(clock);
        var width = WidgetText.TabularWidth(split.Head, style);
        return split.Tail.Length == 0 ? width : width + Typography.Measure(split.Tail, suffixStyle).X;
    }

    public static float Clock(ImDrawListPtr drawList, Vector2 position, string clock, Vector4 color,
        in TextStyle style, in TextStyle suffixStyle, Vector4 suffixColor)
    {
        var split = Split(clock);
        var width = WidgetText.Tabular(drawList, position, split.Head, color, style);
        var headHeight = Typography.Measure(split.Head, style).Y;
        if (split.Tail.Length > 0)
        {
            var tailSize = Typography.Measure(split.Tail, suffixStyle);
            var baseline = position.Y + headHeight * 0.78f;
            Typography.Draw(drawList, new Vector2(position.X + width, baseline - tailSize.Y * 0.78f), split.Tail,
                suffixColor, suffixStyle);
        }

        return headHeight;
    }

    public static string RelativeDay(DateTime localMoment)
    {
        var today = DateTime.Today;
        var day = localMoment.Date;
        if (day == today)
        {
            return Loc.T(L.Clock.DayToday);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Clock.DayTomorrow);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Clock.DayYesterday);
        }

        return day.ToString("ddd", Loc.Culture);
    }

    public static string DayAndClock(DateTime localMoment)
    {
        if (localMoment.Date == DateTime.Today)
        {
            return TimeText.Clock(localMoment);
        }

        var dayOffset = (localMoment.Date - DateTime.Today).Days;
        var day = dayOffset switch
        {
            1 => Loc.T(L.Time.Tomorrow),
            >= DaysPerWeek => localMoment.ToString("ddd d", Loc.Culture),
            _ => localMoment.ToString("ddd", Loc.Culture),
        };
        return string.Concat(day, " ", TimeText.Clock(localMoment));
    }

    public static long MinuteKey(DateTime moment) => moment.Ticks / TimeSpan.TicksPerMinute;

    public static long DayAndClockKey(DateTime localMoment)
    {
        var dayOffset = (localMoment.Date - DateTime.Today).Days;
        var relation = dayOffset switch
        {
            0 or 1 => dayOffset,
            >= DaysPerWeek => 3,
            _ => 2,
        };
        return MinuteKey(localMoment) * 4 + relation;
    }

    private static ClockSplit Split(string clock)
    {
        if (Splits.TryGetValue(clock, out var split))
        {
            return split;
        }

        if (Splits.Count >= SplitCacheCapacity)
        {
            Splits.Clear();
        }

        var lastDigit = -1;
        var firstDigit = -1;
        for (var index = 0; index < clock.Length; index++)
        {
            if (!char.IsDigit(clock[index]))
            {
                continue;
            }

            if (firstDigit < 0)
            {
                firstDigit = index;
            }

            lastDigit = index;
        }

        split = lastDigit < 0 || firstDigit > 0 || lastDigit == clock.Length - 1
            ? new ClockSplit(clock, string.Empty)
            : new ClockSplit(clock[..(lastDigit + 1)], clock[(lastDigit + 1)..]);
        Splits[clock] = split;
        return split;
    }
}
