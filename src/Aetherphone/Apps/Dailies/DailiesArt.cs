using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Dailies;

internal static class DailiesArt
{
    public const float CheckRadius = 12f;

    private const float CheckStroke = 1.8f;
    private const float MarkStroke = 2.2f;
    private const float OutlineAlpha = 0.55f;
    private const float DiscFloor = 0.55f;
    private const float MarkDelay = 0.35f;
    private const float HoverWashBoost = 1.6f;
    private const int Segments = 32;

    private static readonly Vector4 MarkInk = new(1f, 1f, 1f, 1f);

    public static void Check(ImDrawListPtr drawList, Vector2 center, float radius, float fill, Vector4 accent,
        Vector4 muted, float scale)
    {
        if (fill < 1f)
        {
            var outline = Vector4.Lerp(Palette.WithAlpha(muted, OutlineAlpha), accent, fill);
            drawList.AddCircle(center, radius - CheckStroke * 0.5f * scale, ImGui.GetColorU32(outline), Segments,
                CheckStroke * scale);
        }

        if (fill <= 0f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius * (DiscFloor + (1f - DiscFloor) * fill),
            ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * fill)), Segments);
        var markAlpha = Math.Clamp((fill - MarkDelay) / (1f - MarkDelay), 0f, 1f);
        if (markAlpha <= 0f)
        {
            return;
        }

        var ink = ImGui.GetColorU32(Palette.WithAlpha(MarkInk, markAlpha));
        var start = center + new Vector2(-0.42f, 0.02f) * radius;
        var knee = center + new Vector2(-0.12f, 0.32f) * radius;
        var tip = center + new Vector2(0.42f, -0.30f) * radius;
        drawList.AddLine(start, knee, ink, MarkStroke * scale);
        drawList.AddLine(knee, tip, ink, MarkStroke * scale);
    }

    public static bool RoundButton(ImDrawListPtr drawList, uint id, Vector2 center, float radius, float hitRadius,
        FontAwesomeIcon icon, Vector4 glyphInk, Vector4 wash, float washAlpha, float glyphSize, string tooltip)
    {
        var hit = new Vector2(hitRadius, hitRadius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, pressed, PressFx.IconPressedScale);
        var alpha = hovered ? MathF.Min(1f, washAlpha * HoverWashBoost) : washAlpha;
        drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(Palette.WithAlpha(wash, alpha)), Segments);
        ProgressRing.CenterIcon(drawList, center, icon, glyphInk, glyphSize * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - hit, center + hit, hovered, false);
    }
}
