using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum TransportAction : byte
{
    Previous,
    Stop,
    Next,
    Play,
}

internal static class TransportButton
{
    public static bool Draw(Vector2 center, float radius, TransportAction action, Vector4 accent, Vector4 ink, float alpha, bool active, ImDrawListPtr? drawListOverride = null)
    {
        var drawList = drawListOverride ?? ImGui.GetWindowDrawList();
        var hovered = active && ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.20f * alpha)), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var color = ImGui.GetColorU32(Palette.WithAlpha(hovered ? accent : ink, alpha));
        var size = radius * 0.52f;
        switch (action)
        {
            case TransportAction.Previous:
                MediaGlyph.Previous(drawList, center, size, color);
                break;
            case TransportAction.Stop:
                MediaGlyph.Stop(drawList, center, size, color);
                break;
            case TransportAction.Next:
                MediaGlyph.Next(drawList, center, size, color);
                break;
            case TransportAction.Play:
                MediaGlyph.Play(drawList, center, size, color);
                break;
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
