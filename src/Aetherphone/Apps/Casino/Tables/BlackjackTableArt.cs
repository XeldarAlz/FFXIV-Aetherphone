using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackTableArt
{
    public const int ChipColumnCapacity = 5;

    private const float ChipDiscStep = 4.2f;
    private const float PlateDiscRadius = 6.5f;
    private const float FlightDiscRadius = 7.5f;

    private static readonly Vector4 PlateFill = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 DiscShadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 DiscRim = new(0f, 0f, 0f, 0.28f);

    public static void DrawShoe(ImDrawListPtr drawList, Vector2 anchor, float scale)
    {
        var width = 22f * scale;
        var height = PlayingCards.HeightFor(width);
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var lift = new Vector2(0f, 2.5f * scale);
        var rounding = PlayingCards.RoundingFor(width);
        PlayingCards.DrawBack(drawList, new Rect(anchor - half + lift, anchor + half + lift), rounding, scale, false);
        PlayingCards.DrawBack(drawList, new Rect(anchor - half, anchor + half), rounding, scale, true);
    }

    public static void DrawTotalPill(ImDrawListPtr drawList, Vector2 center, string label, Vector4 fill, Vector4 ink,
        float scale)
    {
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var half = new Vector2(size.X * 0.5f + 7f * scale, MathF.Max(9f * scale, size.Y * 0.5f + 2.5f * scale));
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(fill));
        Typography.DrawCentered(drawList, center, label, ink, TextStyles.FootnoteEmphasized);
    }

    public static void DrawTotalCapsule(ImDrawListPtr drawList, Vector2 center, string label, Vector4 fill,
        Vector4 ink, bool active, float scale)
    {
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var half = new Vector2(size.X * 0.5f + 10f * scale, MathF.Max(11f * scale, size.Y * 0.5f + 3f * scale));
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, center - half, center + half, half.Y,
                ImGui.GetColorU32(Palette.WithAlpha(ink, 0.55f)), Metrics.Stroke.Thin * scale);
        }

        Typography.DrawCentered(drawList, center, label, ink, TextStyles.SubheadlineEmphasized);
    }

    public static void DrawOutcomeBadge(ImDrawListPtr drawList, Vector2 center, string label, Vector4 tint,
        Vector4 ink, float entrance, float maxWidth, float scale)
    {
        if (label.Length == 0 || entrance <= 0f)
        {
            return;
        }

        var padX = 8f * scale;
        label = Typography.FitText(label, MathF.Max(1f, maxWidth - padX * 2f), TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var half = new Vector2(size.X * 0.5f + padX, size.Y * 0.5f + 4f * scale);
        var bubble = BubblePop.For(entrance, scale, center);
        var min = bubble.Apply(center - half);
        var max = bubble.Apply(center + half);
        var rounding = (max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.22f * bubble.Alpha)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.55f * bubble.Alpha)), Metrics.Stroke.Hairline * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, Palette.WithAlpha(ink, bubble.Alpha),
            TextStyles.FootnoteEmphasized);
    }

    public static void DrawBetPlate(ImDrawListPtr drawList, Vector2 center, long amount, Vector4 ink, float entrance,
        bool practice, float maxWidth, float scale)
    {
        if (amount <= 0 || entrance <= 0f)
        {
            return;
        }

        var pad = 5f * scale;
        var label = NumberText.Compact(amount);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var discRadius = PlateDiscRadius * scale;
        var halfWidth = (discRadius * 2f + pad * 2.5f + size.X) * 0.5f;
        if (halfWidth * 2f > maxWidth)
        {
            DrawBareBetPlate(drawList, center, label, size, ink, entrance, maxWidth, pad, scale);
            return;
        }

        var halfHeight = MathF.Max(8.5f * scale, size.Y * 0.5f + 2.5f * scale);
        var bubble = BubblePop.For(entrance, scale, center);
        var min = bubble.Apply(new Vector2(center.X - halfWidth, center.Y - halfHeight));
        var max = bubble.Apply(new Vector2(center.X + halfWidth, center.Y + halfHeight));
        var alpha = bubble.Alpha;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(PlateFill, alpha)));
        var middleY = (min.Y + max.Y) * 0.5f;
        var discCenter = new Vector2(min.X + pad + discRadius, middleY);
        if (practice)
        {
            ChipStack.DrawPractice(drawList, discCenter, discRadius * bubble.Pop);
        }
        else
        {
            DrawDisc(drawList, discCenter, discRadius * bubble.Pop, TopChipColor(amount), alpha);
        }

        Typography.DrawCentered(drawList,
            new Vector2(min.X + pad * 1.5f + discRadius * 2f + size.X * 0.5f, middleY), label,
            Palette.WithAlpha(ink, alpha), TextStyles.FootnoteEmphasized);
    }

    private static void DrawBareBetPlate(ImDrawListPtr drawList, Vector2 center, string label, Vector2 size,
        Vector4 ink, float entrance, float maxWidth, float pad, float scale)
    {
        var shown = Typography.FitText(label, MathF.Max(1f, maxWidth - pad * 2f), TextStyles.FootnoteEmphasized);
        var shownWidth = MathF.Min(size.X, MathF.Max(1f, maxWidth - pad * 2f));
        var halfWidth = shownWidth * 0.5f + pad;
        var halfHeight = MathF.Max(8.5f * scale, size.Y * 0.5f + 2.5f * scale);
        var bubble = BubblePop.For(entrance, scale, center);
        var min = bubble.Apply(new Vector2(center.X - halfWidth, center.Y - halfHeight));
        var max = bubble.Apply(new Vector2(center.X + halfWidth, center.Y + halfHeight));
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(PlateFill, bubble.Alpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, shown, Palette.WithAlpha(ink, bubble.Alpha),
            TextStyles.FootnoteEmphasized);
    }

    public static void DrawChipColumn(ImDrawListPtr drawList, Vector2 baseCenter, long amount, bool practice,
        float scale)
    {
        Span<Vector4> colors = stackalloc Vector4[ChipColumnCapacity];
        var discs = ChipColumnDiscs(amount, colors);
        var radius = 7f * scale;
        for (var index = 0; index < discs; index++)
        {
            var center = new Vector2(baseCenter.X, baseCenter.Y - index * ChipDiscStep * scale);
            if (practice)
            {
                DrawDisc(drawList, center, radius, Stage.CasinoColors.Practice, 1f);
                ChipStack.DrawPractice(drawList, center, radius * 0.86f);
                continue;
            }

            DrawDisc(drawList, center, radius, colors[index], 1f);
        }
    }

    public static Vector4 DiscColor(long amount, bool practice)
    {
        return practice ? Stage.CasinoColors.Practice : TopChipColor(amount);
    }

    public static int ChipColumnDiscs(long amount, Span<Vector4> colors)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var discs = 0;
        var remaining = amount;
        for (var index = 0; index < ChipStack.Denominations.Length && discs < colors.Length; index++)
        {
            var denomination = ChipStack.Denominations[index];
            var count = remaining / denomination;
            if (count <= 0)
            {
                continue;
            }

            remaining -= count * denomination;
            for (var disc = 0L; disc < count && discs < colors.Length; disc++)
            {
                colors[discs] = ChipStack.ColorFor(denomination);
                discs++;
            }
        }

        return discs;
    }

    public static void DrawFlightDisc(ImDrawListPtr drawList, Vector2 from, Vector2 to, float progress, Vector4 color,
        float scale)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var center = BlackjackDealChoreography.Position(from, to, progress, scale);
        DrawDisc(drawList, center, FlightDiscRadius * scale, color, 1f);
    }

    public static void DrawActingGlow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Breath);
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(center, radius * (0.6f + layer * 0.30f),
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.035f * pulse * (4 - layer))), 40);
        }
    }

    public static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float alpha)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius,
            ImGui.GetColorU32(Palette.WithAlpha(DiscShadow, alpha)), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha)), 20);
        drawList.AddCircle(center, radius * 0.62f,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(color, 0.35f), 0.9f * alpha)), 20, radius * 0.16f);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(DiscRim, alpha)), 20, radius * 0.12f);
    }

    public static Vector4 TopChipColor(long amount)
    {
        for (var index = 0; index < ChipStack.Denominations.Length; index++)
        {
            if (amount >= ChipStack.Denominations[index])
            {
                return ChipStack.ColorFor(ChipStack.Denominations[index]);
            }
        }

        return ChipStack.ColorFor(1);
    }
}
