using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell;

internal static class StatusBar
{
    private const float TimeScale = 0.95f;
    private const FontWeight TimeWeight = FontWeight.SemiBold;
    private const float EarGap = 10f;
    private const float IndicatorGap = 6f;
    private const float IndicatorHeight = 11f;
    internal static readonly Vector4 DndTone = new(0.58f, 0.55f, 0.96f, 1f);
    private const float IslandSidePadding = 14f;
    private const float MinIslandHalfWidth = 30f;
    private const float MaxIslandHalfWidth = 49f;
    private const float IslandHeight = 26f;
    private const float IslandTop = 9f;
    private static string cachedTime = string.Empty;
    private static int cachedTimeKey = -1;
    private static int cachedFormatVersion = -1;

    internal static string CurrentTime()
    {
        var now = DateTime.Now;
        var key = now.Hour * 60 + now.Minute;
        if (key != cachedTimeKey || cachedFormatVersion != TimeText.FormatVersion)
        {
            cachedTimeKey = key;
            cachedFormatVersion = TimeText.FormatVersion;
            cachedTime = TimeText.CompactClock(now);
        }

        return cachedTime;
    }

    public static void Draw(Rect screen, PhoneTheme theme, bool landscape, float alpha = 1f)
    {
        var scale = UiScale.Current;
        var rowCenterY = screen.Min.Y + 22f * scale;
        Plugin.Device.SyncTarget();
        var localTime = CurrentTime();
        var timeSize = Typography.Measure(localTime, TimeScale, TimeWeight);
        var island = BaseIsland(screen);
        if (!landscape)
        {
            DeviceChrome.DrawIsland(island, theme);
        }

        var drawList = ImGui.GetWindowDrawList();
        var vertexStart = drawList.VtxBuffer.Size;
        var earGap = EarGap * scale;
        var earRight = island.Min.X - earGap;
        var edgeInset = (earRight - screen.Min.X - StatusIcons.MeasureWidth(scale, Plugin.Device.BatteryPercent)) * 0.5f;
        var timeLeft = MathF.Min(screen.Min.X + edgeInset, earRight - timeSize.X);
        Typography.Draw(new Vector2(timeLeft, rowCenterY - timeSize.Y * 0.5f), localTime, theme.TextStrong, TimeScale,
            TimeWeight);
        DrawIndicators(drawList, timeLeft + timeSize.X, earRight, rowCenterY, scale, theme);
        StatusIcons.Draw(screen, theme, rowCenterY, island.Max.X + earGap);
        LayerCompositor.Fade(drawList, vertexStart, Math.Clamp(alpha, 0f, 1f));
    }

    private static int IndicatorCount()
    {
        var configuration = Plugin.Cfg;
        var count = 0;
        if (configuration.DoNotDisturb)
        {
            count++;
        }

        if (configuration.LockPosition)
        {
            count++;
        }

        return count;
    }

    private static float IndicatorsWidth(float scale) => IndicatorCount() * (IndicatorGap + IndicatorHeight) * scale;

    private static void DrawIndicators(ImDrawListPtr drawList, float timeRight, float limit, float rowCenterY,
        float scale, PhoneTheme theme)
    {
        var configuration = Plugin.Cfg;
        var slot = (IndicatorGap + IndicatorHeight) * scale;
        var cursor = timeRight;
        if (configuration.DoNotDisturb && cursor + slot <= limit)
        {
            cursor = DrawIndicator(drawList, cursor, rowCenterY, scale, FontAwesomeIcon.Moon, DndTone);
        }

        if (configuration.LockPosition && cursor + slot <= limit)
        {
            DrawIndicator(drawList, cursor, rowCenterY, scale, FontAwesomeIcon.Lock, theme.TextStrong);
        }
    }

    private static float DrawIndicator(ImDrawListPtr drawList, float left, float rowCenterY, float scale,
        FontAwesomeIcon icon, Vector4 tone)
    {
        var center = new Vector2(left + (IndicatorGap + IndicatorHeight * 0.5f) * scale, rowCenterY);
        ProgressRing.CenterIcon(drawList, center, icon, tone, IndicatorHeight * scale);
        return left + (IndicatorGap + IndicatorHeight) * scale;
    }

    internal static Rect BaseIsland(Rect screen)
    {
        var scale = UiScale.Current;
        var timeWidth = Typography.Measure(CurrentTime(), TimeScale, TimeWeight).X + IndicatorsWidth(scale);
        var clusterWidth = StatusIcons.MeasureWidth(scale, Plugin.Device.BatteryPercent);
        return ComputeIsland(screen, scale, timeWidth, clusterWidth);
    }

    private static Rect ComputeIsland(Rect screen, float scale, float timeWidth, float clusterWidth)
    {
        var earGap = EarGap * scale;
        var sidePadding = IslandSidePadding * scale;
        var rightEarNeed = earGap + clusterWidth + sidePadding;
        var leftEarNeed = earGap + timeWidth + sidePadding;
        var maxHalfWidth = screen.Width * 0.5f - MathF.Max(rightEarNeed, leftEarNeed);
        var halfWidth = Math.Clamp(maxHalfWidth, MinIslandHalfWidth * scale, MaxIslandHalfWidth * scale);
        var top = screen.Min.Y + IslandTop * scale;
        var height = IslandHeight * scale;
        var centerX = screen.Center.X;
        var min = new Vector2(centerX - halfWidth, top);
        var max = new Vector2(centerX + halfWidth, top + height);
        return new Rect(min, max);
    }
}
