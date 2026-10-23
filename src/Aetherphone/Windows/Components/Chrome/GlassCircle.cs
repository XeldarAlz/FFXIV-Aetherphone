using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class GlassCircle
{
    private const float DisabledAlpha = 0.45f;

    public static bool Draw(ImDrawListPtr drawList, uint key, Vector2 center, float radius, float scale,
        GlassTone tone, string tooltip, HoverLabelSide side, out float grow, Vector4? activeFill = null,
        bool enabled = true, float opacity = 1f, bool tapSound = true, float hitRadius = 0f, bool reserve = false)
    {
        var extent = new Vector2(MathF.Max(radius, hitRadius));
        var over = reserve
            ? UiInteract.HoverOverlay(new Rect(center - extent, center + extent))
            : UiInteract.Hover(center - extent, center + extent);
        var hovered = enabled && over;
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        grow = PressFx.Scale(key, down, PressFx.ControlPressedScale);
        var drawn = radius * grow;
        var min = center - new Vector2(drawn, drawn);
        var max = center + new Vector2(drawn, drawn);
        opacity *= enabled ? 1f : DisabledAlpha;
        if (activeFill is { } fill)
        {
            Material.AccentGlass(drawList, min, max, drawn, scale, fill, opacity);
        }
        else
        {
            Material.LiquidGlass(drawList, min, max, drawn, scale, tone, 0f, opacity);
        }

        if (!enabled)
        {
            return false;
        }

        if (tooltip.Length > 0)
        {
            HoverTooltip.Show(key, new Rect(center - extent, center + extent), tooltip, over, side);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered, tapSound);
    }

    public static bool Icon(ImDrawListPtr drawList, uint key, Vector2 center, float radius, float glyphSize,
        string glyph, Vector4 ink, float scale, GlassTone tone, string tooltip, HoverLabelSide side,
        bool enabled = true, float hitRadius = 0f, bool reserve = false)
    {
        var clicked = Draw(drawList, key, center, radius, scale, tone, tooltip, side, out var grow, null, enabled,
            hitRadius: hitRadius, reserve: reserve);
        var color = enabled ? ink : Palette.WithAlpha(ink, ink.W * DisabledAlpha);
        PhoneIcon.Draw(drawList, center, glyph, color, glyphSize * grow, optical: true);
        return clicked;
    }
}
