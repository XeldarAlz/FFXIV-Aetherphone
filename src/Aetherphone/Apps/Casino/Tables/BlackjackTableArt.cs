using Aetherphone.Apps.Casino.Stage;
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
    public const float TimeBankPipRadius = 2.2f;
    public const float TimeBankPipStep = 2.8f;
    public const float TimeBankPipPad = 0.8f;
    public const float OutcomeBadgePadX = 8f;
    public const float BetPlatePadX = 5f;

    private const float ChipDiscStep = 4.2f;
    private const float TotalPillPadX = 7f;
    private const float PlateDiscRadius = 6.5f;
    private const float FlightDiscRadius = 7.5f;
    private const float OutcomeBadgeStrokeAlpha = 0.8f;
    private const float SideStakePadX = 3f;
    private const float SideStakeRimAlpha = 0.55f;
    private const float SideStakePaidRimAlpha = 0.9f;
    private const float SideStakeLostRimAlpha = 0.3f;
    private const float TimeBankSpentAlpha = 0.35f;

    public static readonly Vector4 OutcomeBadgeFill = StageText.CapsuleFill with { W = 0.92f };

    private static readonly Vector4 PlateFill = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 DiscShadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 DiscRim = new(0f, 0f, 0f, 0.28f);

    public static void DrawShoe(ImDrawListPtr drawList, Vector2 anchor, float scale) =>
        DrawShoe(drawList, anchor, BlackjackTableLayout.ShoeCardWidth * scale, scale);

    public static void DrawShoe(ImDrawListPtr drawList, Vector2 anchor, float width, float scale)
    {
        var height = PlayingCards.HeightFor(width);
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var lift = new Vector2(0f, BlackjackTableLayout.ShoeLift * scale);
        var rounding = PlayingCards.RoundingFor(width);
        PlayingCards.DrawBack(drawList, new Rect(anchor - half + lift, anchor + half + lift), rounding, scale, false);
        PlayingCards.DrawBack(drawList, new Rect(anchor - half, anchor + half), rounding, scale, true);
    }

    public static float TotalPillHalfWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.FootnoteEmphasized).X * 0.5f + TotalPillPadX * scale;

    public static void DrawTotalPill(ImDrawListPtr drawList, Vector2 center, string label, Vector4 fill, Vector4 ink,
        float scale)
    {
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var half = new Vector2(size.X * 0.5f + TotalPillPadX * scale,
            MathF.Max(9f * scale, size.Y * 0.5f + 2.5f * scale));
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

        var style = TextStyles.FootnoteEmphasized;
        var fit = StageText.Squeeze(label, maxWidth, StageText.TightPadX * scale, OutcomeBadgePadX * scale, style,
            StageTextRole.Caption);
        var shown = StageText.Shown(label, fit, style.Weight);
        var lineHeight = Typography.Measure(shown, fit.Scale, style.Weight).Y;
        var half = new Vector2(fit.Width * 0.5f, lineHeight * 0.5f + 4f * scale);
        var bubble = BubblePop.For(entrance, scale, center);
        var min = bubble.Apply(center - half);
        var max = bubble.Apply(center + half);
        var rounding = (max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(OutcomeBadgeFill, OutcomeBadgeFill.W * bubble.Alpha)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, OutcomeBadgeStrokeAlpha * bubble.Alpha)),
            Metrics.Stroke.Hairline * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, shown, Palette.WithAlpha(ink, bubble.Alpha), fit.Scale,
            style.Weight);
    }

    public static void DrawBetPlate(ImDrawListPtr drawList, Vector2 center, long amount, Vector4 ink, float entrance,
        bool practice, float maxWidth, float scale)
    {
        if (amount <= 0 || entrance <= 0f)
        {
            return;
        }

        var pad = BetPlatePadX * scale;
        var label = NumberText.Compact(amount);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var discRadius = PlateDiscRadius * scale;
        var halfWidth = (discRadius * 2f + pad * 2.5f + size.X) * 0.5f;
        if (halfWidth * 2f > maxWidth)
        {
            DrawBareBetPlate(drawList, center, label, size.Y, ink, entrance, maxWidth, scale);
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

    private static void DrawBareBetPlate(ImDrawListPtr drawList, Vector2 center, string label, float lineHeight,
        Vector4 ink, float entrance, float maxWidth, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var fit = StageText.Squeeze(label, maxWidth, StageText.TightPadX * scale, BetPlatePadX * scale, style,
            StageTextRole.Caption);
        var halfWidth = fit.Width * 0.5f;
        var halfHeight = MathF.Max(8.5f * scale, lineHeight * 0.5f + 2.5f * scale);
        var bubble = BubblePop.For(entrance, scale, center);
        var min = bubble.Apply(new Vector2(center.X - halfWidth, center.Y - halfHeight));
        var max = bubble.Apply(new Vector2(center.X + halfWidth, center.Y + halfHeight));
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(PlateFill, bubble.Alpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, StageText.Shown(label, fit, style.Weight),
            Palette.WithAlpha(ink, bubble.Alpha), fit.Scale, style.Weight);
    }

    public static void DrawSideStake(ImDrawListPtr drawList, in Rect rect, string label, SideSpotResult result,
        float scale)
    {
        StageText.Capsule(drawList, rect.Min, rect.Max);
        var paid = result == SideSpotResult.Paid;
        var lost = result == SideSpotResult.Lost;
        var rim = paid
            ? Palette.WithAlpha(Stage.CasinoColors.Money, SideStakePaidRimAlpha)
            : lost
                ? Palette.WithAlpha(StageText.Muted, SideStakeLostRimAlpha)
                : Palette.WithAlpha(Stage.CasinoColors.LightA, SideStakeRimAlpha);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(rim),
            Metrics.Stroke.Hairline * scale);
        var inner = MathF.Max(1f, rect.Width - SideStakePadX * 2f * scale);
        var style = TextStyles.FootnoteEmphasized;
        var fit = StageText.FitScale(label, inner, style, StageTextRole.Caption);
        var ink = paid ? Stage.CasinoColors.Money : lost ? StageText.Muted : StageText.Strong;
        Typography.DrawCentered(drawList, rect.Center, Typography.FitText(label, inner, fit, style.Weight), ink, fit,
            style.Weight);
    }

    public static float TimeBankPipHalfHeight(float scale) => (TimeBankPipRadius + TimeBankPipPad) * scale;

    public static float TimeBankPipHalfWidth(int total, float scale)
    {
        var radius = TimeBankPipRadius * scale;
        return radius * TimeBankPipStep * (total - 1) * 0.5f + radius + TimeBankPipPad * scale;
    }

    public static void DrawTimeBankPips(ImDrawListPtr drawList, Vector2 center, int left, int total, float scale)
    {
        if (left <= 0 || total <= 0)
        {
            return;
        }

        var half = new Vector2(TimeBankPipHalfWidth(total, scale), TimeBankPipHalfHeight(scale));
        StageText.Capsule(drawList, center - half, center + half);
        var radius = TimeBankPipRadius * scale;
        var step = radius * TimeBankPipStep;
        var first = center.X - step * (total - 1) * 0.5f;
        var ready = ImGui.GetColorU32(Stage.CasinoColors.LightB);
        var spent = ImGui.GetColorU32(Palette.WithAlpha(Stage.CasinoColors.InkMuted, TimeBankSpentAlpha));
        for (var pip = 0; pip < total; pip++)
        {
            drawList.AddCircleFilled(new Vector2(first + pip * step, center.Y), radius, pip < left ? ready : spent,
                10);
        }
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
