using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class SpinRingArt
{
    public const float RimInset = 14f;

    private static readonly Vector4 SmallFill = new(0.200f, 0.110f, 0.330f, 1f);
    private static readonly Vector4 MidFill = new(0.560f, 0.130f, 0.380f, 1f);
    private static readonly Vector4 HighFill = new(0.090f, 0.470f, 0.560f, 1f);
    private static readonly Vector4 Night = new(0.040f, 0.027f, 0.086f, 1f);
    private static readonly Vector4 LightInk = new(0.96f, 0.94f, 0.89f, 1f);

    private const int WedgeSegments = 12;
    private const float LabelRadiusFactor = 0.74f;
    private const float LabelPadding = 3f;
    private const float HubFactor = 0.30f;
    private const float PointerLength = 22f;
    private const float PointerWidth = 9f;
    private const float PointerSwing = 0.55f;

    public static float SegmentSpan => WheelChoreography.SpanFor(DailySpinRules.SegmentCount);

    public static Vector4 FillFor(int segment)
    {
        var award = DailySpinRules.AwardOf(segment);
        if (award >= DailySpinRules.TopAward)
        {
            return CasinoColors.Money;
        }

        if (award >= 30)
        {
            return HighFill;
        }

        return award > 5 ? MidFill : SmallFill;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        drawList.AddCircleFilled(center, radius + 5f * scale, ImGui.GetColorU32(Night), 64);
        var span = SegmentSpan;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var fill = Lit(segment, highlightSegment, highlightGlow);
            var centreAngle = rotation + segment * span;
            var from = centreAngle - span * 0.5f - MathF.PI * 0.5f;
            var to = centreAngle + span * 0.5f - MathF.PI * 0.5f;
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, to, WedgeSegments);
            drawList.PathFillConvex(ImGui.GetColorU32(fill));
            var edge = center + WheelRingArt.Direction(centreAngle - span * 0.5f) * radius;
            drawList.AddLine(center, edge, ImGui.GetColorU32(CasinoColors.Money with { W = 0.45f }),
                MathF.Max(1f, scale));
        }

        DrawLabels(drawList, center, radius * LabelRadiusFactor, rotation, highlightSegment, highlightGlow, scale);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = 0.8f }), 64,
            2f * scale);
        drawList.AddCircleFilled(center, radius * HubFactor, ImGui.GetColorU32(Night), 48);
        drawList.AddCircle(center, radius * HubFactor, ImGui.GetColorU32(CasinoColors.LightA with { W = 0.85f }), 48,
            2f * scale);
    }

    public static void DrawRim(ImDrawListPtr drawList, Vector2 center, float radius, float phase, float lit,
        float scale)
    {
        var rim = radius + RimInset * 0.6f * scale;
        var rect = new Rect(center - new Vector2(rim, rim), center + new Vector2(rim, rim));
        CasinoLights.BulbChase(drawList, rect, rim, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, lit);
    }

    public static void DrawPointer(ImDrawListPtr drawList, Vector2 center, float radius, float deflection,
        float scale)
    {
        var pivot = new Vector2(center.X, center.Y - radius - RimInset * scale);
        var swing = Math.Clamp(deflection, -1f, 1f) * PointerSwing;
        var down = new Vector2(MathF.Sin(-swing), MathF.Cos(-swing));
        var side = new Vector2(down.Y, -down.X);
        var tip = pivot + down * PointerLength * scale;
        var half = PointerWidth * 0.5f * scale;
        drawList.AddTriangleFilled(tip, pivot - side * half, pivot + side * half,
            ImGui.GetColorU32(CasinoColors.MoneyHighlight));
        drawList.AddTriangle(tip, pivot - side * half, pivot + side * half,
            ImGui.GetColorU32(CasinoColors.Money), MathF.Max(1f, scale));
        drawList.AddCircleFilled(pivot, 4.5f * scale, ImGui.GetColorU32(CasinoColors.LightA), 16);
        drawList.AddCircleFilled(pivot, 2f * scale, ImGui.GetColorU32(LightInk), 12);
    }

    private static Vector4 Lit(int segment, int highlightSegment, float highlightGlow)
    {
        var fill = FillFor(segment);
        if (segment != highlightSegment || highlightGlow <= 0f)
        {
            return fill;
        }

        return Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
    }

    private static void DrawLabels(ImDrawListPtr drawList, Vector2 center, float labelRadius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        var chord = 2f * labelRadius * MathF.Sin(SegmentSpan * 0.5f);
        var span = SegmentSpan;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var label = GameNumber.Label((int)DailySpinRules.AwardOf(segment));
            if (Typography.Measure(label, TextStyles.FootnoteEmphasized).X + LabelPadding * 2f * scale > chord)
            {
                continue;
            }

            var at = center + WheelRingArt.Direction(rotation + segment * span) * labelRadius;
            Typography.DrawCentered(drawList, at, label, WheelRingArt.InkOn(Lit(segment, highlightSegment,
                highlightGlow)), TextStyles.FootnoteEmphasized);
        }
    }
}
