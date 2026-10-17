using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp
{
    private const float HeroHeight = 172f;
    private const float HeroRingRadius = 62f;
    private const float HeroRingThickness = 15f;
    private const float HeroRingGap = 2.5f;
    private const float HeroTextGap = 18f;
    private const float HeroChevronSize = 5f;
    private const float StreakTileHeight = 100f;
    private const float TileGlyph = 30f;
    private const float TileGlowCoverage = 0.6f;
    private const float TileGlowStrength = 0.14f;
    private const float HintGap = 8f;
    private const float TrendTileHeight = 88f;
    private const float TrendArrowSize = 28f;
    private const float TrendArrowGlyph = 13f;
    private const float TrendFallingAlpha = 0.5f;
    private const float SessionCellHeight = 60f;
    private const float RetainerRowHeight = 62f;
    private const float AwardColumns = 4f;
    private const float AwardRadius = 27f;
    private const float AwardNameGap = 8f;
    private const float AwardRowGap = 16f;
    private const float LineGap = 2f;

    private static readonly LocString[] AwardNames =
    {
        L.Character.AwardPerfectDay,
        L.Character.AwardPerfectWeek,
        L.Character.AwardPerfectMonth,
        L.Character.AwardExperience,
        L.Character.AwardDuties,
        L.Character.AwardFortune,
        L.Character.AwardLongestDay,
        L.Character.AwardLevels,
    };

    private static readonly LocString[] AwardDescriptions =
    {
        L.Character.AwardPerfectDayHint,
        L.Character.AwardPerfectWeekHint,
        L.Character.AwardPerfectMonthHint,
        L.Character.AwardExperienceHint,
        L.Character.AwardDutiesHint,
        L.Character.AwardFortuneHint,
        L.Character.AwardLongestDayHint,
        L.Character.AwardLevelsHint,
    };

    private static readonly string[] AwardTooltipIds =
    {
        "character.award.0",
        "character.award.1",
        "character.award.2",
        "character.award.3",
        "character.award.4",
        "character.award.5",
        "character.award.6",
        "character.award.7",
    };

    private static readonly LocString[] TrendNames =
    {
        L.Character.GoalLevels,
        L.Character.Duties,
        L.Character.GilEarned,
        L.Character.TimePlayed,
    };

    private static readonly LocString[] RingNames =
    {
        L.Character.RingProgress,
        L.Character.RingAdventure,
        L.Character.RingFortune,
    };

    private readonly Spring[] heroFills = new Spring[ActivityGoals.RingCount];
    private readonly NavBarButton[] summaryButtons = new NavBarButton[1];

    private void DrawSummary(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawHero(drawList, origin, width, scale);
            cursorY = WeekStrip(drawList, new Vector2(origin.X, cursorY + ActivityArt.TileGap * scale), width, -1,
                true, scale);
            cursorY = DrawStreaks(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawTrends(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawSession(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawRetainers(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawAwards(drawList, origin.X, cursorY, width, scale);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        summaryButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Bullseye), Loc.T(L.Character.ChangeGoals));
        UiAnchors.Report("character.goals", AppHeader.LargeTitleButtonRect(in navBar, 0, 1));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "character.nav", DisplayName, NavBarStyle.From(ui),
            summaryButtons);
        if (pressed != 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        router.Push(ActivityView.Goals());
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.Title2);
        var height = ActivityArt.HeroCardHeight(HeroHeight, labelHeight + valueHeight, scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("character.rings", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID("character.hero"), pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = ActivityArt.CardPad * scale;
        var ringRadius = HeroRingRadius * scale * press;
        var center = new Vector2(min.X + pad + ringRadius, (min.Y + max.Y) * 0.5f);
        var delta = ActivityArt.FrameDelta();
        var offset = ActivityDigest.TodaySlot * ActivityGoals.RingCount;
        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            ringScratch[ring] = heroFills[ring].Step(digest.Fractions[offset + ring], Motion.Sheet, delta);
        }

        ActivityArt.Rings(drawList, center, ringRadius, HeroRingThickness * scale * press, HeroRingGap * scale,
            ringScratch, true);
        var chevronCenter = new Vector2(max.X - pad, min.Y + pad + HeroChevronSize * scale);
        ActivityArt.Chevron(drawList, chevronCenter, HeroChevronSize * scale, ui.MutedInk, scale);
        var textLeft = center.X + ringRadius + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, chevronCenter.X - HeroChevronSize * scale - textLeft);
        var blockHeight = (max.Y - min.Y - pad * 2f) / ActivityGoals.RingCount;
        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            var top = min.Y + pad + blockHeight * ring + (blockHeight - labelHeight - valueHeight) * 0.5f;
            var tint = ActivityArt.Tint(ring);
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(Loc.T(RingNames[ring]), textWidth, TextStyles.FootnoteEmphasized), ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            DrawValueWithUnit(drawList, new Vector2(textLeft, top + labelHeight), textWidth,
                digest.RingValues[offset + ring], digest.RingUnit(ActivityDigest.TodaySlot, ring), tint);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenDay(ActivityDigest.TodaySlot);
        }

        return rect.Max.Y;
    }

    private static void DrawValueWithUnit(ImDrawListPtr drawList, Vector2 topLeft, float width, string value,
        string unit, Vector4 tint)
    {
        var valueText = Typography.FitText(value, width, TextStyles.Title2);
        var valueSize = Typography.Measure(valueText, TextStyles.Title2);
        Typography.Draw(drawList, topLeft, valueText, tint, TextStyles.Title2);
        var unitWidth = width - valueSize.X - LineGap * 2f;
        if (unitWidth <= 0f)
        {
            return;
        }

        var unitText = Typography.FitText(unit, unitWidth, TextStyles.FootnoteEmphasized);
        var unitHeight = Typography.Measure(unitText, TextStyles.FootnoteEmphasized).Y;
        Typography.Draw(drawList,
            new Vector2(topLeft.X + valueSize.X + LineGap * 2f, topLeft.Y + valueSize.Y - unitHeight - LineGap),
            unitText, tint, TextStyles.FootnoteEmphasized);
    }

    private float SectionTop(ImDrawListPtr drawList, float left, float top, float width, string title, float scale)
    {
        var cursorY = top + ActivityArt.SectionGap * scale;
        cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, title, ui.TitleInk);
        return cursorY + ActivityArt.HeaderGap * scale;
    }

    private float DrawStreaks(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Character.Streaks), scale);
        var gap = ActivityArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var textWidth = TileTextWidth(tileWidth, scale);
        var currentLabel = Loc.T(L.Character.CurrentStreak);
        var bestLabel = Loc.T(L.Character.BestStreak);
        var labelLines = Math.Max(WrappedLabelLines(currentLabel, textWidth),
            WrappedLabelLines(bestLabel, textWidth));
        var labelHeight = Typography.LineHeight(TextStyles.Footnote) * labelLines;
        var height = ActivityArt.TileHeight(StreakTileHeight, TileGlyph, labelHeight,
            Typography.LineHeight(TextStyles.Title3), scale);
        var current = new Rect(new Vector2(left, cursorY), new Vector2(left + tileWidth, cursorY + height));
        var best = new Rect(new Vector2(current.Max.X + gap, cursorY), new Vector2(left + width, cursorY + height));
        DrawStatTile(drawList, current, Accent.Amber, FontAwesomeIcon.Fire, currentLabel, digest.CurrentStreakText,
            digest.CurrentStreak > 0, textWidth, labelLines, scale);
        DrawStatTile(drawList, best, Accent.Violet, FontAwesomeIcon.Trophy, bestLabel, digest.BestStreakText, false,
            textWidth, labelLines, scale);
        cursorY += height + HintGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.Character.StreaksHint), ui.MutedInk,
            TextStyles.Footnote, width);
        return cursorY;
    }

    private void DrawStatTile(ImDrawListPtr drawList, Rect rect, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, bool lit, float textWidth, int labelLines, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, rect.Min, rect.Max, radius);
        if (lit)
        {
            Material.TopGlow(drawList, rect.Min, rect.Max, radius, tint, TileGlowCoverage, TileGlowStrength);
        }

        var pad = ActivityArt.CardPad * scale;
        var glyph = TileGlyph * scale;
        ActivityArt.GlyphTile(drawList, new Vector2(rect.Min.X + pad + glyph * 0.5f, rect.Min.Y + pad + glyph * 0.5f),
            glyph, tint, icon);
        var valueTop = rect.Max.Y - pad - Typography.LineHeight(TextStyles.Title3);
        DrawTileLabel(drawList, rect.Min.X + pad, valueTop, label, textWidth, labelLines);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop),
            Typography.FitText(value, textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
    }

    private float DrawTrends(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Character.Trends), scale);
        if (!digest.TrendsAvailable)
        {
            return cursorY + ActivityArt.State(drawList, ui, new Vector2(left, cursorY), width,
                FontAwesomeIcon.ChartBar, ui.Accent, Loc.T(L.Character.TrendsEmptyTitle),
                Loc.T(L.Character.TrendsEmptyBody), scale);
        }

        var gap = ActivityArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var textWidth = TileTextWidth(tileWidth, scale);
        var labelLines = 1;
        for (var metric = 0; metric < ActivityStats.MetricCount; metric++)
        {
            labelLines = Math.Max(labelLines, WrappedLabelLines(Loc.T(TrendNames[metric]), textWidth));
        }

        var labelHeight = Typography.LineHeight(TextStyles.Footnote) * labelLines;
        var height = ActivityArt.TileHeight(TrendTileHeight, TrendArrowSize, labelHeight,
            Typography.LineHeight(TextStyles.Headline), scale);
        for (var metric = 0; metric < ActivityStats.MetricCount; metric++)
        {
            var column = metric % 2;
            var row = metric / 2;
            var min = new Vector2(left + column * (tileWidth + gap), cursorY + row * (height + gap));
            DrawTrendTile(drawList, new Rect(min, min + new Vector2(tileWidth, height)), metric, textWidth, labelLines,
                scale);
        }

        cursorY += height * 2f + gap + HintGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.Character.TrendsHint), ui.MutedInk,
            TextStyles.Footnote, width);
        return cursorY;
    }

    private void DrawTrendTile(ImDrawListPtr drawList, Rect rect, int metric, float textWidth, int labelLines,
        float scale)
    {
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale);
        var pad = ActivityArt.CardPad * scale;
        var tint = metric < ActivityGoals.RingCount ? ActivityArt.Tint(metric) : Accent.Blue;
        var rising = digest.TrendRising[metric];
        var arrow = TrendArrowSize * scale;
        var arrowCenter = new Vector2(rect.Min.X + pad + arrow * 0.5f, rect.Min.Y + pad + arrow * 0.5f);
        var arrowInk = rising ? tint : Palette.WithAlpha(ui.MutedInk, TrendFallingAlpha);
        drawList.AddCircleFilled(arrowCenter, arrow * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(arrowInk, 0.22f)), 24);
        ProgressRing.CenterIcon(drawList, arrowCenter, rising ? FontAwesomeIcon.ArrowUp : FontAwesomeIcon.ArrowDown,
            rising ? tint : ui.MutedInk, TrendArrowGlyph * scale);
        var valueTop = rect.Max.Y - pad - Typography.LineHeight(TextStyles.Headline);
        DrawTileLabel(drawList, rect.Min.X + pad, valueTop, Loc.T(TrendNames[metric]), textWidth, labelLines);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop),
            Typography.FitText(digest.TrendValues[metric], textWidth, TextStyles.Headline),
            rising ? tint : ui.TitleInk, TextStyles.Headline);
    }

    private static float TileTextWidth(float tileWidth, float scale) =>
        MathF.Max(1f, tileWidth - ActivityArt.CardPad * 2f * scale);

    private static int WrappedLabelLines(string label, float textWidth) =>
        ActivityArt.CapTileLabelLines(Typography.WrapText(label, TextStyles.Footnote, textWidth).Length);

    private void DrawTileLabel(ImDrawListPtr drawList, float left, float bottom, string label, float textWidth,
        int maxLines)
    {
        var wrapped = Typography.WrapText(label, TextStyles.Footnote, textWidth);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var count = Math.Min(wrapped.Length, maxLines);
        var top = bottom - lineHeight * count;
        for (var lineIndex = 0; lineIndex < count; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, top + lineHeight * lineIndex),
                Typography.FitText(wrapped[lineIndex], textWidth, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }
    }

    private float DrawSession(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var headerTop = top + ActivityArt.SectionGap * scale;
        var since = Typography.FitText(digest.SessionSince, width * 0.45f, TextStyles.Footnote);
        var sinceSize = Typography.Measure(since, TextStyles.Footnote);
        var bandHeight = CardSectionHeader.Draw(drawList, new Vector2(left, headerTop), width,
            Loc.T(L.Character.ThisSession), ui.TitleInk, sinceSize.X + HintGap * scale);
        Typography.Draw(drawList,
            new Vector2(left + width - sinceSize.X, headerTop + (bandHeight - sinceSize.Y) * 0.5f), since,
            ui.MutedInk, TextStyles.Footnote);
        var cardTop = headerTop + bandHeight + ActivityArt.HeaderGap * scale;
        var pad = ActivityArt.CardPad * scale;
        var cell = SessionCellHeight * scale;
        var max = new Vector2(left + width, cardTop + cell * 2f + pad);
        ui.Card(drawList, new Vector2(left, cardTop), max, Metrics.Radius.Grouped * scale);
        var cellWidth = (width - pad * 2f) * 0.5f;
        var originY = cardTop + pad * 0.5f;
        DrawSessionCell(drawList, new Vector2(left + pad, originY), cellWidth, cell, Accent.Blue,
            FontAwesomeIcon.Clock, Loc.T(L.Character.TimePlayed), digest.SessionPlay, scale);
        DrawSessionCell(drawList, new Vector2(left + pad + cellWidth, originY), cellWidth, cell, ActivityArt.Tint(0),
            FontAwesomeIcon.Bolt, Loc.T(L.Character.Experience), digest.SessionExperience, scale);
        DrawSessionCell(drawList, new Vector2(left + pad, originY + cell), cellWidth, cell, ActivityArt.Tint(1),
            FontAwesomeIcon.Dungeon, Loc.T(L.Character.Duties), digest.SessionDuties, scale);
        DrawSessionCell(drawList, new Vector2(left + pad + cellWidth, originY + cell), cellWidth, cell,
            ActivityArt.Tint(2), FontAwesomeIcon.Coins, Loc.T(L.Character.GilEarned), digest.SessionGil, scale);
        return max.Y;
    }

    private void DrawSessionCell(ImDrawListPtr drawList, Vector2 origin, float width, float height, Vector4 tint,
        FontAwesomeIcon icon, string label, string value, float scale)
    {
        var glyph = Metrics.Size.IconTile * scale;
        var centerY = origin.Y + height * 0.5f;
        ActivityArt.GlyphTile(drawList, new Vector2(origin.X + glyph * 0.5f, centerY), glyph, tint, icon);
        var textLeft = origin.X + glyph + ActivityArt.TileGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - textLeft - HintGap * scale);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var textTop = centerY - (labelHeight + valueHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + labelHeight),
            Typography.FitText(value, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
    }

    private float DrawRetainers(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (tracker.RetainerCount <= 0)
        {
            return top;
        }

        var rowTop = top + ActivityArt.TileGap * scale;
        var rect = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + RetainerRowHeight * scale));
        var canOpen = navigation.IsAvailable(TimersAppId);
        var hovered = canOpen && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID("character.retainers"), pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = ActivityArt.CardPad * scale;
        var glyph = TileGlyph * scale;
        var centerY = (min.Y + max.Y) * 0.5f;
        ActivityArt.GlyphTile(drawList, new Vector2(min.X + pad + glyph * 0.5f, centerY), glyph, Accent.Amber,
            FontAwesomeIcon.Briefcase);
        var right = max.X - pad;
        if (canOpen)
        {
            ActivityArt.Chevron(drawList, new Vector2(right - HeroChevronSize * 0.5f * scale, centerY),
                HeroChevronSize * scale, ui.MutedInk, scale);
            right -= HeroChevronSize * scale + ActivityArt.TileGap * scale;
        }

        var value = digest.RetainerValue;
        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), value,
            digest.RetainersReady ? ui.Accent : ui.MutedInk, TextStyles.SubheadlineEmphasized);
        var textLeft = min.X + pad + glyph + ActivityArt.TileGap * scale;
        var label = Typography.FitText(Loc.T(L.Character.Retainers),
            MathF.Max(1f, right - valueSize.X - HintGap * scale - textLeft), TextStyles.Headline);
        var labelHeight = Typography.Measure(label, TextStyles.Headline).Y;
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Headline);
        if (canOpen && UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(TimersAppId);
        }

        return rect.Max.Y;
    }

    private float DrawAwards(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Character.Awards), scale);
        var columns = (int)AwardColumns;
        var columnWidth = width / columns;
        var medallion = AwardRadius * scale;
        var nameHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var cellHeight = medallion * 2f + AwardNameGap * scale + nameHeight + subHeight;
        var rows = (ActivityAwards.Count + columns - 1) / columns;
        var textWidth = MathF.Max(1f, columnWidth - LineGap * 2f * scale);
        for (var index = 0; index < ActivityAwards.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellMin = new Vector2(left + columnWidth * column, cursorY + row * (cellHeight + AwardRowGap * scale));
            var cell = new Rect(cellMin, cellMin + new Vector2(columnWidth, cellHeight));
            var earned = digest.AwardEarned[index];
            var award = (ActivityAward)index;
            var centerX = cell.Center.X;
            ActivityArt.Medallion(drawList, new Vector2(centerX, cellMin.Y + medallion), medallion,
                ActivityArt.AwardTint(award), ActivityArt.AwardIcon(award), earned);
            var nameTop = cellMin.Y + medallion * 2f + AwardNameGap * scale;
            WidgetText.Centered(drawList, centerX, nameTop, textWidth, Loc.T(AwardNames[index]),
                earned ? ui.TitleInk : ui.MutedInk, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, new Vector2(centerX, nameTop + nameHeight + subHeight * 0.5f),
                Typography.FitText(digest.AwardSubs[index], textWidth, TextStyles.Footnote),
                earned ? ActivityArt.AwardTint(award) : ui.MutedInk, TextStyles.Footnote);
            HoverTooltip.Show(AwardTooltipIds[index], cell, Loc.T(AwardDescriptions[index]), HoverLabelSide.Above);
        }

        return cursorY + rows * cellHeight + (rows - 1) * AwardRowGap * scale;
    }
}
