using System.Numerics;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal static class StatusBar
{
    private const float TimeScale = 0.95f;
    private const FontWeight TimeWeight = FontWeight.SemiBold;
    private const float TimePadding = 24f;
    private const float EarGap = 10f;
    private const float IslandSidePadding = 14f;
    private const float MinIslandHalfWidth = 30f;
    private const float MaxIslandHalfWidth = 49f;
    private const float IslandHeight = 26f;
    private const float IslandTop = 9f;

    private static string cachedTime = string.Empty;
    private static int cachedTimeKey = -1;

    private static string CurrentTime()
    {
        var now = DateTime.Now;
        var key = now.Hour * 60 + now.Minute;
        if (key != cachedTimeKey)
        {
            cachedTimeKey = key;
            cachedTime = now.ToString("HH:mm");
        }

        return cachedTime;
    }

    public static void Draw(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = screen.Min.Y + 22f * scale;

        Plugin.Device.SyncTarget();

        var localTime = CurrentTime();
        var timeSize = Typography.Measure(localTime, TimeScale, TimeWeight);

        var island = BaseIsland(screen);
        DeviceChrome.DrawIsland(island, theme);

        var earGap = EarGap * scale;

        var timeLeft = MathF.Min(screen.Min.X + TimePadding * scale, island.Min.X - earGap - timeSize.X);
        Typography.Draw(new Vector2(timeLeft, rowCenterY - timeSize.Y * 0.5f), localTime, theme.TextStrong, TimeScale, TimeWeight);

        StatusIcons.Draw(screen, theme, rowCenterY, island.Max.X + earGap);
    }

    internal static Rect BaseIsland(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var timeWidth = Typography.Measure(CurrentTime(), TimeScale, TimeWeight).X;
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
