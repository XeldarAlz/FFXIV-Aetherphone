using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet.Kit;

internal readonly record struct VTabDef(FontAwesomeIcon Icon, string Label, int Badge = 0);

internal static class VTabBar
{
    public const float Height = 66f;

    public static int Draw(Rect area, ReadOnlySpan<VTabDef> tabs, int active, float scale)
    {
        var count = tabs.Length;
        if (count <= 0)
        {
            return -1;
        }

        var drawList = ImGui.GetWindowDrawList();
        var margin = 12f * scale;
        var barMin = new Vector2(area.Min.X + margin, area.Min.Y + 6f * scale);
        var barMax = new Vector2(area.Max.X - margin, area.Max.Y - 12f * scale);
        var radius = (barMax.Y - barMin.Y) * 0.5f;
        Elevation.Floating(drawList, barMin, barMax, radius, scale, 0.5f);
        Squircle.Fill(drawList, barMin, barMax, radius, VelvetTheme.CardHi.Packed());
        Squircle.Stroke(drawList, barMin, barMax, radius, VelvetTheme.CardStroke.Packed(), 1f * scale);

        var cellWidth = (barMax.X - barMin.X) / count;
        var clicked = -1;
        for (var index = 0; index < count; index++)
        {
            var cellMin = new Vector2(barMin.X + index * cellWidth, barMin.Y);
            var cellMax = new Vector2(barMin.X + (index + 1) * cellWidth, barMax.Y);
            var centerX = (cellMin.X + cellMax.X) * 0.5f;
            var centerY = (cellMin.Y + cellMax.Y) * 0.5f;
            var isActive = index == active;
            var hovered = UiInteract.Hover(cellMin, cellMax);
            if (isActive || hovered)
            {
                var pillMin = new Vector2(cellMin.X + 8f * scale, cellMin.Y + 7f * scale);
                var pillMax = new Vector2(cellMax.X - 8f * scale, cellMax.Y - 7f * scale);
                var pill = isActive
                    ? VelvetTheme.Alpha(VelvetTheme.Rose, 0.20f)
                    : VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.08f);
                Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, pill.Packed());
            }

            var ink = isActive ? VelvetTheme.RoseInk : hovered ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
            AppSkin.Icon(new Vector2(centerX, centerY), tabs[index].Icon.ToIconString(), ink, isActive ? 1.1f : 0.98f);
            if (tabs[index].Badge > 0)
            {
                VBadge.Count(drawList, new Vector2(centerX + 20f * scale, centerY - 12f * scale), tabs[index].Badge);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = index;
                }
            }
        }

        return clicked;
    }
}
