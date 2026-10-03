using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Maps;

internal enum MapControl : byte
{
    None,
    Recenter,
    Overview,
}

internal static class MapChrome
{
    public const float ControlSize = Metrics.Size.TapTarget;
    public const float CircleSize = Metrics.Size.GlassButton;
    public const float TileHeight = 62f;
    private const float ControlGlyph = 20f;
    private const float CircleGlyph = 17f;
    private const float TileGlyph = 20f;
    private const float TileRadius = Metrics.Radius.Card;
    private const float TileLabelGap = 6f;
    private const float ChipHeight = 30f;
    private const float ChipPadding = 12f;
    private const float DividerInset = 9f;
    private const float DisabledAlpha = 0.38f;

    public static Rect ControlsRect(Rect screen, float top, float scale)
    {
        var size = ControlSize * scale;
        var right = screen.Max.X - Metrics.Space.GlassInset * scale;
        return new Rect(new Vector2(right - size, top), new Vector2(right, top + size * 2f));
    }

    public static MapControl Controls(ImDrawListPtr drawList, Rect capsule, bool following, PhoneTheme theme,
        float scale, string recenterTooltip, string overviewTooltip)
    {
        var radius = capsule.Width * 0.5f;
        Elevation.Card(drawList, capsule.Min, capsule.Max, radius, scale);
        Material.ThemedGlass(drawList, capsule.Min, capsule.Max, radius, scale, theme);
        var middle = capsule.Min.Y + capsule.Height * 0.5f;
        drawList.AddLine(new Vector2(capsule.Min.X + DividerInset * scale, middle),
            new Vector2(capsule.Max.X - DividerInset * scale, middle), ImGui.GetColorU32(theme.Separator),
            Metrics.Stroke.Hairline);
        var top = new Rect(capsule.Min, new Vector2(capsule.Max.X, middle));
        var bottom = new Rect(new Vector2(capsule.Min.X, middle), capsule.Max);
        var pressed = MapControl.None;
        if (ControlHalf(top, "maps.control.recenter", recenterTooltip, scale, out var recenterGrow))
        {
            pressed = MapControl.Recenter;
        }

        var recenterInk = following ? theme.Accent : theme.TextStrong;
        PhoneIcon.Draw(drawList, top.Center, following ? PhoneIcons.NavigationFilled : PhoneIcons.Navigation,
            recenterInk, ControlGlyph * scale * recenterGrow);
        if (ControlHalf(bottom, "maps.control.overview", overviewTooltip, scale, out var overviewGrow))
        {
            pressed = MapControl.Overview;
        }

        ProgressRing.CenterIcon(drawList, bottom.Center, FontAwesomeIcon.Expand, theme.TextStrong,
            ControlGlyph * 0.82f * scale * overviewGrow);
        return pressed;
    }

    private static bool ControlHalf(Rect half, string id, string tooltip, float scale, out float grow)
    {
        var hovered = UiInteract.Hover(half.Min, half.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        grow = PressFx.Scale(id, down, PressFx.IconPressedScale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(half, tooltip, HoverLabelSide.Below);
        }

        return UiInteract.Click(half.Min, half.Max, hovered);
    }

    public static bool Circle(ImDrawListPtr drawList, string id, Vector2 center, string glyph, Vector4 ink,
        PhoneTheme theme, float scale, string tooltip, bool glass = true)
    {
        var radius = CircleSize * 0.5f * scale;
        var hitRadius = MathF.Max(radius, ControlSize * 0.5f * scale);
        var hit = new Vector2(hitRadius, hitRadius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var drawn = new Vector2(radius * grow, radius * grow);
        if (glass)
        {
            Material.ThemedGlass(drawList, center - drawn, center + drawn, radius * grow, scale, theme);
        }
        else
        {
            var fill = hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.10f) : theme.SurfaceMuted;
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(fill), 32);
        }

        PhoneIcon.Draw(drawList, center, glyph, ink, CircleGlyph * scale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tooltip.Length > 0)
            {
                HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Below);
            }
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static bool Tile(ImDrawListPtr drawList, string id, Rect rect, string glyph, string label, Vector4 glyphInk,
        bool enabled, PhoneTheme theme, float scale)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var half = rect.Size * 0.5f * grow;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var fill = hovered ? Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.06f) : theme.GroupedCard;
        Squircle.Fill(drawList, min, max, TileRadius * scale * grow, ImGui.GetColorU32(fill));
        var alpha = enabled ? 1f : DisabledAlpha;
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        var glyphSize = TileGlyph * scale * grow;
        var block = glyphSize + TileLabelGap * scale + labelSize.Y;
        var glyphCenter = new Vector2(rect.Center.X, rect.Center.Y - block * 0.5f + glyphSize * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, Palette.WithAlpha(glyphInk, glyphInk.W * alpha), glyphSize);
        var labelText = Typography.FitText(label, rect.Width - Metrics.Space.Sm * 2f * scale, TextStyles.Footnote);
        var fitted = Typography.Measure(labelText, TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(rect.Center.X - fitted.X * 0.5f, glyphCenter.Y + glyphSize * 0.5f + TileLabelGap * scale),
            labelText, Palette.WithAlpha(theme.TextStrong, alpha), TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static void Chip(ImDrawListPtr drawList, Vector2 bottomLeft, string text, PhoneTheme theme, float scale)
    {
        var size = Typography.Measure(text, TextStyles.FootnoteEmphasized);
        var height = ChipHeight * scale;
        var min = new Vector2(bottomLeft.X, bottomLeft.Y - height);
        var max = new Vector2(bottomLeft.X + size.X + ChipPadding * 2f * scale, bottomLeft.Y);
        Elevation.Card(drawList, min, max, height * 0.5f, scale);
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, theme);
        Typography.Draw(drawList, new Vector2(min.X + ChipPadding * scale, min.Y + (height - size.Y) * 0.5f), text,
            theme.TextStrong, TextStyles.FootnoteEmphasized);
    }
}
