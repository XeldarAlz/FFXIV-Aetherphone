using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp
{
    private const float DayRingsHeight = 212f;
    private const float DayRingRadius = 92f;
    private const float DayRingThickness = 22f;
    private const float DayRingGap = 3f;
    private const float ChartHeight = 56f;
    private const float ChartBarWidth = 8f;
    private const float ChartGap = 12f;
    private const float ChartLabelGap = 6f;
    private const float StatRowHeight = 60f;
    private const float GuideAlpha = 0.35f;

    private readonly Spring[] dayFills = new Spring[ActivityGoals.RingCount];
    private readonly float[] chartScratch = new float[ActivityDigest.WeekLength];

    private void DrawDay(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var slot = Math.Clamp(selectedSlot, 0, ActivityDigest.TodaySlot);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = WeekStrip(drawList, origin, width, slot, false, scale);
            cursorY = DrawDayRings(drawList, new Vector2(origin.X, cursorY), width, slot, scale);
            if (digest.Days[slot] is null)
            {
                cursorY += ActivityArt.State(drawList, ui, new Vector2(origin.X, cursorY), width,
                    FontAwesomeIcon.Moon, ui.Accent, Loc.T(L.Character.DayEmptyTitle),
                    Loc.T(L.Character.DayEmptyBody), scale);
            }
            else
            {
                for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
                {
                    cursorY = DrawRingCard(drawList, new Vector2(origin.X, cursorY), width, slot, ring, scale);
                    cursorY += ActivityArt.TileGap * scale;
                }

                cursorY = DrawDayStats(drawList, new Vector2(origin.X, cursorY), width, slot, scale);
            }

            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "character.day.nav", digest.Titles[slot], NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawDayRings(ImDrawListPtr drawList, Vector2 origin, float width, int slot, float scale)
    {
        var height = DayRingsHeight * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f);
        var radius = DayRingRadius * scale;
        UiAnchors.Report("character.day", new Rect(center - new Vector2(radius, radius),
            center + new Vector2(radius, radius)));
        var delta = ActivityArt.FrameDelta();
        var offset = slot * ActivityGoals.RingCount;
        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            ringScratch[ring] = dayFills[ring].Step(digest.Fractions[offset + ring], Motion.Sheet, delta);
        }

        ActivityArt.Rings(drawList, center, radius, DayRingThickness * scale, DayRingGap * scale, ringScratch, true);
        return origin.Y + height;
    }

    private float DrawRingCard(ImDrawListPtr drawList, Vector2 origin, float width, int slot, int ring, float scale)
    {
        var pad = ActivityArt.CardPad * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var valueHeight = Typography.LineHeight(TextStyles.Title2);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var letterHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = pad * 2f + nameHeight + valueHeight + detailHeight + ChartGap * scale + ChartHeight * scale +
                     ChartLabelGap * scale + letterHeight;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var tint = ActivityArt.Tint(ring);
        var left = origin.X + pad;
        var textWidth = MathF.Max(1f, width - pad * 2f);
        var cursorY = origin.Y + pad;
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(Loc.T(RingNames[ring]), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        cursorY += nameHeight;
        var index = slot * ActivityGoals.RingCount + ring;
        DrawValueWithUnit(drawList, new Vector2(left, cursorY), textWidth, digest.RingValues[index],
            digest.Units[ring], tint);
        cursorY += valueHeight;
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(digest.RingDetails[index], textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        cursorY += detailHeight + ChartGap * scale;
        for (var day = 0; day < ActivityDigest.WeekLength; day++)
        {
            chartScratch[day] = digest.Fractions[day * ActivityGoals.RingCount + ring];
        }

        var chart = new Rect(new Vector2(left, cursorY), new Vector2(max.X - pad, cursorY + ChartHeight * scale));
        ActivityArt.Bars(drawList, chart, chartScratch, slot, tint, Palette.WithAlpha(ui.MutedInk, GuideAlpha),
            ChartBarWidth * scale);
        var columnWidth = chart.Width / ActivityDigest.WeekLength;
        var letterCenterY = chart.Max.Y + ChartLabelGap * scale + letterHeight * 0.5f;
        for (var day = 0; day < ActivityDigest.WeekLength; day++)
        {
            Typography.DrawCentered(drawList, new Vector2(chart.Min.X + columnWidth * (day + 0.5f), letterCenterY),
                Typography.FitText(digest.Letters[day], columnWidth, TextStyles.Footnote),
                day == slot ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
        }

        return max.Y;
    }

    private float DrawDayStats(ImDrawListPtr drawList, Vector2 origin, float width, int slot, float scale)
    {
        var play = digest.PlayTexts[slot];
        var collectibles = digest.CollectibleTexts[slot];
        var rows = (play.Length > 0 ? 1 : 0) + (collectibles.Length > 0 ? 1 : 0);
        if (rows == 0)
        {
            return origin.Y;
        }

        var rowHeight = StatRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * rows);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var rowTop = origin.Y;
        if (play.Length > 0)
        {
            DrawStatRow(drawList, new Rect(new Vector2(origin.X, rowTop), new Vector2(max.X, rowTop + rowHeight)),
                Accent.Blue, FontAwesomeIcon.Clock, Loc.T(L.Character.TimePlayed), play, scale);
            rowTop += rowHeight;
        }

        if (collectibles.Length > 0)
        {
            if (rowTop > origin.Y)
            {
                var pad = ActivityArt.CardPad * scale;
                drawList.AddLine(new Vector2(origin.X + pad, rowTop), new Vector2(max.X, rowTop),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            DrawStatRow(drawList, new Rect(new Vector2(origin.X, rowTop), new Vector2(max.X, rowTop + rowHeight)),
                Accent.Violet, FontAwesomeIcon.Dragon, Loc.T(L.Character.NewCollectibles), collectibles, scale);
        }

        return max.Y;
    }

    private void DrawStatRow(ImDrawListPtr drawList, Rect row, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, float scale)
    {
        var pad = ActivityArt.CardPad * scale;
        var glyph = TileGlyph * scale;
        var centerY = row.Center.Y;
        ActivityArt.GlyphTile(drawList, new Vector2(row.Min.X + pad + glyph * 0.5f, centerY), glyph, tint, icon);
        var textLeft = row.Min.X + pad + glyph + ActivityArt.TileGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - pad - textLeft);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var top = centerY - (labelHeight + valueHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, top + labelHeight),
            Typography.FitText(value, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
    }
}
