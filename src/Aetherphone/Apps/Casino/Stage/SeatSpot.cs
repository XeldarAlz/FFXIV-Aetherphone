using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct SeatPuck
{
    public readonly Vector2 Center;
    public readonly float Radius;
    public readonly bool Hovered;
    public readonly bool Clicked;

    public SeatPuck(Vector2 center, float radius, bool hovered, bool clicked)
    {
        Center = center;
        Radius = radius;
        Hovered = hovered;
        Clicked = clicked;
    }

    public Rect Bounds => new(Center - new Vector2(Radius, Radius), Center + new Vector2(Radius, Radius));
}

internal static class SeatSpot
{
    public const float MinimumRadius = 28f;
    public const float PlusArm = 7f;
    public const float PlusStroke = 2.4f;
    public const float RingStroke = 2f;
    public const float PulseHertz = 0.8f;
    public const float GlowReach = 12f;
    public const float PuckInset = 4f;

    private const int GlowLayers = 4;
    private const int Segments = 40;
    private const float LabelGap = 3f;

    public static float RadiusFor(float requested, float scale) => MathF.Max(requested, MinimumRadius * scale);

    public static float Pulse(float phase) => 0.5f + 0.5f * MathF.Sin(phase * MathF.Tau * PulseHertz);

    public static bool DrawEmpty(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, bool invite,
        float phase, float scale)
    {
        radius = RadiusFor(radius, scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max) && Vector2.DistanceSquared(ImGui.GetMousePos(), center) <= radius * radius;
        if (invite)
        {
            DrawGlow(drawList, center, radius, accent, Pulse(phase), scale);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(StageText.CapsuleFill), Segments);
        var ring = hovered ? CasinoColors.MoneyHighlight : accent;
        drawList.AddCircle(center, radius - RingStroke * scale * 0.5f, ImGui.GetColorU32(ring with { W = 0.9f }),
            Segments, RingStroke * scale);
        var label = Loc.T(L.Strip.SeatSit);
        var style = StageText.Resolve(StageTextRole.Label, TextStyles.FootnoteEmphasized);
        var labelSize = Typography.Measure(label, style);
        var arm = PlusArm * scale;
        var stack = arm * 2f + LabelGap * scale + labelSize.Y;
        var plusCenter = new Vector2(center.X, center.Y - stack * 0.5f + arm);
        var ink = ImGui.GetColorU32(StageText.Strong);
        var stroke = PlusStroke * scale;
        drawList.AddLine(plusCenter - new Vector2(arm, 0f), plusCenter + new Vector2(arm, 0f), ink, stroke);
        drawList.AddLine(plusCenter - new Vector2(0f, arm), plusCenter + new Vector2(0f, arm), ink, stroke);
        var labelWidth = radius * 1.7f;
        var labelTop = plusCenter.Y + arm + LabelGap * scale;
        if (StageText.Fits(labelSize.X, labelWidth))
        {
            Typography.Draw(drawList, new Vector2(center.X - labelSize.X * 0.5f, labelTop), label, StageText.Strong,
                style);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(center.X - labelWidth * 0.5f, labelTop),
                Typography.FitText(label, labelWidth, style), StageText.Strong, style);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static SeatPuck DrawOccupied(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 ring,
        bool highlight, float phase, float scale)
    {
        radius = RadiusFor(radius, scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max) && Vector2.DistanceSquared(ImGui.GetMousePos(), center) <= radius * radius;
        if (highlight)
        {
            DrawGlow(drawList, center, radius, ring, Pulse(phase), scale);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(StageText.CapsuleFill), Segments);
        drawList.AddCircle(center, radius - RingStroke * scale * 0.5f, ImGui.GetColorU32(ring), Segments,
            RingStroke * scale);
        var clicked = UiInteract.Click(min, max, hovered);
        return new SeatPuck(center, MathF.Max(0f, radius - (RingStroke + PuckInset) * scale), hovered, clicked);
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
