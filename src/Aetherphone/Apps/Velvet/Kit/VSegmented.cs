using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VSegmented
{
    public static int Draw(string id, Rect rect, ReadOnlySpan<string> labels, int current, float scale)
    {
        var count = labels.Length;
        if (count <= 0)
        {
            return -1;
        }

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, VelvetTheme.PlumWell.Packed());

        var segmentWidth = rect.Width / count;
        var pad = 3f * scale;
        var thumb = VAnim.To(id + ".thumb", current, 0.14f, ImGui.GetIO().DeltaTime);
        var thumbMin = new Vector2(rect.Min.X + pad + thumb * segmentWidth, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMin.X + segmentWidth - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (rect.Height - pad * 2f) * 0.5f,
            VelvetTheme.Alpha(VelvetTheme.Rose, 0.28f).Packed());

        var clicked = -1;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(rect.Min.X + index * segmentWidth, rect.Min.Y);
            var max = new Vector2(rect.Min.X + (index + 1) * segmentWidth, rect.Max.Y);
            var hovered = UiInteract.Hover(min, max);
            var ink = index == current ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
            Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), labels[index], ink,
                TextStyles.FootnoteEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                clicked = index;
            }
        }

        return clicked;
    }
}
