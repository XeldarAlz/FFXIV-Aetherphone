using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StatusIcons
{
    private const float NubWidth = 1.6f;
    private const float NubHeight = 4.2f;
    private const float NubGap = 1f;
    private const float BodyWidth = 25f;
    private const float BodyHeight = 12.5f;
    private const float BodyRadius = 4f;
    private const float TrackAlpha = 0.38f;
    private const float NubAlpha = 0.45f;
    private const float MinFillFraction = 0.06f;
    private const float WifiGap = 6f;
    private const float WifiWidth = 14f;
    private const float WifiOuterRadius = 8.5f;
    private const float WifiArcStep = 2.9f;
    private const float WifiStroke = 2.2f;
    private const float WifiDotRadius = 1.5f;
    private const float WifiApexOffset = 4.1f;
    private const float WifiStartAngle = -MathF.PI * 0.75f;
    private const float WifiEndAngle = -MathF.PI * 0.25f;
    private const int WifiArcCount = 3;
    private const int WifiArcSegments = 14;
    private const float SignalGap = 6f;
    private const float BarWidth = 3f;
    private const float BarGap = 1.6f;
    private const float BarRadius = 1.5f;
    private const float BarBaselineOffset = 5.9f;
    private const float DimAlpha = 0.30f;
    private const float RightPadding = 24f;
    private const float MinRightPadding = 8f;
    private const int LowBatteryPercent = 20;
    private const int CriticalBatteryPercent = 10;
    private const float CriticalPulseSpeed = 3.2f;
    private const int StrongLatencyMilliseconds = 120;
    private const int FairLatencyMilliseconds = 260;
    private const float HoverPadding = 4f;
    private static readonly float[] BarHeights = { 4.6f, 7f, 9.4f, 11.8f };
    private static readonly Vector4 KnockoutDark = new(0f, 0f, 0f, 0.82f);
    private static readonly Vector4 KnockoutLight = new(1f, 1f, 1f, 0.95f);
    private static readonly string[] PercentDigits = BuildDigits();

    private static string networkText = string.Empty;
    private static int networkLatency = -1;
    private static int networkLoss = -1;
    private static int networkBars = -1;
    private static string? networkCenter;
    private static string batteryText = string.Empty;
    private static int batteryPercentShown = -1;
    private static int batteryState = -1;
    private static string textLanguage = string.Empty;

    public static float MeasureWidth(float scale, int percent) =>
        (NubWidth + NubGap + BodyWidth) * scale + WifiGap * scale + WifiWidth * scale + SignalGap * scale +
        SignalClusterWidth(scale);

    public static void Draw(Rect screen, PhoneTheme theme, float rowCenterY, float minClusterLeft)
    {
        var scale = UiScale.Current;
        var device = Plugin.Device;
        var clusterWidth = MeasureWidth(scale, device.BatteryPercent);
        var nubRight = screen.Max.X - RightPadding * scale;
        if (nubRight - clusterWidth < minClusterLeft)
        {
            nubRight = MathF.Min(screen.Max.X - MinRightPadding * scale, minClusterLeft + clusterWidth);
        }

        var batteryLeft = DrawBattery(theme, rowCenterY, nubRight, device.BatteryPercent, device.Charging);
        var wifiLeft = DrawWifi(theme, rowCenterY, batteryLeft,
            WifiArcs(device.SignalBars, device.LatencyMilliseconds));
        DrawSignal(theme, rowCenterY, wifiLeft, device.SignalBars);
        ReportAnchors(scale, rowCenterY, nubRight, batteryLeft, wifiLeft);
        ShowTooltips(scale, rowCenterY, nubRight, batteryLeft, wifiLeft);
    }

    private static void ShowTooltips(float scale, float rowCenterY, float nubRight, float batteryLeft, float wifiLeft)
    {
        var pad = HoverPadding * scale;
        var top = rowCenterY - BodyHeight * 0.5f * scale - pad;
        var bottom = rowCenterY + BodyHeight * 0.5f * scale + pad;
        var batteryRect = new Rect(new Vector2(batteryLeft - pad, top), new Vector2(nubRight + pad, bottom));
        var signalLeft = wifiLeft - SignalGap * scale - SignalClusterWidth(scale);
        var networkRect = new Rect(new Vector2(signalLeft - pad, top),
            new Vector2(wifiLeft + WifiWidth * scale + pad, bottom));
        RefreshLanguage();
        HoverTooltip.Show("status.network", networkRect, NetworkText());
        HoverTooltip.Show("status.battery", batteryRect, BatteryText());
    }

    private static void RefreshLanguage()
    {
        var language = Loc.Current.Code;
        if (string.Equals(language, textLanguage, StringComparison.Ordinal))
        {
            return;
        }

        textLanguage = language;
        networkLatency = -1;
        batteryPercentShown = -1;
    }

    private static string NetworkText()
    {
        var device = Plugin.Device;
        var latency = device.LatencyMilliseconds;
        var loss = device.PacketLossPercent;
        var bars = device.SignalBars;
        var center = device.DataCenterName;
        if (latency == networkLatency && loss == networkLoss && bars == networkBars &&
            string.Equals(center, networkCenter, StringComparison.Ordinal))
        {
            return networkText;
        }

        networkLatency = latency;
        networkLoss = loss;
        networkBars = bars;
        networkCenter = center;
        networkText = bars <= 0
            ? Loc.T(L.Home.StatusOffline)
            : center is null
                ? Loc.T(L.Home.StatusPingNoCenter, latency, loss)
                : Loc.T(L.Home.StatusPing, center, latency, loss);
        return networkText;
    }

    private static string BatteryText()
    {
        var device = Plugin.Device;
        var percent = device.BatteryPercent;
        var state = !device.BatteryPresent ? 0 : device.Charging ? 1 : 2;
        if (percent == batteryPercentShown && state == batteryState)
        {
            return batteryText;
        }

        batteryPercentShown = percent;
        batteryState = state;
        batteryText = state switch
        {
            0 => Loc.T(L.Home.StatusNoBattery),
            1 => Loc.T(L.Home.StatusBatteryCharging, percent),
            _ => Loc.T(L.Home.StatusBattery, percent),
        };
        return batteryText;
    }

    private static string[] BuildDigits()
    {
        var digits = new string[101];
        for (var percent = 0; percent < digits.Length; percent++)
        {
            digits[percent] = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return digits;
    }

    internal static int WifiArcs(int signalBars, int latencyMilliseconds)
    {
        if (signalBars <= 0)
        {
            return 0;
        }

        if (latencyMilliseconds <= StrongLatencyMilliseconds)
        {
            return WifiArcCount;
        }

        return latencyMilliseconds <= FairLatencyMilliseconds ? 2 : 1;
    }

    private static void ReportAnchors(float scale, float rowCenterY, float nubRight, float batteryLeft, float wifiLeft)
    {
        if (!UiAnchors.Recording)
        {
            return;
        }

        var top = rowCenterY - 9f * scale;
        var bottom = rowCenterY + 9f * scale;
        UiAnchors.Report("chrome.battery",
            new Rect(new Vector2(batteryLeft - 2f * scale, top), new Vector2(nubRight, bottom)));
        var signalWidth = SignalClusterWidth(scale);
        var signalLeft = wifiLeft - SignalGap * scale - signalWidth;
        UiAnchors.Report("chrome.signal",
            new Rect(new Vector2(signalLeft, top), new Vector2(signalLeft + signalWidth, bottom)));
    }

    private static float SignalClusterWidth(float scale) => (BarWidth * 4f + BarGap * 3f) * scale;

    private static float DrawBattery(PhoneTheme theme, float rowCenterY, float nubRight, int percent, bool charging)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var nubWidth = NubWidth * scale;
        var nubHeight = NubHeight * scale;
        var bodyMax = new Vector2(nubRight - nubWidth - NubGap * scale, rowCenterY + BodyHeight * 0.5f * scale);
        var bodyMin = new Vector2(bodyMax.X - BodyWidth * scale, rowCenterY - BodyHeight * 0.5f * scale);
        var radius = BodyRadius * scale;
        var warning = WarningInk(theme, percent, charging);
        var fill = charging ? theme.ToggleOn : warning ?? theme.TextStrong;
        drawList.AddRectFilled(bodyMin, bodyMax,
            ImGui.GetColorU32(Palette.WithAlpha(warning ?? theme.TextStrong, TrackAlpha)), radius);
        var nubMin = new Vector2(nubRight - nubWidth, rowCenterY - nubHeight * 0.5f);
        drawList.AddRectFilled(nubMin, new Vector2(nubRight, rowCenterY + nubHeight * 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, NubAlpha)), nubWidth, ImDrawFlags.RoundCornersRight);
        var clamped = Math.Clamp(percent, 0, 100);
        var fillRight = bodyMin.X + (bodyMax.X - bodyMin.X) * Math.Clamp(clamped / 100f, MinFillFraction, 1f);
        drawList.PushClipRect(bodyMin, new Vector2(fillRight, bodyMax.Y), true);
        drawList.AddRectFilled(bodyMin, bodyMax, ImGui.GetColorU32(fill), radius);
        drawList.PopClipRect();
        var digits = PercentDigits[clamped];
        var size = Typography.Measure(digits, TextStyles.StatusDigits);
        var position = new Vector2((bodyMin.X + bodyMax.X - size.X) * 0.5f, rowCenterY - size.Y * 0.5f);
        var knockout = Palette.Luminance(fill) >= 0.5f ? KnockoutDark : KnockoutLight;
        drawList.PushClipRect(bodyMin, new Vector2(fillRight, bodyMax.Y), true);
        Typography.Draw(drawList, position, digits, knockout, TextStyles.StatusDigits);
        drawList.PopClipRect();
        if (fillRight < bodyMax.X)
        {
            drawList.PushClipRect(new Vector2(fillRight, bodyMin.Y), bodyMax, true);
            Typography.Draw(drawList, position, digits, warning ?? theme.TextStrong, TextStyles.StatusDigits);
            drawList.PopClipRect();
        }

        return bodyMin.X;
    }

    private static Vector4? WarningInk(PhoneTheme theme, int percent, bool charging)
    {
        if (charging || percent > LowBatteryPercent)
        {
            return null;
        }

        if (percent > CriticalBatteryPercent)
        {
            return theme.Danger;
        }

        var breath = 0.72f + 0.28f * (0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * CriticalPulseSpeed));
        return Palette.WithAlpha(theme.Danger, breath);
    }

    private static float DrawWifi(PhoneTheme theme, float rowCenterY, float batteryLeft, int arcs)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var wifiLeft = batteryLeft - WifiGap * scale - WifiWidth * scale;
        var apex = new Vector2(wifiLeft + WifiWidth * 0.5f * scale, rowCenterY + WifiApexOffset * scale);
        var lit = ImGui.GetColorU32(theme.TextStrong);
        var dim = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, DimAlpha));
        for (var arcIndex = 0; arcIndex < WifiArcCount; arcIndex++)
        {
            var radius = (WifiOuterRadius - WifiArcStep * (WifiArcCount - 1 - arcIndex)) * scale;
            drawList.PathArcTo(apex, radius, WifiStartAngle, WifiEndAngle, WifiArcSegments);
            drawList.PathStroke(arcIndex < arcs ? lit : dim, ImDrawFlags.None, WifiStroke * scale);
        }

        drawList.AddCircleFilled(apex, WifiDotRadius * scale, arcs > 0 ? lit : dim, 12);
        return wifiLeft;
    }

    private static void DrawSignal(PhoneTheme theme, float rowCenterY, float wifiLeft, int bars)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var barWidth = BarWidth * scale;
        var barGap = BarGap * scale;
        var clusterLeft = wifiLeft - SignalGap * scale - SignalClusterWidth(scale);
        var baseline = rowCenterY + BarBaselineOffset * scale;
        var lit = ImGui.GetColorU32(theme.TextStrong);
        var dim = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, DimAlpha));
        for (var index = 0; index < BarHeights.Length; index++)
        {
            var left = clusterLeft + index * (barWidth + barGap);
            var height = BarHeights[index] * scale;
            var min = new Vector2(left, baseline - height);
            var max = new Vector2(left + barWidth, baseline);
            drawList.AddRectFilled(min, max, index < bars ? lit : dim, BarRadius * scale, ImDrawFlags.RoundCornersTop);
        }
    }
}
