using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class WallpaperStrip
{
    public static int Draw(Rect row, string label, IReadOnlyList<WallpaperStyle> options, int selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = Typography.Measure(label);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong);

        var thumbHeight = row.Height - 12f * scale;
        var thumbWidth = thumbHeight * 0.6f;
        var gap = 9f * scale;
        var step = thumbWidth + gap;
        var startX = row.Max.X - (options.Count * step - gap);
        var rounding = 5f * scale;
        var result = selected;

        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < options.Count; index++)
        {
            var min = new Vector2(startX + index * step, row.Center.Y - thumbHeight * 0.5f);
            var max = new Vector2(min.X + thumbWidth, min.Y + thumbHeight);

            dl.AddImageRounded(Plugin.Wallpapers.Handle(options[index]), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);

            dl.AddRect(min, max, ImGui.GetColorU32(index == selected ? theme.TextStrong : theme.Separator), rounding);
            if (index == selected)
            {
                dl.AddRect(min - new Vector2(1.5f, 1.5f), max + new Vector2(1.5f, 1.5f), ImGui.GetColorU32(theme.TextStrong), rounding);
            }

            if (!ImGui.IsMouseHoveringRect(min, max))
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                result = index;
            }
        }

        return result;
    }
}
