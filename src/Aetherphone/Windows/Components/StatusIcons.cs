using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class StatusIcons
{
    private const float LabelScale = 0.8f;

    public static void Draw(Rect screen, PhoneTheme theme, float rowCenterY)
    {
        var device = Plugin.Device;
        var batteryLeft = DrawBattery(screen, theme, rowCenterY, device.BatteryPercent, device.Charging);
        var labelLeft = DrawBatteryLabel(theme, rowCenterY, batteryLeft, device.BatteryPercent);
        DrawSignal(theme, rowCenterY, labelLeft, device.SignalBars);
    }

    private static float DrawBattery(Rect screen, PhoneTheme theme, float rowCenterY, int percent, bool charging)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var nubWidth = 2f * scale;
        var nubHeight = 4.5f * scale;
        var bodyWidth = 22f * scale;
        var bodyHeight = 11f * scale;
        var rounding = 3f * scale;

        var bodyMax = new Vector2(screen.Max.X - 24f * scale - nubWidth, rowCenterY + bodyHeight * 0.5f);
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
        var position = new Vector2(batteryLeft - 6f * scale - size.X, rowCenterY - size.Y * 0.5f);
        Typography.Draw(position, label, theme.TextStrong, LabelScale);
        return position.X;
    }

    private static void DrawSignal(PhoneTheme theme, float rowCenterY, float labelLeft, int bars)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var barWidth = 3f * scale;
        var barGap = 1.8f * scale;
        var clusterWidth = barWidth * 4f + barGap * 3f;
        var clusterLeft = labelLeft - 7f * scale - clusterWidth;
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
