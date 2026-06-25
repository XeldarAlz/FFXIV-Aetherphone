using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal static class HomeScreen
{
    private const int Columns = 4;

    public static void Draw(Rect content, PhoneTheme theme, IReadOnlyList<IPhoneApp> apps, INavigator navigation)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = ImGui.GetIO().DeltaTime;
        var columnWidth = content.Width / Columns;
        var iconSize = MathF.Min(columnWidth * 0.58f, 58f * scale);
        var rowHeight = iconSize + 30f * scale;

        for (var index = 0; index < apps.Count; index++)
        {
            var column = index % Columns;
            var row = index / Columns;
            var centerX = content.Min.X + column * columnWidth + columnWidth * 0.5f;
            var topY = content.Min.Y + row * rowHeight + 10f * scale;
            var iconCenter = new Vector2(centerX, topY + iconSize * 0.5f);

            if (AppIcon.Draw(iconCenter, iconSize, apps[index], theme, delta))
            {
                navigation.OpenApp(apps[index]);
            }
        }
    }
}
