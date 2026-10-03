using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class ControlTile
{
    private const float PressSlop = 6f;
    private const float InactiveGlyphAlpha = 0.9f;
    private const float GlyphFraction = 0.26f;
    private const float GlyphAnchor = 0.34f;
    private const float CircleGlyphFraction = 0.9f;
    private const float TransportGlyphFraction = 0.52f;
    private const float SliderGlyphFraction = 0.23f;
    private const float SliderGlyphInset = 0.30f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static Vector2 armedOrigin;
    private static bool armed;
    private static uint sliderCapture;

    public static void CancelPress()
    {
        armed = false;
        sliderCapture = 0u;
    }

    public static float Radius(float scale) => Metrics.Radius.Widget * scale;

    public static Vector4 Glyph(bool active, float opacity) =>
        White with { W = (active ? 1f : InactiveGlyphAlpha) * opacity };

    public static void Surface(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float opacity) =>
        Surface(drawList, rect, Radius(UiScale.Current), theme, opacity);

    public static void Surface(ImDrawListPtr drawList, Rect rect, float radius, PhoneTheme theme, float opacity) =>
        Material.LiquidGlass(drawList, rect.Min, rect.Max, radius, UiScale.Current, GlassTone.Dark,
            WallpaperLegibility.Strength(theme), opacity);

    public static bool Toggle(ImDrawListPtr drawList, string id, Rect rect, FontAwesomeIcon icon, string label,
        bool active, Vector4 accent, PhoneTheme theme, float opacity, bool interactive, bool showLabel = true)
    {
        var scale = UiScale.Current;
        var released = Release(rect, interactive, out _);
        var press = PressFx.Press(id, Pressed(rect, interactive), PressFx.CardPressedScale);
        var body = Shrink(rect, press);
        var radius = Radius(scale) * press;
        if (active)
        {
            Material.AccentGlass(drawList, body.Min, body.Max, radius, scale, accent, opacity);
        }
        else
        {
            Surface(drawList, body, radius, theme, opacity);
        }

        var labelMaxWidth = body.Width - 20f * scale;
        var lines = Typography.WrapText(label, TextStyles.FootnoteEmphasized, labelMaxWidth);
        var lineCount = lines.Length;
        var blockHeight = lineCount * Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var glyphRadius = MathF.Min(body.Height, body.Width) * GlyphFraction;
        var glyphBottomForFit = body.Min.Y + body.Height * GlyphAnchor + glyphRadius;
        var labelAvailableHeight = body.Max.Y - 6f * scale - (glyphBottomForFit + 4f * scale);
        var fitsLabel = showLabel && lineCount <= 2 && blockHeight <= labelAvailableHeight;
        var glyphColor = Glyph(active, opacity);
        var glyphCenter = fitsLabel
            ? new Vector2(body.Center.X, body.Min.Y + body.Height * GlyphAnchor)
            : body.Center;
        ProgressRing.CenterIcon(drawList, glyphCenter, icon, glyphColor, glyphRadius);
        if (fitsLabel)
        {
            var blockCenterY = body.Max.Y - 6f * scale - blockHeight * 0.5f;
            Typography.DrawWrappedCentered(drawList, new Vector2(body.Center.X, blockCenterY), label, glyphColor,
                TextStyles.FootnoteEmphasized, labelMaxWidth);
        }
        else
        {
            HoverTooltip.Show(rect, label, HoverLabelSide.Below);
        }

        return released;
    }

    public static float VerticalSlider(ImDrawListPtr drawList, string id, Rect rect, float value, FontAwesomeIcon icon,
        string label, PhoneTheme theme, float opacity, bool interactive, out bool released)
    {
        var scale = UiScale.Current;
        var key = ImGui.GetID(id);
        var result = Math.Clamp(value, 0f, 1f);
        released = false;
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                sliderCapture = key;
            }
        }

        if (sliderCapture == key)
        {
            if (interactive && ImGui.IsMouseDown(ImGuiMouseButton.Left) && rect.Height > 0f)
            {
                result = Math.Clamp(1f - (ImGui.GetMousePos().Y - rect.Min.Y) / rect.Height, 0f, 1f);
            }
            else
            {
                sliderCapture = 0u;
                released = true;
            }
        }

        var radius = Radius(scale);
        Surface(drawList, rect, radius, theme, opacity);
        var fillTop = rect.Min.Y + (1f - result) * rect.Height;
        if (fillTop < rect.Max.Y - 1f)
        {
            drawList.PushClipRect(new Vector2(rect.Min.X, fillTop), rect.Max, true);
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(theme.Accent with { W = opacity }));
            drawList.PopClipRect();
        }

        var glyphCenter = new Vector2(rect.Center.X, rect.Max.Y - rect.Width * SliderGlyphInset);
        ProgressRing.CenterIcon(drawList, glyphCenter, icon, Glyph(fillTop <= glyphCenter.Y, opacity),
            rect.Width * SliderGlyphFraction);
        HoverTooltip.Show(rect, label, HoverLabelSide.Below);
        return result;
    }

    public static bool Circle(ImDrawListPtr drawList, string id, Vector2 center, float radius, FontAwesomeIcon icon,
        bool active, Vector4 accent, PhoneTheme theme, float opacity, bool interactive, string? label = null,
        bool tapSound = true)
    {
        var drawRadius = CircleBody(drawList, id, center, radius, active, accent, theme, opacity, interactive,
            out var hovered);
        ProgressRing.CenterIcon(drawList, center, icon, Glyph(active, opacity), drawRadius * CircleGlyphFraction);
        return CircleTap(id, center, radius, hovered, label, tapSound);
    }

    public static bool Transport(ImDrawListPtr drawList, string id, Vector2 center, float radius,
        TransportAction action, PhoneTheme theme, float opacity, bool interactive)
    {
        var drawRadius = CircleBody(drawList, id, center, radius, false, theme.Accent, theme, opacity, interactive,
            out var hovered);
        var ink = ImGui.GetColorU32(Glyph(true, opacity));
        var size = drawRadius * TransportGlyphFraction;
        switch (action)
        {
            case TransportAction.Previous:
                MediaGlyph.Previous(drawList, center, size, ink);
                break;
            case TransportAction.Play:
                MediaGlyph.Play(drawList, center, size, ink);
                break;
            case TransportAction.Pause:
                MediaGlyph.Pause(drawList, center, size, ink);
                break;
            case TransportAction.Next:
                MediaGlyph.Next(drawList, center, size, ink);
                break;
            default:
                MediaGlyph.Stop(drawList, center, size, ink);
                break;
        }

        return CircleTap(id, center, radius, hovered, null, true);
    }

    public static bool Swatch(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, bool selected,
        float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var rect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var released = Release(rect, interactive, out var hovered);
        var grow = hovered ? 1.08f : 1f;
        drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(Palette.WithAlpha(color, opacity)), 32);
        if (selected)
        {
            drawList.AddCircle(center, radius * grow + 3f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f * opacity)), 32, 2f * scale);
        }

        return released;
    }

    private static float CircleBody(ImDrawListPtr drawList, string id, Vector2 center, float radius, bool active,
        Vector4 accent, PhoneTheme theme, float opacity, bool interactive, out bool hovered)
    {
        var scale = UiScale.Current;
        var half = new Vector2(radius, radius);
        hovered = interactive && UiInteract.Hover(center - half, center + half);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var drawRadius = radius * PressFx.Press(id, pressed, PressFx.IconPressedScale);
        var drawHalf = new Vector2(drawRadius, drawRadius);
        if (active)
        {
            Material.AccentGlass(drawList, center - drawHalf, center + drawHalf, drawRadius, scale, accent, opacity);
        }
        else
        {
            Surface(drawList, new Rect(center - drawHalf, center + drawHalf), drawRadius, theme, opacity);
        }

        return drawRadius;
    }

    private static bool CircleTap(string id, Vector2 center, float radius, bool hovered, string? label, bool tapSound)
    {
        var half = new Vector2(radius, radius);
        var min = center - half;
        var max = center + half;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (label is not null)
            {
                HoverTooltip.Show(id, new Rect(min, max), label, HoverLabelSide.Below);
            }
        }

        return UiInteract.Click(min, max, hovered, tapSound);
    }

    private static Rect Shrink(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = rect.Size * (0.5f * factor);
        return new Rect(center - half, center + half);
    }

    private static bool Pressed(Rect rect, bool interactive) =>
        interactive && armed && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
        UiInteract.Hover(rect.Min, rect.Max);

    private static bool Release(Rect rect, bool interactive, out bool hovered)
    {
        hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            armed = true;
            armedOrigin = ImGui.GetMousePos();
        }

        if (!hovered || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return false;
        }

        var scale = UiScale.Current;
        var moved = (ImGui.GetMousePos() - armedOrigin).Length() > PressSlop * scale;
        var fire = armed && !moved;
        armed = false;
        return fire;
    }
}
