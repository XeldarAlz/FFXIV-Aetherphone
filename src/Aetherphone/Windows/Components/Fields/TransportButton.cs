using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum TransportAction : byte
{
    Previous,
    Stop,
    Next,
    Play,
    Pause,
}

internal static class TransportButton
{
    public static bool Draw(Vector2 center, float radius, TransportAction action, Vector4 accent, Vector4 ink,
        float alpha, bool active, ImDrawListPtr? drawListOverride = null)
    {
        var drawList = drawListOverride ?? ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = active && Hovered(center, radius);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(PressId(action), pressed, PressFx.ControlPressedScale);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius * press,
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.20f * alpha)), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var color = ImGui.GetColorU32(Palette.WithAlpha(hovered ? accent : ink, alpha));
        var size = radius * 0.52f * press;
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
            case TransportAction.Pause:
                MediaGlyph.Pause(drawList, center, size, color);
                break;
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static bool Hovered(Vector2 center, float radius) =>
        UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

    private static string PressId(TransportAction action) => action switch
    {
        TransportAction.Previous => "transport.previous",
        TransportAction.Stop => "transport.stop",
        TransportAction.Next => "transport.next",
        TransportAction.Play => "transport.play",
        TransportAction.Pause => "transport.pause",
        _ => "transport",
    };
}
