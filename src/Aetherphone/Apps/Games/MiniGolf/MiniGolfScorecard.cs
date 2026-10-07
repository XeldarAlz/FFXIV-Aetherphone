using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.MiniGolf;

internal static class MiniGolfScorecard
{
    private const int Columns = MiniGolfCourse.FrontNine;
    private const float MaxCardWidth = 380f;
    private const float CardRadius = 22f;
    private const float RowHeight = 26f;
    private const float LabelWidth = 70f;
    private const float TotalWidth = 40f;
    private const float ToParWidth = 40f;
    private const float ChipInset = 3f;
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 44f;
    private static readonly TextStyle CellStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle LabelStyle = TextStyles.Footnote;
    private static readonly Vector4 Gold = new(1f, 0.80f, 0.30f, 1f);
    private static readonly Vector4 Birdie = new(0.36f, 0.74f, 1f, 1f);
    private static readonly Vector4 Bogey = new(1f, 0.62f, 0.30f, 1f);
    private static readonly Vector4 Double = new(0.95f, 0.36f, 0.36f, 1f);

    public static Vector4 ResultColor(HoleResult result) => result switch
    {
        HoleResult.HoleInOne or HoleResult.Eagle => Gold,
        HoleResult.Birdie => Birdie,
        HoleResult.Par => new Vector4(0.82f, 0.86f, 0.90f, 1f),
        HoleResult.Bogey => Bogey,
        _ => Double,
    };

    public static string ToParLabel(int difference) => difference switch
    {
        0 => Loc.T(L.MiniGolf.Even),
        > 0 => GameNumber.Signed(difference),
        _ => GameNumber.Label(difference),
    };

    public static bool Draw(ImDrawListPtr drawList, Rect area, MiniGolfRound round, int shownHole, string subtitle,
        bool final, Vector4 accent, PhoneTheme theme, float appear, float scale)
    {
        var alpha = Math.Clamp(appear * 1.6f, 0f, 1f);
        Material.Veil(drawList, area.Min, area.Max, 0.45f * alpha);
        var grow = 0.9f + 0.1f * Easing.EaseOutBack(Math.Clamp(appear, 0f, 1f));
        var padding = Metrics.Space.Lg * scale;
        var cardWidth = MathF.Min(area.Width - Metrics.Space.Md * scale, MaxCardWidth * scale);
        var contentWidth = cardWidth - padding * 2f;
        var title = Loc.T(L.MiniGolf.Scorecard);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var subtitleHeight = Typography.LineHeight(LabelStyle);
        var rowHeight = RowHeight * scale;
        var rows = 2 + round.Players;
        var gridHeight = rows * rowHeight;
        var buttonHeight = ButtonHeight * scale;
        var cardHeight = padding * 2f + titleHeight + Metrics.Space.Xxs * scale + subtitleHeight + Metrics.Space.Lg * scale +
                         gridHeight + Metrics.Space.Xl * scale + buttonHeight;
        var center = area.Center;
        var half = new Vector2(cardWidth, cardHeight) * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var radius = CardRadius * scale;
        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.25f * alpha }), scale);
        var top = min.Y + padding * grow;
        Typography.DrawCentered(drawList, new Vector2(center.X, top + titleHeight * 0.5f), title,
            theme.TextStrong with { W = alpha }, TextStyles.Title2);
        top += titleHeight + Metrics.Space.Xxs * scale;
        Typography.DrawCentered(drawList, new Vector2(center.X, top + subtitleHeight * 0.5f), subtitle,
            theme.TextMuted with { W = alpha }, LabelStyle);
        top += subtitleHeight + Metrics.Space.Lg * scale;
        var left = center.X - contentWidth * 0.5f * grow;
        DrawGrid(drawList, new Vector2(left, top), contentWidth * grow, rowHeight, round, shownHole, accent, theme, alpha,
            scale);
        top += gridHeight + Metrics.Space.Xl * scale;
        if (appear < 0.6f)
        {
            return false;
        }

        var label = Loc.T(final ? L.MiniGolf.FinishRound : L.MiniGolf.NextHole);
        var size = new Vector2(MathF.Min(contentWidth, ButtonWidth * scale), buttonHeight);
        return GameHud.Button(new Vector2(center.X, top + buttonHeight * 0.5f), size, label, accent, theme);
    }

    private static void DrawGrid(ImDrawListPtr drawList, Vector2 origin, float width, float rowHeight, MiniGolfRound round,
        int shownHole, Vector4 accent, PhoneTheme theme, float alpha, float scale)
    {
        var firstHole = shownHole >= Columns && round.Holes > Columns ? Columns : 0;
        var labelWidth = LabelWidth * scale;
        var totalWidth = TotalWidth * scale;
        var toParWidth = ToParWidth * scale;
        var cellWidth = MathF.Max(1f, (width - labelWidth - totalWidth - toParWidth) / Columns);
        var ink = theme.TextStrong with { W = alpha };
        var muted = theme.TextMuted with { W = alpha };
        var highlightLeft = origin.X + labelWidth + (shownHole - firstHole) * cellWidth;
        drawList.AddRectFilled(new Vector2(highlightLeft, origin.Y), new Vector2(highlightLeft + cellWidth,
            origin.Y + rowHeight * (2 + round.Players)), ImGui.GetColorU32(accent with { W = 0.16f * alpha }), 6f * scale);
        var rowTop = origin.Y;
        DrawLabel(drawList, origin.X, rowTop, labelWidth, rowHeight, Loc.T(L.MiniGolf.Hole), muted);
        for (var column = 0; column < Columns; column++)
        {
            var hole = firstHole + column;
            if (hole >= round.Holes)
            {
                break;
            }

            DrawCellText(drawList, CellCenter(origin.X, labelWidth, cellWidth, column, rowTop, rowHeight),
                GameNumber.Label(hole + 1), muted);
        }

        DrawCellText(drawList, new Vector2(origin.X + width - toParWidth - totalWidth * 0.5f, rowTop + rowHeight * 0.5f),
            Typography.FitText(Loc.T(L.MiniGolf.Total), totalWidth, LabelStyle), muted);
        rowTop += rowHeight;
        DrawLabel(drawList, origin.X, rowTop, labelWidth, rowHeight, Loc.T(L.MiniGolf.Par), muted);
        for (var column = 0; column < Columns; column++)
        {
            var hole = firstHole + column;
            if (hole >= round.Holes)
            {
                break;
            }

            DrawCellText(drawList, CellCenter(origin.X, labelWidth, cellWidth, column, rowTop, rowHeight),
                GameNumber.Label(MiniGolfCourse.Get(hole).Par), muted);
        }

        DrawCellText(drawList, new Vector2(origin.X + width - toParWidth - totalWidth * 0.5f, rowTop + rowHeight * 0.5f),
            GameNumber.Label(MiniGolfCourse.Par(round.Holes)), muted);
        drawList.AddLine(new Vector2(origin.X, rowTop + rowHeight), new Vector2(origin.X + width, rowTop + rowHeight),
            ImGui.GetColorU32(muted with { W = 0.25f * alpha }), scale);
        for (var player = 0; player < round.Players; player++)
        {
            rowTop += rowHeight;
            var name = round.Players > 1 ? GameSeats.Name(player) : Loc.T(L.MiniGolf.You);
            DrawLabel(drawList, origin.X, rowTop, labelWidth, rowHeight, name,
                round.Players > 1 ? GameSeats.Color(player) with { W = alpha } : ink);
            for (var column = 0; column < Columns; column++)
            {
                var hole = firstHole + column;
                if (hole >= round.Holes)
                {
                    break;
                }

                var strokes = round.Strokes(player, hole);
                if (strokes <= 0)
                {
                    continue;
                }

                var cellCenter = CellCenter(origin.X, labelWidth, cellWidth, column, rowTop, rowHeight);
                var result = MiniGolfRound.Classify(strokes, MiniGolfCourse.Get(hole).Par);
                var chipHalf = new Vector2(cellWidth * 0.5f - ChipInset * scale, rowHeight * 0.5f - ChipInset * scale);
                var fill = ResultColor(result);
                drawList.AddRectFilled(cellCenter - chipHalf, cellCenter + chipHalf,
                    ImGui.GetColorU32(fill with { W = (result == HoleResult.Par ? 0.18f : 0.85f) * alpha }), 5f * scale);
                DrawCellText(drawList, cellCenter, GameNumber.Label(strokes),
                    result == HoleResult.Par ? ink : GamePalette.InkOn(fill) with { W = alpha });
            }

            DrawCellText(drawList,
                new Vector2(origin.X + width - toParWidth - totalWidth * 0.5f, rowTop + rowHeight * 0.5f),
                GameNumber.Label(round.Total(player)), ink);
            DrawCellText(drawList, new Vector2(origin.X + width - toParWidth * 0.5f, rowTop + rowHeight * 0.5f),
                ToParLabel(round.ToPar(player)), ink);
        }
    }

    private static Vector2 CellCenter(float left, float labelWidth, float cellWidth, int column, float rowTop,
        float rowHeight) =>
        new(left + labelWidth + cellWidth * (column + 0.5f), rowTop + rowHeight * 0.5f);

    private static void DrawLabel(ImDrawListPtr drawList, float left, float rowTop, float width, float rowHeight,
        string text, Vector4 color)
    {
        var fitted = Typography.FitText(text, width - 4f, LabelStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - Typography.LineHeight(LabelStyle)) * 0.5f), fitted,
            color, LabelStyle);
    }

    private static void DrawCellText(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color)
    {
        Typography.DrawCentered(drawList, center, text, color, CellStyle);
    }
}
