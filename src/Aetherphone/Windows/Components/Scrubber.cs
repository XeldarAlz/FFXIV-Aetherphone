using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class Scrubber
{
    public static float Draw(Rect track, float value, Vector4 accent, Vector4 rail, float alpha)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var midY = track.Center.Y;
        var left = track.Min.X;
        var width = track.Width;
        var thickness = track.Height;
        var result = Math.Clamp(value, 0f, 1f);
        if (IsHovered(track))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && width > 0f)
            {
                result = Math.Clamp((ImGui.GetMousePos().X - left) / width, 0f, 1f);
            }
        }

        var railMin = new Vector2(left, midY - thickness * 0.5f);
        var railMax = new Vector2(track.Max.X, midY + thickness * 0.5f);
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(Palette.WithAlpha(rail, alpha)), thickness * 0.5f);
        var knobX = left + width * result;
        drawList.AddRectFilled(railMin, new Vector2(knobX, railMax.Y),
            ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), thickness * 0.5f);
        drawList.AddCircleFilled(new Vector2(knobX, midY), thickness * 0.5f + 4f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), alpha)), 24);
        return result;
    }

    public static bool IsHovered(Rect track)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = track.Center.Y;
        var hitMin = new Vector2(track.Min.X - 6f * scale, midY - 14f * scale);
        var hitMax = new Vector2(track.Max.X + 6f * scale, midY + 14f * scale);
        return ImGui.IsMouseHoveringRect(hitMin, hitMax);
    }
}
