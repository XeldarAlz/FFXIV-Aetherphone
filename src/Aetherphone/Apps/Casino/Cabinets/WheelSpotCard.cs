using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelSpotView
{
    public readonly int Spot;
    public readonly long Mine;
    public readonly int Bettors;
    public readonly long Total;
    public readonly bool Selected;
    public readonly bool Hovered;
    public readonly bool Winning;
    public readonly bool Settled;
    public readonly bool Open;

    public WheelSpotView(int spot, long mine, int bettors, long total, bool selected, bool hovered, bool winning,
        bool settled, bool open)
    {
        Spot = spot;
        Mine = mine;
        Bettors = WheelSpotCard.CrowdOf(bettors, mine);
        Total = Math.Max(total, mine);
        Selected = selected;
        Hovered = hovered;
        Winning = winning;
        Settled = settled;
        Open = open;
    }

    public bool ShowsTable => WheelSpotCard.ShowsTable(Bettors, Mine);

    public bool Losing => Settled && !Winning && Mine > 0;

    public bool Ghost => Open && Selected && Mine <= 0;

    public bool Marked => Winning || (Open && Selected);
}

internal static class WheelSpotCard
{
    public const float CapHeight = 3f;
    public const float SelectedCapHeight = 5f;
    public const float DiscRadius = 8f;
    public const float GlyphBox = 12f;
    public const float GlyphGap = 4f;

    public static readonly TextStyle HeaderStyle = TextStyles.Title2;
    public static readonly TextStyle StakeStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle TableStyle = TextStyles.Footnote;

    private const float LosingDim = 0.55f;
    private const float DividerAlpha = 0.12f;
    private const float GhostAlpha = 0.7f;

    public static int CrowdOf(int bettors, long mine) => mine > 0 && bettors < 1 ? 1 : Math.Max(0, bettors);

    public static bool ShowsTable(int bettors, long mine) => bettors > (mine > 0 ? 1 : 0);

    public static void Draw(ImDrawListPtr drawList, AppSkin ui, Rect rect, in WheelSpotView view, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        var color = WheelRingArt.SpotColors[view.Spot];
        var ink = WheelRingArt.SpotTextInks[view.Spot];
        var marked = view.Marked;
        var lit = view.Winning ? 1f : marked ? 0.7f : 0.2f;
        var ground = ui.Palette.CardFill with { W = 1f };
        drawList.AddCircleFilled(new Vector2(rect.Center.X, rect.Min.Y), rect.Width * 0.6f,
            ImGui.GetColorU32(color with { W = 0.12f * lit }), 24);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(ground, color, 0.10f + 0.28f * lit)), ImGui.GetColorU32(ground));
        var cap = (marked ? SelectedCapHeight : CapHeight) * scale;
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Min.Y + cap), rounding * 0.4f,
            ImGui.GetColorU32(color));
        if (view.Hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
        }

        var rows = WheelPodiumLayout.Compute(rect, scale);
        DrawHeader(drawList, rows.Header, WheelRingArt.SpotLabel(view.Spot), ink);
        DrawStake(drawList, rows.Stake, view, color, scale);
        if (view.ShowsTable)
        {
            drawList.AddLine(new Vector2(rows.Crowd.Min.X, rows.DividerY), new Vector2(rows.Crowd.Max.X, rows.DividerY),
                ImGui.GetColorU32(CasinoColors.InkTitle with { W = DividerAlpha }), MathF.Max(1f, scale));
            DrawCrowd(drawList, rows.Crowd, view.Bettors, scale);
            DrawTotal(drawList, rows.Total, view.Total);
        }

        if (view.Losing)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, LosingDim)));
        }

        if (marked)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(view.Winning ? CasinoColors.Money : ink), 2f * scale);
        }
    }

    private static void DrawHeader(ImDrawListPtr drawList, Rect row, string label, Vector4 ink)
    {
        var style = Fitted(label, row, HeaderStyle);
        Typography.DrawCentered(drawList, row.Center, label, ink, style);
    }

    private static void DrawStake(ImDrawListPtr drawList, Rect row, in WheelSpotView view, Vector4 color, float scale)
    {
        var radius = MathF.Min(DiscRadius * scale, row.Height * 0.5f);
        if (view.Mine <= 0)
        {
            if (view.Ghost)
            {
                drawList.AddCircle(row.Center, radius, ImGui.GetColorU32(Palette.WithAlpha(
                    WheelRingArt.SpotTextInks[view.Spot], GhostAlpha)), 20, MathF.Max(1f, 1.4f * scale));
            }

            return;
        }

        var text = NumberText.Compact(view.Mine);
        var gap = GlyphGap * scale;
        var style = Fitted(text, new Rect(row.Min, new Vector2(row.Max.X - radius * 2f - gap, row.Max.Y)),
            StakeStyle);
        var size = Typography.Measure(text, style);
        var left = row.Center.X - (radius * 2f + gap + size.X) * 0.5f;
        ChipStack.DrawDisc(drawList, new Vector2(left + radius, row.Center.Y), radius, TopDenomination(view.Mine));
        Typography.Draw(drawList, new Vector2(left + radius * 2f + gap, row.Center.Y - size.Y * 0.5f), text,
            view.Winning ? CasinoColors.MoneyHighlight : CasinoColors.Money, style);
    }

    private static void DrawCrowd(ImDrawListPtr drawList, Rect row, int bettors, float scale)
    {
        var text = GameNumber.Label(bettors);
        var glyph = MathF.Min(GlyphBox * scale, row.Height);
        var gap = GlyphGap * scale;
        var style = Fitted(text, new Rect(row.Min, new Vector2(row.Max.X - glyph - gap, row.Max.Y)), TableStyle);
        var size = Typography.Measure(text, style);
        var left = row.Center.X - (glyph + gap + size.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, row.Center.Y), PhoneIcons.Users,
            CasinoColors.InkBody, glyph);
        Typography.Draw(drawList, new Vector2(left + glyph + gap, row.Center.Y - size.Y * 0.5f), text,
            CasinoColors.InkBody, style);
    }

    private static void DrawTotal(ImDrawListPtr drawList, Rect row, long total)
    {
        var text = NumberText.Compact(total);
        var amount = CurrencyGlyph.MeasureAmount(text, TableStyle);
        if (amount.X > row.Width)
        {
            var style = Fitted(text, row, TableStyle);
            Typography.DrawCentered(drawList, row.Center, text, CasinoColors.InkBody, style);
            return;
        }

        var origin = new Vector2(row.Center.X - amount.X * 0.5f, row.Center.Y - amount.Y * 0.5f);
        CurrencyGlyph.DrawAmount(drawList, origin, text, CurrencyKind.Chips, CasinoColors.InkBody, TableStyle);
    }

    private static TextStyle Fitted(string text, Rect row, in TextStyle style)
    {
        var lineHeight = Typography.LineHeight(style);
        var tallest = lineHeight > row.Height && lineHeight > 0f ? style.Scale * row.Height / lineHeight : style.Scale;
        var floor = MathF.Min(tallest, TextStyles.Footnote.Scale);
        return style with { Scale = Typography.FitScale(text, row.Width, tallest, floor, style.Weight) };
    }

    private static long TopDenomination(long amount)
    {
        var denominations = ChipStack.Denominations;
        for (var index = 0; index < denominations.Length; index++)
        {
            if (denominations[index] <= amount)
            {
                return denominations[index];
            }
        }

        return denominations[denominations.Length - 1];
    }
}
