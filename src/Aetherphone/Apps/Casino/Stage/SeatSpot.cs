using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class SeatSpot
{
    public const float MinimumRadius = 28f;
    public const float PlusArm = 7f;
    public const float PlusStroke = 2.4f;
    public const float RingStroke = 2f;
    public const float GlowReach = 12f;

    private const int GlowLayers = 4;
    private const int Segments = 40;
    private const float LabelGap = 3f;

    public static float RadiusFor(float requested, float scale) => MathF.Max(requested, MinimumRadius * scale);

    public static bool DrawEmpty(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, bool invite,
        float scale) =>
        DrawEmpty(drawList, center, radius, Loc.T(L.Strip.SeatSit), accent, invite, scale);

    public static bool DrawEmpty(ImDrawListPtr drawList, Vector2 center, float radius, string label, Vector4 accent,
        bool invite, float scale)
    {
        radius = RadiusFor(radius, scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max) && Vector2.DistanceSquared(ImGui.GetMousePos(), center) <= radius * radius;
        if (invite)
        {
            DrawGlow(drawList, center, radius, accent, Pulse.Wave(Pulse.Breath), scale);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(StageText.CapsuleFill), Segments);
        var ring = hovered ? CasinoColors.MoneyHighlight : accent;
        drawList.AddCircle(center, radius - RingStroke * scale * 0.5f,
            ImGui.GetColorU32(ring with { W = invite || hovered ? 0.9f : 0.5f }), Segments, RingStroke * scale);
        var style = StageText.Resolve(StageTextRole.Label, TextStyles.FootnoteEmphasized);
        var labelSize = label.Length > 0 ? Typography.Measure(label, style) : Vector2.Zero;
        var arm = PlusArm * scale;
        var stack = label.Length > 0 ? arm * 2f + LabelGap * scale + labelSize.Y : arm * 2f;
        var plusCenter = new Vector2(center.X, center.Y - stack * 0.5f + arm);
        var ink = ImGui.GetColorU32(StageText.Strong);
        var stroke = PlusStroke * scale;
        drawList.AddLine(plusCenter - new Vector2(arm, 0f), plusCenter + new Vector2(arm, 0f), ink, stroke);
        drawList.AddLine(plusCenter - new Vector2(0f, arm), plusCenter + new Vector2(0f, arm), ink, stroke);
        DrawLabel(drawList, center.X, plusCenter.Y + arm + LabelGap * scale, label, labelSize, radius * 1.7f, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static void DrawLabel(ImDrawListPtr drawList, float centerX, float top, string label, Vector2 size,
        float width, in TextStyle style)
    {
        if (label.Length == 0)
        {
            return;
        }

        var shown = StageText.Fits(size.X, width) ? label : Typography.FitText(label, width, style);
        var shownWidth = MathF.Min(size.X, width);
        Typography.Draw(drawList, new Vector2(centerX - shownWidth * 0.5f, top), shown, StageText.Strong, style);
    }

    private static void DrawGlow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float pulse,
        float scale)
    {
        var reach = GlowReach * scale * (0.6f + 0.4f * pulse);
        for (var layer = GlowLayers; layer >= 1; layer--)
        {
            var share = layer / (float)GlowLayers;
            drawList.AddCircleFilled(center, radius + reach * share,
                ImGui.GetColorU32(color with { W = 0.10f * (1.2f - share) * (0.5f + 0.5f * pulse) }), Segments);
        }
    }
}
