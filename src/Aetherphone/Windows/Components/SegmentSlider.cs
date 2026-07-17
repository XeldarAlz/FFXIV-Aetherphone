using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SegmentSlider
{
    public static int Draw(Rect rect, string leftLabel, string rightLabel, int selected, ref float animation,
        Vector4 accent, Vector4 mutedInk)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        var target = selected == 1 ? 1f : 0f;
        animation += (target - animation) * MathF.Min(1f, ImGui.GetIO().DeltaTime * 14f);
        var scale = ImGuiHelpers.GlobalScale;
        var half = rect.Width * 0.5f;
        var pad = 3f * scale;
        var thumbMinX = rect.Min.X + pad + animation * half;
        var thumbMin = new Vector2(thumbMinX, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMinX + half - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (thumbMax.Y - thumbMin.Y) * 0.5f, ImGui.GetColorU32(accent));
        var leftRect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
        var rightRect = new Rect(new Vector2(rect.Min.X + half, rect.Min.Y), rect.Max);
        DrawSegmentLabel(leftRect, leftLabel, selected == 0, mutedInk);
        DrawSegmentLabel(rightRect, rightLabel, selected == 1, mutedInk);
        var result = selected;
        if (UiInteract.HoverClick(leftRect.Min, leftRect.Max))
        {
            result = 0;
        }

        if (UiInteract.HoverClick(rightRect.Min, rightRect.Max))
        {
            result = 1;
        }

        return result;
    }

    private static void DrawSegmentLabel(Rect rect, string label, bool active, Vector4 mutedInk)
    {
        var ink = active ? new Vector4(1f, 1f, 1f, 1f) : mutedInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.9f, active ? FontWeight.SemiBold : FontWeight.Medium);
    }
}
