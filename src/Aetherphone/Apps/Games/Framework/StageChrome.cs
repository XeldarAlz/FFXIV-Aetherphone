using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal static class StageChrome
{
    public static bool BackChip(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, float scale) =>
        Chip(drawList, center, radius, FontAwesomeIcon.ChevronLeft, theme, scale);

    public static bool PauseChip(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, float scale,
        bool paused) =>
        Chip(drawList, center, radius, paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, theme, scale);

    private static bool Chip(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        PhoneTheme theme, float scale)
    {
        var corner = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - corner, center + corner);
        Material.Frosted(drawList, center - corner, center + corner, radius, scale, hovered ? 1f : 0.85f);
        ProgressRing.CenterIcon(drawList, center, icon, hovered ? theme.TextStrong : theme.Accent, radius * 0.9f);
        return UiInteract.HoverClickCircle(center, radius);
    }
}
