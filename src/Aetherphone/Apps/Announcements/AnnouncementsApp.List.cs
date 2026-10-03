using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const int SearchMaxLength = 64;
    private const float SearchBottomGap = 14f;
    private const float FeatureRadius = Metrics.Radius.Widget;
    private const float FeaturePad = 18f;
    private const float FeatureLane = 18f;
    private const float FeatureEyebrowGap = 10f;
    private const float FeaturePreviewGap = 6f;
    private const float FeatureGlyphScale = 0.72f;
    private const int FeatureTitleLines = 3;
    private const int FeaturePreviewLines = 4;
    private const float SectionTopGap = 22f;
    private const float SectionHeaderGap = 8f;
    private const float SectionHeaderInset = 4f;
    private const float GroupRadius = Metrics.Radius.Grouped;
    private const float RowPadY = 12f;
    private const float RowLane = 26f;
    private const float RowPadRight = 14f;
    private const float RowLineGap = 3f;
    private const int RowTitleLines = 2;
    private const int RowPreviewLines = 2;
    private const float DotRadius = 4.5f;
    private const float TrailingGap = 8f;
    private const float ChevronSize = 4f;
    private const float ChevronGap = 7f;
    private const float RowHoverTarget = 0.5f;
    private const float RowHighlightAlpha = 0.10f;
    private const float NoticeHeight = 44f;
    private const float NoticePad = 14f;
    private const float NoticeGlyphScale = 0.8f;
    private const float NoticeBottomGap = 14f;
    private const float ListBottomGap = 24f;
    private const float SkeletonFeatureHeight = 150f;
    private const float SkeletonRowHeight = 72f;
    private const int SkeletonRows = 3;
    private const float SkeletonPulseSpeed = 3.2f;
    private const float SkeletonAlphaLow = 0.06f;
    private const float SkeletonAlphaHigh = 0.12f;
    private const float SkeletonBarHeight = 10f;
    private const float SkeletonTitleBarHeight = 15f;
    private const string FeaturedId = "announcements.featured";
    private const string NoticeId = "announcements.notice";
    private const string CardAnchor = "announcements.card";

    private readonly PullToRefresh listRefresh = new();
    private readonly NavBarButton[] listButtons = new NavBarButton[1];
    private string query = string.Empty;
    private string appliedQuery = string.Empty;
    private string noResultsHint = string.Empty;
    private bool filterDirty = true;
    private bool searching;
    private int[] visible = Array.Empty<int>();
    private int visibleCount;

    private void DrawList(Rect area, int depth)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        ConsumeReading(depth);
        SyncFilter();

        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading, ui.MutedInk, store.Refresh);
            DrawListContent(body, scale);
        }

        listButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.CheckDouble),
            Loc.T(L.Announcements.MarkAllRead));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "announcements.nav", DisplayName, NavStyle(),
            listButtons.AsSpan(0, anyUnread ? 1 : 0));
        if (pressed == 0)
        {
            store.MarkAllRead();
        }
    }

    private void ConsumeReading(int depth)
    {
        if (readingId is null || depth != router.Depth)
        {
            return;
        }

        store.MarkRead(readingId);
        readingId = null;
    }

    private void SyncFilter()
    {
        if (!filterDirty && ReferenceEquals(query, appliedQuery))
        {
            return;
        }

        filterDirty = false;
        appliedQuery = query;
        var needle = query.Trim();
        searching = needle.Length > 0;
        if (visible.Length < entries.Length)
        {
            visible = new int[entries.Length];
        }

        visibleCount = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (!searching || entries[index].Matches(needle))
            {
                visible[visibleCount++] = index;
            }
        }

        noResultsHint = searching ? Loc.T(L.Announcements.NoResultsHint, needle) : string.Empty;
    }

    private void DrawListContent(Rect body, float scale)
    {
        if (entries.Length == 0)
        {
            DrawEmptyList(body, scale);
            return;
        }

        DrawSearchField(scale);
        if (store.Failed)
        {
            DrawRefreshNotice(scale);
        }

        if (visibleCount == 0)
        {
            var top = ImGui.GetCursorScreenPos().Y;
            AnnouncementsStatePanel.Draw(new Rect(new Vector2(body.Min.X, top), body.Max), ui,
                FontAwesomeIcon.Search, Loc.T(L.Announcements.NoResultsTitle), noResultsHint, string.Empty);
            return;
        }

        var position = 0;
        if (!searching)
        {
            DrawFeatured(entries[visible[0]], scale);
            position = 1;
        }

        while (position < visibleCount)
        {
            position = DrawSection(position, scale);
        }

        if (store.LoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }
        else if (store.HasMore && InfiniteScroll.ReachedBottom())
        {
            store.LoadMore();
        }

        ImGui.Dummy(new Vector2(0f, ListBottomGap * scale));
    }

    private void DrawEmptyList(Rect body, float scale)
    {
        if (store.Loading && !store.LoadedOnce)
        {
            DrawSkeleton(scale);
            return;
        }

        if (store.Failed)
        {
            listFailure.Set(store.Failure);
            if (AnnouncementsStatePanel.Draw(body, ui, FontAwesomeIcon.Wifi, Loc.T(L.Failure.CouldNotLoad),
                    listFailure.Text(), Loc.T(L.Common.Retry)))
            {
                store.Refresh();
            }

            return;
        }

        AnnouncementsStatePanel.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.EmptyTitle),
            Loc.T(L.Announcements.EmptyHint), string.Empty);
    }

    private void DrawSearchField(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Search(drawList, field, "##announcementsSearch", Loc.T(L.Announcements.SearchHint), ref query,
            theme, scale, SearchMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height + SearchBottomGap * scale));
    }

    private void DrawRefreshNotice(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = NoticeHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = UiInteract.Hover(origin, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(NoticeId, pressed, PressFx.CardPressedScale);
        var half = new Vector2(width, height) * 0.5f * press;
        var center = (origin + max) * 0.5f;
        ui.Card(drawList, center - half, center + half, height * 0.5f * press);

        var pad = NoticePad * scale;
        var glyphCenter = new Vector2(origin.X + pad + Metrics.Space.Sm * scale, center.Y);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.ExclamationTriangle), ui.MutedInk,
            NoticeGlyphScale);
        var retry = Loc.T(L.Common.Retry);
        var retrySize = Typography.Measure(retry, TextStyles.FootnoteEmphasized);
        var retryLeft = max.X - pad - retrySize.X;
        Typography.Draw(drawList, new Vector2(retryLeft, center.Y - retrySize.Y * 0.5f), retry, ui.Accent,
            TextStyles.FootnoteEmphasized);
        var textLeft = glyphCenter.X + Metrics.Space.Lg * scale;
        var message = Typography.FitText(Loc.T(L.Announcements.RefreshFailed),
            MathF.Max(1f, retryLeft - Metrics.Space.Sm * scale - textLeft), TextStyles.Footnote);
        var messageHeight = Typography.Measure(message, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(textLeft, center.Y - messageHeight * 0.5f), message, ui.MutedInk,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(origin, max, hovered))
        {
            store.Refresh();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + NoticeBottomGap * scale));
    }

    private void DrawFeatured(AnnouncementEntry entry, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = FeaturePad * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        var titleLines = entry.FeatureTitle.Get(entry.Title, TextStyles.Title2, innerWidth, FeatureTitleLines,
            fontKey);
        var previewLines = entry.FeaturePreview.Get(entry.Preview, TextStyles.Subheadline, innerWidth,
            FeaturePreviewLines, fontKey);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleLineHeight = Typography.LineHeight(TextStyles.Title2);
        var previewLineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var previewBlock = previewLines.Length > 0
            ? FeaturePreviewGap * scale + previewLines.Length * previewLineHeight
            : 0f;
        var height = pad + eyebrowHeight + FeatureEyebrowGap * scale + titleLines.Length * titleLineHeight
            + previewBlock + pad;
        var rest = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        Lift(drawList, rest, FeaturedId, hovered, FeatureRadius * scale, scale);
        UiAnchors.Report(CardAnchor, rest);

        var left = rest.Min.X + pad;
        var right = rest.Max.X - pad;
        var eyebrowCenterY = rest.Min.Y + pad + eyebrowHeight * 0.5f;
        DrawFeatureLane(drawList, entry, new Vector2(left + FeatureLane * 0.5f * scale - DotRadius * scale,
            eyebrowCenterY), scale);

        var eyebrow = Loc.Upper(Loc.T(L.Announcements.Latest));
        var eyebrowSize = Typography.Measure(eyebrow, TextStyles.FootnoteEmphasized);
        var eyebrowLeft = left + FeatureLane * scale;
        Typography.Draw(drawList, new Vector2(eyebrowLeft, eyebrowCenterY - eyebrowSize.Y * 0.5f), eyebrow,
            ui.Accent, TextStyles.FootnoteEmphasized);
        var metaSpace = right - (eyebrowLeft + eyebrowSize.X + Metrics.Space.Md * scale);
        if (metaSpace > 0f)
        {
            var meta = Typography.FitText(entry.Meta, metaSpace, TextStyles.Footnote);
            var metaSize = Typography.Measure(meta, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(right - metaSize.X, eyebrowCenterY - metaSize.Y * 0.5f), meta,
                ui.MutedInk, TextStyles.Footnote);
        }

        var cursorY = rest.Min.Y + pad + eyebrowHeight + FeatureEyebrowGap * scale;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY), titleLines[lineIndex], ui.TitleInk,
                TextStyles.Title2);
            cursorY += titleLineHeight;
        }

        if (previewLines.Length > 0)
        {
            cursorY += FeaturePreviewGap * scale;
            for (var lineIndex = 0; lineIndex < previewLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(left, cursorY), previewLines[lineIndex], ui.MutedInk,
                    TextStyles.Subheadline);
                cursorY += previewLineHeight;
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            Open(entry);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawFeatureLane(ImDrawListPtr drawList, AnnouncementEntry entry, Vector2 center, float scale)
    {
        var dot = entry.StepDot(store.IsUnread(entry.Source), deltaSeconds);
        if (dot > 0.001f)
        {
            drawList.AddCircleFilled(center, DotRadius * scale, ImGui.GetColorU32(ui.Accent with { W = dot }), 16);
        }

        if (dot < 0.999f)
        {
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Bullhorn), ui.Accent with { W = 1f - dot },
                FeatureGlyphScale);
        }
    }

    private int DrawSection(int start, float scale)
    {
        var first = entries[visible[start]];
        var end = start + 1;
        while (end < visibleCount && entries[visible[end]].LocalDay == first.LocalDay)
        {
            end++;
        }

        DrawSectionHeader(first.DayLabel, start == 0 ? Metrics.Space.Xs : SectionTopGap, scale);

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var total = 0f;
        for (var position = start; position < end; position++)
        {
            total += MeasureRow(entries[visible[position]], origin.X, origin.X + width, scale, out _, out _, out _);
        }

        var cardMax = new Vector2(origin.X + width, origin.Y + total);
        ui.Card(drawList, origin, cardMax, GroupRadius * scale);
        var clipTop = ImGui.GetWindowPos().Y;
        var clipBottom = clipTop + ImGui.GetWindowSize().Y;
        var rowTop = origin.Y;
        for (var position = start; position < end; position++)
        {
            var entry = entries[visible[position]];
            var height = MeasureRow(entry, origin.X, cardMax.X, scale, out var titleLines, out var previewLines,
                out var clockWidth);
            var row = new Rect(new Vector2(origin.X, rowTop), new Vector2(cardMax.X, rowTop + height));
            if (searching && position == 0)
            {
                UiAnchors.Report(CardAnchor, row);
            }

            if (row.Max.Y >= clipTop && row.Min.Y <= clipBottom)
            {
                DrawRow(drawList, entry, row, titleLines, previewLines, clockWidth, position == start,
                    position == end - 1, scale);
            }

            rowTop += height;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total));
        return end;
    }

    private void DrawSectionHeader(string label, float gapUnits, float scale)
    {
        ImGui.Dummy(new Vector2(0f, gapUnits * scale));
        var origin = ImGui.GetCursorScreenPos();
        var size = Typography.Measure(label, TextStyles.Title3);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + SectionHeaderInset * scale, origin.Y), label,
            ui.TitleInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), size.Y + SectionHeaderGap * scale));
    }

    private float MeasureRow(AnnouncementEntry entry, float left, float right, float scale, out string[] titleLines,
        out string[] previewLines, out float clockWidth)
    {
        var textLeft = left + RowLane * scale;
        var textRight = right - RowPadRight * scale;
        clockWidth = Typography.Measure(entry.Clock, TextStyles.Footnote).X;
        var trailing = clockWidth + (ChevronGap + ChevronSize + TrailingGap) * scale;
        titleLines = entry.RowTitle.Get(entry.Title, TextStyles.Headline, MathF.Max(1f, textRight - trailing - textLeft),
            RowTitleLines, fontKey);
        previewLines = entry.RowPreview.Get(entry.Preview, TextStyles.Subheadline, MathF.Max(1f, textRight - textLeft),
            RowPreviewLines, fontKey);
        var titleBlock = Math.Max(1, titleLines.Length) * headlineLineHeight;
        var previewBlock = previewLines.Length > 0
            ? RowLineGap * scale + previewLines.Length * subheadlineLineHeight
            : 0f;
        return RowPadY * 2f * scale + titleBlock + previewBlock;
    }

    private void DrawRow(ImDrawListPtr drawList, AnnouncementEntry entry, Rect row, string[] titleLines,
        string[] previewLines, float clockWidth, bool first, bool last, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var highlight = entry.StepHighlight(pressed ? 1f : hovered ? RowHoverTarget : 0f, deltaSeconds);
        if (highlight > 0.001f)
        {
            var corners = (first ? ImDrawFlags.RoundCornersTop : ImDrawFlags.None)
                | (last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.None);
            if (corners == ImDrawFlags.None)
            {
                corners = ImDrawFlags.RoundCornersNone;
            }

            drawList.AddRectFilled(row.Min, row.Max,
                ImGui.GetColorU32(ui.TitleInk with { W = RowHighlightAlpha * highlight }), GroupRadius * scale,
                corners);
        }

        var textLeft = row.Min.X + RowLane * scale;
        var textRight = row.Max.X - RowPadRight * scale;
        var titleLineHeight = headlineLineHeight;
        var cursorY = row.Min.Y + RowPadY * scale;
        var firstLineCenterY = cursorY + Typography.Measure("Ag", TextStyles.Headline).Y * 0.5f;

        var dot = entry.StepDot(store.IsUnread(entry.Source), deltaSeconds);
        if (dot > 0.001f)
        {
            drawList.AddCircleFilled(new Vector2(row.Min.X + RowLane * 0.5f * scale, firstLineCenterY),
                DotRadius * scale, ImGui.GetColorU32(ui.Accent with { W = dot }), 16);
        }

        var chevronTip = new Vector2(textRight, firstLineCenterY);
        DrawChevron(drawList, chevronTip, ChevronSize * scale, Metrics.Stroke.Thin * scale,
            hovered ? ui.TitleInk : ui.MutedInk with { W = ui.MutedInk.W * 0.7f }, true);
        var clockHeight = Typography.Measure(entry.Clock, TextStyles.Footnote).Y;
        Typography.Draw(drawList,
            new Vector2(chevronTip.X - ChevronSize * scale - ChevronGap * scale - clockWidth,
                firstLineCenterY - clockHeight * 0.5f), entry.Clock, ui.MutedInk, TextStyles.Footnote);

        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk,
                TextStyles.Headline);
            cursorY += titleLineHeight;
        }

        if (titleLines.Length == 0)
        {
            cursorY += titleLineHeight;
        }

        if (previewLines.Length > 0)
        {
            cursorY += RowLineGap * scale;
            var previewLineHeight = subheadlineLineHeight;
            for (var lineIndex = 0; lineIndex < previewLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), previewLines[lineIndex], ui.MutedInk,
                    TextStyles.Subheadline);
                cursorY += previewLineHeight;
            }
        }

        if (!last)
        {
            FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, ui.Hairline);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            Open(entry);
        }
    }

    private void DrawSkeleton(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * SkeletonPulseSpeed);
        var ink = ui.TitleInk with { W = SkeletonAlphaLow + (SkeletonAlphaHigh - SkeletonAlphaLow) * pulse };
        var packed = ImGui.GetColorU32(ink);
        var pad = FeaturePad * scale;

        var featureMax = new Vector2(origin.X + width, origin.Y + SkeletonFeatureHeight * scale);
        ui.Card(drawList, origin, featureMax, FeatureRadius * scale, true);
        var barY = origin.Y + pad;
        SkeletonBar(drawList, origin.X + pad, barY, width * 0.22f, SkeletonBarHeight * scale, packed);
        barY += (SkeletonBarHeight + FeatureEyebrowGap + Metrics.Space.Xxs) * scale;
        SkeletonBar(drawList, origin.X + pad, barY, width * 0.78f, SkeletonTitleBarHeight * scale, packed);
        barY += (SkeletonTitleBarHeight + Metrics.Space.Sm) * scale;
        SkeletonBar(drawList, origin.X + pad, barY, width * 0.52f, SkeletonTitleBarHeight * scale, packed);
        barY += (SkeletonTitleBarHeight + Metrics.Space.Md) * scale;
        SkeletonBar(drawList, origin.X + pad, barY, width - pad * 2f, SkeletonBarHeight * scale, packed);
        barY += (SkeletonBarHeight + Metrics.Space.Sm) * scale;
        SkeletonBar(drawList, origin.X + pad, barY, width * 0.64f, SkeletonBarHeight * scale, packed);

        var groupTop = featureMax.Y + (SectionTopGap + SectionHeaderGap) * scale + Typography.LineHeight(TextStyles.Title3);
        SkeletonBar(drawList, origin.X + SectionHeaderInset * scale,
            groupTop - SectionHeaderGap * scale - SkeletonTitleBarHeight * scale - Metrics.Space.Xxs * scale,
            width * 0.30f, SkeletonTitleBarHeight * scale, packed);
        var rowHeight = SkeletonRowHeight * scale;
        var groupMax = new Vector2(origin.X + width, groupTop + rowHeight * SkeletonRows);
        ui.Card(drawList, new Vector2(origin.X, groupTop), groupMax, GroupRadius * scale);
        var textLeft = origin.X + RowLane * scale;
        for (var rowIndex = 0; rowIndex < SkeletonRows; rowIndex++)
        {
            var rowTop = groupTop + rowIndex * rowHeight;
            var lineY = rowTop + RowPadY * scale;
            SkeletonBar(drawList, textLeft, lineY, width * 0.55f, SkeletonTitleBarHeight * scale, packed);
            lineY += (SkeletonTitleBarHeight + Metrics.Space.Sm) * scale;
            SkeletonBar(drawList, textLeft, lineY, width * 0.74f, SkeletonBarHeight * scale, packed);
            lineY += (SkeletonBarHeight + Metrics.Space.Xs) * scale;
            SkeletonBar(drawList, textLeft, lineY, width * 0.46f, SkeletonBarHeight * scale, packed);
            if (rowIndex < SkeletonRows - 1)
            {
                FeedCell.Hairline(drawList, textLeft, groupMax.X, rowTop + rowHeight, ui.Hairline);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, groupMax.Y - origin.Y));
    }

    private static void SkeletonBar(ImDrawListPtr drawList, float left, float top, float width, float height,
        uint color)
    {
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(left + width, top + height), color, height * 0.5f);
    }
}
