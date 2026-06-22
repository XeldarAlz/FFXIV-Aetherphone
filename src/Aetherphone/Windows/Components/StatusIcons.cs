using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class StatusIcons
{
    private const float LabelScale = 0.8f;
    private const float NubWidth = 2f;
    private const float BodyWidth = 22f;
    private const float LabelGap = 6f;
    private const float SignalGap = 7f;
    private const float BarWidth = 3f;
    private const float BarGap = 1.8f;
    private const float RightPadding = 24f;
    private const float MinRightPadding = 8f;

    public static float MeasureWidth(float scale, int percent)
    {
        var labelWidth = Typography.Measure(percent + "%", LabelScale).X;
        return (NubWidth + BodyWidth) * scale + LabelGap * scale + labelWidth + SignalGap * scale + SignalClusterWidth(scale);
    }

    public static void Draw(Rect screen, PhoneTheme theme, float rowCenterY, float minClusterLeft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var device = Plugin.Device;

        var clusterWidth = MeasureWidth(scale, device.BatteryPercent);
        var nubRight = screen.Max.X - RightPadding * scale;
        if (nubRight - clusterWidth < minClusterLeft)
        {
            nubRight = MathF.Min(screen.Max.X - MinRightPadding * scale, minClusterLeft + clusterWidth);
        }

        var batteryLeft = DrawBattery(theme, rowCenterY, nubRight, device.BatteryPercent, device.Charging);
        var labelLeft = DrawBatteryLabel(theme, rowCenterY, batteryLeft, device.BatteryPercent);
        DrawSignal(theme, rowCenterY, labelLeft, device.SignalBars);
    }

    private static float SignalClusterWidth(float scale) => (BarWidth * 4f + BarGap * 3f) * scale;

    private static float DrawBattery(PhoneTheme theme, float rowCenterY, float nubRight, int percent, bool charging)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var nubWidth = NubWidth * scale;
        var nubHeight = 4.5f * scale;
        var bodyWidth = BodyWidth * scale;
        var bodyHeight = 11f * scale;
        var rounding = 3f * scale;

        var bodyMax = new Vector2(nubRight - nubWidth, rowCenterY + bodyHeight * 0.5f);
        var bodyMin = new Vector2(bodyMax.X - bodyWidth, rowCenterY - bodyHeight * 0.5f);

        var shell = Palette.WithAlpha(theme.TextStrong, 0.6f);
        dl.AddRect(bodyMin, bodyMax, ImGui.GetColorU32(shell), rounding, ImDrawFlags.RoundCornersAll, 1.2f * scale);

        var nubMin = new Vector2(bodyMax.X, rowCenterY - nubHeight * 0.5f);
        var nubMax = new Vector2(bodyMax.X + nubWidth, rowCenterY + nubHeight * 0.5f);
        dl.AddRectFilled(nubMin, nubMax, ImGui.GetColorU32(shell), nubWidth * 0.5f);

        var fillColor = charging
            ? theme.ToggleOn
            : percent <= 20 ? theme.Danger : theme.TextStrong;

        var inset = 1.8f * scale;
        var trackLeft = bodyMin.X + inset;
        var trackWidth = bodyMax.X - inset - trackLeft;
        var fillWidth = trackWidth * Math.Clamp(percent / 100f, 0.05f, 1f);
        var fillMin = new Vector2(trackLeft, bodyMin.Y + inset);
        var fillMax = new Vector2(trackLeft + fillWidth, bodyMax.Y - inset);
        dl.AddRectFilled(fillMin, fillMax, ImGui.GetColorU32(fillColor), rounding * 0.5f);

        return bodyMin.X;
    }

    private static float DrawBatteryLabel(PhoneTheme theme, float rowCenterY, float batteryLeft, int percent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var label = percent + "%";
        var size = Typography.Measure(label, LabelScale);
        var position = new Vector2(batteryLeft - LabelGap * scale - size.X, rowCenterY - size.Y * 0.5f);
        Typography.Draw(position, label, theme.TextStrong, LabelScale);
        return position.X;
    }

    private static void DrawSignal(PhoneTheme theme, float rowCenterY, float labelLeft, int bars)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var barWidth = BarWidth * scale;
        var barGap = BarGap * scale;
        var clusterLeft = labelLeft - SignalGap * scale - SignalClusterWidth(scale);
        var baseline = rowCenterY + 6.5f * scale;

        Span<float> heights = stackalloc float[4] { 5f, 7.8f, 10.6f, 13.4f };
        for (var index = 0; index < heights.Length; index++)
        {
            var left = clusterLeft + index * (barWidth + barGap);
            var height = heights[index] * scale;
            var min = new Vector2(left, baseline - height);
            var max = new Vector2(left + barWidth, baseline);
            var color = index < bars ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.26f);
            dl.AddRectFilled(min, max, ImGui.GetColorU32(color), 1f * scale);
        }
    }
}
