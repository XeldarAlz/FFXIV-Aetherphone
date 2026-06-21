using System.Numerics;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal static class StatusBar
{
    public static void Draw(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = screen.Min.Y + 22f * scale;

        var localTime = DateTime.Now.ToString("HH:mm");
        var localSize = Typography.Measure(localTime, 0.95f);
        Typography.Draw(new Vector2(screen.Min.X + 24f * scale, rowCenterY - localSize.Y * 0.5f), localTime, theme.TextStrong, 0.95f);

        Plugin.Device.SyncTarget();
        StatusIcons.Draw(screen, theme, rowCenterY);
    }
}
