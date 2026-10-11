using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Plinko;

internal static class PlinkoArt
{
    public static readonly Vector4 ColdLow = new(0.13f, 0.30f, 0.72f, 1f);
    public static readonly Vector4 ColdHigh = new(0.47f, 0.70f, 1f, 1f);
    public static readonly Vector4 Even = new(0.93f, 0.93f, 0.96f, 1f);
    public static readonly Vector4 Warm = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 Peg = new(0.80f, 0.78f, 0.90f, 1f);
    public static readonly Vector4 BallFill = new(1f, 0.93f, 0.74f, 1f);

    private const float PegGlowScale = 2.6f;
    private const float PegGlowAlpha = 0.45f;
    private const float BallHaloScale = 2.1f;
    private const float BallHaloAlpha = 0.28f;
    private const float TrailWidth = 0.7f;
    private const float SlotFillAlpha = 0.88f;
    private const float LegendGap = 4f;
    private const float LeaderAlpha = 0.7f;

    private static readonly TextStyle LabelStyle = TextStyles.FootnoteEmphasized;
    private const float PopRise = 1.7f;
    private const float EdgeGlowAlpha = 0.55f;
    private const float LabelPad = 3f;
    private const int CircleSegments = 20;

    public static Vector4 SlotTint(int tenths, int topTenths)
    {
        if (tenths < PlinkoRules.TenthsPerMultiple)
        {
            var cold = Math.Clamp(tenths / (float)PlinkoRules.TenthsPerMultiple, 0f, 1f);
            return Vector4.Lerp(ColdLow, ColdHigh, cold);
        }

        if (tenths == PlinkoRules.TenthsPerMultiple || topTenths <= PlinkoRules.TenthsPerMultiple)
        {
            return Even;
        }

        var span = MathF.Log(topTenths / (float)PlinkoRules.TenthsPerMultiple);
        var heat = Math.Clamp(MathF.Log(tenths / (float)PlinkoRules.TenthsPerMultiple) / span, 0f, 1f);
        if (heat < 0.5f)
        {
            return Vector4.Lerp(Warm, CasinoColors.Money, heat * 2f);
        }

        return Vector4.Lerp(CasinoColors.Money, CasinoColors.LightA, (heat - 0.5f) * 2f);
    }

    public static void Pegs(ImDrawListPtr drawList, in PlinkoBoardLayout layout, PlinkoBoardFx fx, float scale)
    {
        var radius = MathF.Max(1.5f * scale, layout.PegRadius);
        var half = new Vector2(radius, radius);
        for (var row = 0; row < layout.Rows; row++)
        {
            var pegs = PlinkoBoardLayout.PegsInRow(row);
            for (var column = 0; column < pegs; column++)
            {
                var center = layout.PegCenter(row, column);
                var glow = fx.PegGlow(row, column);
                if (glow > 0f)
                {
                    drawList.AddCircleFilled(center, radius * PegGlowScale,
                        ImGui.GetColorU32(CasinoColors.LightB with { W = PegGlowAlpha * glow }), CircleSegments);
                }

                var fill = Vector4.Lerp(Peg, CasinoColors.LightB, glow);
                StageCell.Draw(drawList, new Rect(center - half, center + half), fill, CellDepth.Raised, radius, scale);
            }
        }
    }

    public static void Slots(ImDrawListPtr drawList, in PlinkoBoardLayout layout, ReadOnlySpan<int> strip,
        PlinkoBoardFx fx, float phase, float scale)
    {
        if (strip.Length == 0)
        {
            return;
        }

        var top = strip[0];
        var edgeGlow = fx.Edge;
        for (var slot = 0; slot < strip.Length; slot++)
        {
            var rect = layout.SlotRect(slot);
            var squash = fx.SlotSquash(slot);
            var width = rect.Width * (1f + squash * 0.5f);
            var height = rect.Height * (1f - squash);
            var min = new Vector2(rect.Center.X - width * 0.5f, rect.Max.Y - height);
            var max = new Vector2(rect.Center.X + width * 0.5f, rect.Max.Y);
            var tint = SlotTint(strip[slot], top);
            var rounding = MathF.Min(height, width) * 0.32f;
            var edge = slot == 0 || slot == strip.Length - 1;
            if (edge && edgeGlow > 0f)
            {
                var pulse = 0.6f + 0.4f * MathF.Sin(phase * 14f);
                var grow = new Vector2(4f * scale, 4f * scale);
                Squircle.Fill(drawList, min - grow, max + grow, rounding + grow.X,
                    ImGui.GetColorU32(CasinoColors.LightA with { W = EdgeGlowAlpha * edgeGlow * pulse }));
            }

            Squircle.FillVerticalGradient(drawList, min, max, rounding,
                ImGui.GetColorU32(Vector4.Lerp(tint, Vector4.One, 0.18f) with { W = SlotFillAlpha }),
                ImGui.GetColorU32(Vector4.Lerp(tint, Vector4.Zero, 0.25f) with { W = SlotFillAlpha }));
            var label = CasinoMultiples.Label(strip[slot] * PlinkoRules.TenthsPerMultiple);
            if (!layout.HasLegend)
            {
                Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), label,
                    GamePalette.InkOn(tint), LabelStyle);
                continue;
            }

            var tier = slot & 1;
            var lineHeight = Typography.LineHeight(LabelStyle);
            var labelCenter = new Vector2(rect.Center.X,
                layout.SlotsBottom + LegendGap * scale + lineHeight * (tier + 0.5f));
            if (tier == 1)
            {
                drawList.AddLine(new Vector2(rect.Center.X, rect.Max.Y + LegendGap * 0.5f * scale),
                    new Vector2(rect.Center.X, labelCenter.Y - lineHeight * 0.5f),
                    ImGui.GetColorU32(tint with { W = LeaderAlpha }), MathF.Max(1f, scale));
            }

            Typography.DrawCentered(drawList, labelCenter, label, LegendInk(strip[slot]), LabelStyle);
        }
    }

    public static float LegendHeight(float scale) => Typography.LineHeight(LabelStyle) * 2f + LegendGap * 2f * scale;

    public static bool LabelsFitInside(ReadOnlySpan<int> strip, float slotWidth, float scale)
    {
        var room = slotWidth - LabelPad * 2f * scale;
        for (var slot = 0; slot < strip.Length; slot++)
        {
            var label = CasinoMultiples.Label(strip[slot] * PlinkoRules.TenthsPerMultiple);
            if (Typography.Measure(label, LabelStyle).X > room)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector4 LegendInk(int tenths) =>
        tenths > PlinkoRules.TenthsPerMultiple ? CasinoColors.MoneyHighlight : StageInks.Strong;

    public static void Pops(ImDrawListPtr drawList, in PlinkoBoardLayout layout, ReadOnlySpan<int> strip,
        PlinkoBoardFx fx)
    {
        for (var slot = 0; slot < strip.Length; slot++)
        {
            var pop = fx.SlotPop(slot);
            if (pop <= 0f)
            {
                continue;
            }

            var rect = layout.SlotRect(slot);
            var rise = (1f - pop) * layout.Pitch * PopRise;
            var style = TextStyles.FootnoteEmphasized;
            var won = fx.SlotWon(slot);
            var grow = won ? 1f + 0.35f * MathF.Sin(MathF.PI * Math.Clamp((1f - pop) * 3f, 0f, 1f)) : 1f;
            var ink = won ? CasinoColors.Money : CasinoColors.InkMuted;
            Typography.DrawCentered(drawList, new Vector2(rect.Center.X, rect.Min.Y - rise - layout.Pitch * 0.4f),
                CasinoMultiples.Label(strip[slot] * PlinkoRules.TenthsPerMultiple), ink with { W = MathF.Min(1f, pop * 1.6f) },
                style.Scale * grow, style.Weight);
        }
    }

    public static void Ball(ImDrawListPtr drawList, Vector2 center, float radius, Ribbon trail)
    {
        trail.Draw(drawList, CasinoColors.MoneyHighlight with { W = 0.55f }, radius * TrailWidth * 2f, true);
        drawList.AddCircleFilled(center, radius * BallHaloScale,
            ImGui.GetColorU32(CasinoColors.Money with { W = BallHaloAlpha }), CircleSegments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BallFill), CircleSegments);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f, radius * 0.34f), radius * 0.36f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)), 12);
    }
}
