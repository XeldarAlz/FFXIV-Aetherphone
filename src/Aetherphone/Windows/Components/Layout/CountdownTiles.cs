using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct CountdownStyle(
    Vector4 FillTop,
    Vector4 FillBottom,
    Vector4 Stroke,
    Vector4 DigitInk,
    Vector4 UnitInk,
    TextStyle DigitStyle,
    TextStyle UnitStyle,
    float RadiusUnits,
    float GapUnits);

internal sealed class CountdownTiles
{
    public const long SecondsPerDay = 86400L;

    private const float RollWindow = 0.30f;
    private const float RollTravel = 0.7f;
    private const float FadedAlpha = 0.01f;
    private const float StrokeUnits = 1f;
    private const long SecondsPerMinute = 60L;
    private const long SecondsPerHour = 3600L;
    private const int MaxUnits = 4;

    private static readonly string[] TwoDigits = BuildTwoDigits();

    private readonly CountdownUnit[] units = new CountdownUnit[MaxUnits];
    private long dayDigitsValue = -1;
    private string dayDigits = string.Empty;
    private System.Globalization.CultureInfo? dayDigitsCulture;

    private readonly record struct CountdownUnit(long Value, long Previous, bool Rolling, LocString Label);

    public static bool InLastDay(long remainingSeconds) => remainingSeconds < SecondsPerDay;

    public void Draw(ImDrawListPtr drawList, Rect row, long remainingSeconds, double nowSeconds,
        in CountdownStyle style, float scale, bool secondsInLastDayOnly = false)
    {
        var count = Fill(remainingSeconds, secondsInLastDayOnly);
        var gap = style.GapUnits * scale;
        var tileWidth = (row.Width - gap * (count - 1)) / count;
        var subSecond = (float)(nowSeconds - Math.Floor(nowSeconds));
        for (var index = 0; index < count; index++)
        {
            var left = row.Min.X + index * (tileWidth + gap);
            var tile = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + tileWidth, row.Max.Y));
            DrawTile(drawList, tile, units[index], subSecond, style, scale);
        }
    }

    private int Fill(long remainingSeconds, bool secondsInLastDayOnly)
    {
        var remaining = Math.Max(0L, remainingSeconds);
        var next = remaining + 1;
        var days = remaining / SecondsPerDay;
        var count = 0;
        if (days > 0)
        {
            units[count++] = Unit(days, next / SecondsPerDay, L.Common.CountdownDays);
        }

        units[count++] = Unit(remaining % SecondsPerDay / SecondsPerHour, next % SecondsPerDay / SecondsPerHour,
            L.Common.CountdownHours);
        units[count++] = Unit(remaining % SecondsPerHour / SecondsPerMinute, next % SecondsPerHour / SecondsPerMinute,
            L.Common.CountdownMinutes);
        if (days == 0 || !secondsInLastDayOnly)
        {
            units[count++] = Unit(remaining % SecondsPerMinute, next % SecondsPerMinute, L.Common.CountdownSeconds);
        }

        return count;
    }

    private static CountdownUnit Unit(long value, long previous, LocString label) =>
        new(value, previous, previous != value, label);

    private void DrawTile(ImDrawListPtr drawList, Rect tile, in CountdownUnit unit, float subSecond,
        in CountdownStyle style, float scale)
    {
        var radius = style.RadiusUnits * scale;
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(style.FillTop),
            ImGui.GetColorU32(style.FillBottom));
        if (style.Stroke.W > 0f)
        {
            Squircle.Stroke(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(style.Stroke),
                StrokeUnits * scale);
        }

        var unitHeight = Typography.LineHeight(style.UnitStyle);
        var digitHeight = Typography.LineHeight(style.DigitStyle);
        var digitTop = tile.Center.Y - (digitHeight + unitHeight) * 0.5f;
        var unitLabel = Typography.FitText(Loc.T(unit.Label), tile.Width - Metrics.Space.Sm * scale,
            style.UnitStyle);
        var unitWidth = Typography.Measure(unitLabel, style.UnitStyle).X;
        Typography.Draw(drawList, new Vector2(tile.Center.X - unitWidth * 0.5f, digitTop + digitHeight), unitLabel,
            style.UnitInk, style.UnitStyle);
        var current = Digits(unit.Value);
        if (!unit.Rolling || subSecond >= RollWindow)
        {
            DrawDigits(drawList, tile.Center.X, digitTop, current, 1f, style);
            return;
        }

        var progress = subSecond / RollWindow;
        var eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        var travel = digitHeight * RollTravel;
        drawList.PushClipRect(new Vector2(tile.Min.X, digitTop), new Vector2(tile.Max.X, digitTop + digitHeight), true);
        DrawDigits(drawList, tile.Center.X, digitTop - travel * eased, Digits(unit.Previous), 1f - eased, style);
        DrawDigits(drawList, tile.Center.X, digitTop + travel * (1f - eased), current, eased, style);
        drawList.PopClipRect();
    }

    private static void DrawDigits(ImDrawListPtr drawList, float centerX, float top, string digits, float alpha,
        in CountdownStyle style)
    {
        if (alpha <= FadedAlpha)
        {
            return;
        }

        var size = Typography.Measure(digits, style.DigitStyle);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), digits,
            style.DigitInk with { W = style.DigitInk.W * alpha }, style.DigitStyle);
    }

    private string Digits(long value)
    {
        if (value < TwoDigits.Length)
        {
            return TwoDigits[value];
        }

        if (value != dayDigitsValue || !ReferenceEquals(dayDigitsCulture, Loc.Culture))
        {
            dayDigitsValue = value;
            dayDigitsCulture = Loc.Culture;
            dayDigits = NumberText.Group((int)Math.Min(value, int.MaxValue));
        }

        return dayDigits;
    }

    private static string[] BuildTwoDigits()
    {
        var digits = new string[100];
        for (var index = 0; index < digits.Length; index++)
        {
            digits[index] = index.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        return digits;
    }
}
