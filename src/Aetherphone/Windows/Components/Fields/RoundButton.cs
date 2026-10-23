using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class RoundButton
{
    public const float SmallRadius = 15f;
    public const float RegularRadius = Metrics.Size.GlassButton * 0.5f;

    private const float DisabledAlpha = 0.4f;
    private const float TintedAlpha = 0.20f;
    private const float TintedHoverAlpha = 0.28f;
    private const float RimLightAlpha = 0.5f;
    private const float GlyphFraction = 0.95f;
    private const float ProminentHoverLift = 0.10f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static bool Icon(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, in ControlInk ink,
        ButtonStyle style = ButtonStyle.Gray, string tooltip = "", HoverLabelSide side = HoverLabelSide.Above,
        bool enabled = true, bool overlay = false, Vector4? glyphInk = null)
    {
        var clicked = Draw(drawList, ImGui.GetID(tooltip.Length > 0 ? tooltip : glyph), center, radius, ink, style,
            enabled, overlay, out var face);
        var color = glyphInk ?? face.LabelInk;
        PhoneIcon.Draw(drawList, center, glyph, color with { W = face.LabelInk.W }, radius * GlyphFraction *
            (face.Face.Width / MathF.Max(radius * 2f, 0.0001f)));
        if (enabled && tooltip.Length > 0)
        {
            HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
                tooltip, side);
        }

        return clicked;
    }

    public static bool FontIcon(ImDrawListPtr drawList, string id, Vector2 center, float radius,
        FontAwesomeIcon icon, float glyphHeight, in ControlInk ink, ButtonStyle style = ButtonStyle.Gray,
        string tooltip = "", HoverLabelSide side = HoverLabelSide.Above, bool enabled = true,
        Vector4? glyphInk = null) =>
        FontIcon(drawList, ImGui.GetID(id), center, radius, icon, glyphHeight, ink, style, tooltip, side, enabled,
            glyphInk);

    public static bool FontIcon(ImDrawListPtr drawList, uint key, Vector2 center, float radius,
        FontAwesomeIcon icon, float glyphHeight, in ControlInk ink, ButtonStyle style = ButtonStyle.Gray,
        string tooltip = "", HoverLabelSide side = HoverLabelSide.Above, bool enabled = true,
        Vector4? glyphInk = null)
    {
        var clicked = Draw(drawList, key, center, radius, ink, style, enabled, false, out var face);
        var color = glyphInk ?? face.LabelInk;
        ProgressRing.CenterIcon(drawList, center, icon, color with { W = face.LabelInk.W },
            glyphHeight * (face.Face.Width / MathF.Max(radius * 2f, 0.0001f)));
        if (enabled && tooltip.Length > 0)
        {
            HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
                tooltip, side);
        }

        return clicked;
    }

    public static bool Draw(ImDrawListPtr drawList, uint key, Vector2 center, float radius, in ControlInk ink,
        ButtonStyle style, bool enabled, bool overlay, out ButtonFace face)
    {
        var extent = new Vector2(radius, radius);
        var rect = new Rect(center - extent, center + extent);
        var hovered = enabled && (overlay
            ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
            : UiInteract.Hover(rect.Min, rect.Max));
        face = Surface(drawList, rect, ink, style, enabled, hovered, key);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static ButtonFace Surface(ImDrawListPtr drawList, Rect rect, in ControlInk ink, ButtonStyle style,
        bool enabled, bool hovered, uint key, Vector4? fill = null)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var pose = enabled ? MotionButton.Animate(rect, (int)key, hovered, pressed) : new ButtonPose(rect, 0f, 0f, -1f);
        var face = pose.Face;
        var radius = face.Height * 0.5f;
        var alpha = enabled ? 1f : DisabledAlpha;
        Vector4 labelInk;
        if (fill is { } custom)
        {
            Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(Surfaces.Fill(ink, FillLevel.Secondary)));
            Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(custom with { W = custom.W * alpha }));
            labelInk = ink.Ink;
        }
        else
        {
            switch (style)
            {
                case ButtonStyle.Prominent:
                    Squircle.Fill(drawList, face.Min, face.Max, radius,
                        ImGui.GetColorU32(Palette.Mix(ink.Accent, White, ProminentHoverLift * pose.Hover) with
                        {
                            W = alpha,
                        }));
                    labelInk = White;
                    break;
                case ButtonStyle.Tinted:
                    Squircle.Fill(drawList, face.Min, face.Max, radius,
                        ImGui.GetColorU32(ink.Accent with
                        {
                            W = (TintedAlpha + (TintedHoverAlpha - TintedAlpha) * pose.Hover) * alpha,
                        }));
                    labelInk = ink.AccentInk;
                    break;
                case ButtonStyle.Gray:
                    var gray = Vector4.Lerp(Surfaces.Fill(ink, FillLevel.Secondary),
                        Surfaces.Fill(ink, FillLevel.Primary), pose.Hover);
                    Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(gray with { W = gray.W * alpha }));
                    labelInk = ink.Ink;
                    break;
                default:
                    if (pose.Hover > 0.001f)
                    {
                        var wash = Surfaces.Fill(ink, FillLevel.Tertiary);
                        Squircle.Fill(drawList, face.Min, face.Max, radius,
                            ImGui.GetColorU32(wash with { W = wash.W * pose.Hover }));
                    }

                    labelInk = ink.Ink;
                    break;
            }
        }

        if (style != ButtonStyle.Plain)
        {
            MotionButton.RimLight(drawList, face, radius, pose.Hover * RimLightAlpha * alpha);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new ButtonFace(face, labelInk with { W = labelInk.W * alpha }, hovered);
    }
}
