using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SceneChrome
{
    public static Rect ScreenFrom(Rect content, PhoneTheme theme, float scale)
    {
        var min = new Vector2(content.Min.X - theme.SidePadding * scale, content.Min.Y - theme.TopZoneHeight * scale);
        var max = new Vector2(content.Max.X + theme.SidePadding * scale, content.Max.Y + theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    public static void BackChevron(Rect content, INavigator navigation, Vector4 ink, float scale)
    {
        var rowCenterY = content.Min.Y + 20f * scale;
        var hitMin = new Vector2(content.Min.X, content.Min.Y);
        var hitMax = new Vector2(content.Min.X + 46f * scale, content.Min.Y + 40f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var tip = new Vector2(content.Min.X + 8f * scale, rowCenterY);
        var size = 7f * scale;
        var color = ImGui.GetColorU32(hovered ? ink : ink with { W = 0.82f });
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2(tip.X + size, tip.Y - size), tip, color, 2.4f * scale);
        drawList.AddLine(tip, new Vector2(tip.X + size, tip.Y + size), color, 2.4f * scale);

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Back();
        }
    }
}
