using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ScrollLayout
{
    private const float GrabAlpha = 0.32f;
    private const float GrabHoverAlpha = 0.48f;
    private const float GrabActiveAlpha = 0.62f;

    public static ImRaii.ColorDisposable PushScrollbarInk(Vector4 ink)
    {
        var presence = AppSurface.IndicatorAlpha;
        return ImRaii.PushColor(ImGuiCol.ScrollbarBg, AppSkin.Transparent)
            .Push(ImGuiCol.ScrollbarGrab, Palette.WithAlpha(ink, GrabAlpha * presence))
            .Push(ImGuiCol.ScrollbarGrabHovered, Palette.WithAlpha(ink, GrabHoverAlpha * presence))
            .Push(ImGuiCol.ScrollbarGrabActive, Palette.WithAlpha(ink, GrabActiveAlpha));
    }

    public static float StableContentWidth()
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (DragScrollHost.Enabled)
        {
            return available;
        }

        return ImGui.GetScrollMaxY() > 0f ? available : available - ImGui.GetStyle().ScrollbarSize;
    }

    public static float NativeScrollContentWidth()
    {
        var available = ImGui.GetContentRegionAvail().X;
        return ImGui.GetScrollMaxY() > 0f ? available : available - ImGui.GetStyle().ScrollbarSize;
    }
}
