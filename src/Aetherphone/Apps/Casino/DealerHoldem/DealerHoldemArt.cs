using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal static class DealerHoldemArt
{
    public const float PrintAlpha = 0.62f;
    public const float RingAlpha = 0.45f;
    public const float LitRingAlpha = 0.95f;
    public const float DiscRadius = 9f;
    public const float DiscStep = 3.4f;
    public const int MaxDiscs = 5;

    private const int RingSegments = 48;
    private const float TableRowGap = 1f;
    private const float GlowGrowth = 5f;

    private static readonly Vector4 CircleFill = new(0f, 0f, 0f, 0.18f);
    private static readonly Vector4 Glow = CasinoColors.Money with { W = 0.22f };

    public static void PayTable(ImDrawListPtr drawList, Rect rect, string title, ReadOnlySpan<string> names,
        ReadOnlySpan<string> odds, bool alignRight, float scale)
    {
        if (rect.Width <= 0f || names.Length == 0)
        {
            return;
        }

        var titleStyle = TextStyles.FootnoteEmphasized;
        var rowStyle = TextStyles.Footnote;
        var rowScale = RowScale(rect.Width, names, odds, rowStyle, scale);
        if (rowScale <= 0f)
        {
            return;
        }

        var ink = CasinoColors.Money with { W = PrintAlpha };
        var titleShown = Typography.FitText(title, rect.Width, titleStyle);
        var titleSize = Typography.Measure(titleShown, titleStyle);
        var lineHeight = Typography.LineHeight(rowStyle) * rowScale / rowStyle.Scale + TableRowGap * scale;
        var blockHeight = titleSize.Y + lineHeight * names.Length;
        if (blockHeight > rect.Height)
        {
            var shrink = MathF.Max(0f, rect.Height - titleSize.Y) / (lineHeight * names.Length);
            rowScale *= shrink;
            if (rowScale < TextStyles.Caption2.Scale)
            {
                return;
            }

            lineHeight *= shrink;
            blockHeight = titleSize.Y + lineHeight * names.Length;
        }
        var top = rect.Center.Y - blockHeight * 0.5f;
        var titleLeft = alignRight ? rect.Max.X - titleSize.X : rect.Min.X;
        Typography.Draw(drawList, new Vector2(titleLeft, top), titleShown, ink, titleStyle);
        var y = top + titleSize.Y;
        var gap = Metrics.Space.Xs * scale;
        for (var index = 0; index < names.Length; index++)
        {
            var oddsSize = Typography.Measure(odds[index], rowScale, rowStyle.Weight);
            var name = Typography.FitText(names[index], rect.Width - oddsSize.X - gap, rowScale, rowStyle.Weight);
            var nameSize = Typography.Measure(name, rowScale, rowStyle.Weight);
            if (alignRight)
            {
                Typography.Draw(drawList, new Vector2(rect.Max.X - oddsSize.X, y), odds[index], ink, rowScale,
                    TextStyles.FootnoteEmphasized.Weight);
                Typography.Draw(drawList, new Vector2(rect.Max.X - oddsSize.X - gap - nameSize.X, y), name, ink,
                    rowScale, rowStyle.Weight);
            }
            else
            {
                Typography.Draw(drawList, new Vector2(rect.Min.X, y), name, ink, rowScale, rowStyle.Weight);
                Typography.Draw(drawList, new Vector2(rect.Min.X + nameSize.X + gap, y), odds[index], ink, rowScale,
                    TextStyles.FootnoteEmphasized.Weight);
            }

            y += lineHeight;
        }
    }

    public static float TableHeight(int rows, float scale) =>
        Typography.LineHeight(TextStyles.FootnoteEmphasized)
        + (Typography.LineHeight(TextStyles.Footnote) + TableRowGap * scale) * rows;

    private static float RowScale(float width, ReadOnlySpan<string> names, ReadOnlySpan<string> odds,
        in TextStyle style, float scale)
    {
        var widest = 0f;
        var gap = Metrics.Space.Xs * scale;
        for (var index = 0; index < names.Length; index++)
        {
            var row = Typography.Measure(names[index], style).X + gap
                      + Typography.Measure(odds[index], TextStyles.FootnoteEmphasized).X;
            widest = MathF.Max(widest, row);
        }

        if (widest <= width)
        {
            return style.Scale;
        }

        var shrunk = style.Scale * width / widest;
        return shrunk >= TextStyles.Caption2.Scale ? shrunk : 0f;
    }

    public static void Spot(ImDrawListPtr drawList, Vector2 center, float radius, bool lit, float glow, float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CircleFill), RingSegments);
        if (glow > 0f)
        {
            var spread = GlowGrowth * scale * glow;
            drawList.AddCircle(center, radius + spread, ImGui.GetColorU32(Glow with { W = Glow.W * glow }),
                RingSegments, MathF.Max(2f, spread * 1.4f));
        }

        var alpha = lit ? LitRingAlpha : RingAlpha;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = alpha }), RingSegments,
            MathF.Max(1.2f, 1.6f * scale));
        drawList.AddCircle(center, radius - 4f * scale,
            ImGui.GetColorU32(CasinoColors.Money with { W = alpha * 0.35f }), RingSegments, MathF.Max(1f, scale));
    }

    public static void EqualsSign(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var half = 5f * scale;
        var spread = 2.5f * scale;
        var color = ImGui.GetColorU32(CasinoColors.Money with { W = PrintAlpha });
        var thickness = MathF.Max(1.2f, 1.6f * scale);
        drawList.AddLine(new Vector2(center.X - half, center.Y - spread), new Vector2(center.X + half, center.Y - spread),
            color, thickness);
        drawList.AddLine(new Vector2(center.X - half, center.Y + spread), new Vector2(center.X + half, center.Y + spread),
            color, thickness);
    }

    public static void Stake(ImDrawListPtr drawList, Vector2 center, float radius, long amount, string text,
        float alpha, float scale)
    {
        if (amount <= 0 || alpha <= 0f)
        {
            return;
        }

        var discRadius = MathF.Min(DiscRadius * scale, radius * 0.42f);
        var denomination = TopDenomination(amount);
        var discs = (int)Math.Clamp(amount / denomination, 1, MaxDiscs);
        var step = DiscStep * scale;
        var textHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var stackHeight = discRadius * 2f + step * (discs - 1);
        var top = center.Y - (stackHeight + textHeight) * 0.5f;
        var baseY = top + stackHeight - discRadius;
        for (var disc = 0; disc < discs; disc++)
        {
            ChipStack.DrawDisc(drawList, new Vector2(center.X, baseY - disc * step), discRadius, denomination, alpha);
        }

        var shown = Typography.FitText(text, radius * 1.8f, TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(shown, TextStyles.FootnoteEmphasized);
        var textTop = baseY + discRadius;
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, textTop + 1f * scale), shown,
            new Vector4(0f, 0f, 0f, 0.6f * alpha), TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, textTop), shown,
            CasinoColors.Money with { W = alpha }, TextStyles.FootnoteEmphasized);
    }

    public static void FlyingChips(ImDrawListPtr drawList, Vector2 center, long amount, float alpha, float scale)
    {
        if (amount <= 0 || alpha <= 0f)
        {
            return;
        }

        var discRadius = DiscRadius * scale;
        var denomination = TopDenomination(amount);
        var discs = (int)Math.Clamp(amount / denomination, 1, MaxDiscs);
        var step = DiscStep * scale;
        for (var disc = 0; disc < discs; disc++)
        {
            ChipStack.DrawDisc(drawList, new Vector2(center.X, center.Y - disc * step), discRadius, denomination,
                alpha);
        }
    }

    public static long TopDenomination(long amount)
    {
        var denominations = ChipStack.Denominations;
        for (var index = 0; index < denominations.Length; index++)
        {
            if (amount >= denominations[index])
            {
                return denominations[index];
            }
        }

        return denominations[^1];
    }

    public static void SpotLabel(ImDrawListPtr drawList, Vector2 center, string label, float maxWidth, bool lit)
    {
        var style = TextStyles.FootnoteEmphasized;
        var shown = Typography.FitText(label, maxWidth, style);
        var size = Typography.Measure(shown, style);
        var ink = lit ? CasinoColors.MoneyHighlight : CasinoColors.Money with { W = PrintAlpha };
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f), shown, ink, style);
    }

    public static void CardSlot(ImDrawListPtr drawList, Vector2 center, float width, float scale)
    {
        var half = new Vector2(width * 0.5f, PlayingCards.HeightFor(width) * 0.5f);
        PlayingCards.DrawSlot(drawList, new Rect(center - half, center + half), PlayingCards.RoundingFor(width), scale);
    }
}
