using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const int HubPreviewRows = 3;
    private const float KindGap = 12f;
    private const float KindIconSize = 40f;
    private const float KindIconGap = 12f;
    private const float KindTitleGap = 2f;
    private const float DraftCardHeight = 68f;
    private const float DraftIconSize = 40f;
    private const float ChevronSize = 5f;
    private const float StatusCardHeight = 76f;
    private const float StatusIconSize = 36f;

    private void DrawHub(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;

            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Feedback.HubIntro),
                ui.MutedInk, TextStyles.Subheadline, width);
            cursorY += SectionGap * scale;

            if (!draft.IsEmpty)
            {
                cursorY = DrawDraftCard(drawList, new Vector2(origin.X, cursorY), width, scale);
                cursorY += SectionGap * scale;
            }

            cursorY += DrawSectionHeader(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Feedback.NewSection), ui.TitleInk);
            cursorY += HeaderGap * scale;
            cursorY = DrawKindGrid(drawList, new Vector2(origin.X, cursorY), width, scale);

            if (store.HistoryVisible)
            {
                cursorY += SectionGap * scale;
                cursorY = DrawHubHistory(drawList, new Vector2(origin.X, cursorY), width, scale);
            }

            cursorY += SectionGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Feedback.PrivacyNote),
                ui.MutedInk, TextStyles.Footnote, width);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "feedback.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawDraftCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + DraftCardHeight * scale);
        var pressed = DrawPressCard(drawList, "feedback.hub.draft", min, max, scale);
        ref readonly var kind = ref FeedbackKinds.Of(draft.Category);
        var pad = CardPad * scale;
        var iconSize = DraftIconSize * scale;
        var center = new Vector2(min.X + pad + iconSize * 0.5f, (min.Y + max.Y) * 0.5f);
        FeedbackArt.CategoryTile(drawList, center, iconSize, in kind);

        var textLeft = center.X + iconSize * 0.5f + KindIconGap * scale;
        var textRight = max.X - pad - ChevronSize * 3f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var title = Typography.FitText(Loc.T(L.Feedback.ContinueDraft), textWidth, TextStyles.Headline);
        var preview = draft.Text.Length > 0 ? draft.Text : Loc.T(L.Feedback.DraftNoText);
        var fittedPreview = Typography.FitText(preview, textWidth, TextStyles.Footnote);
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        var previewHeight = Typography.Measure(fittedPreview, TextStyles.Footnote).Y;
        var blockTop = (min.Y + max.Y - titleHeight - previewHeight - KindTitleGap * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), title, ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop + titleHeight + KindTitleGap * scale), fittedPreview,
            ui.MutedInk, TextStyles.Footnote);
        DrawChevron(drawList, new Vector2(max.X - pad, center.Y), ChevronSize * scale, ui.MutedInk, scale);

        if (pressed)
        {
            router.Push(FeedbackRoute.Compose);
        }

        return max.Y;
    }

    private float DrawKindGrid(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = KindGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = KindTileHeight(tileWidth, scale);
        var kinds = FeedbackKinds.All;
        for (var index = 0; index < kinds.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            if (DrawKindTile(drawList, in kinds[index], min, max, scale))
            {
                StartCompose(kinds[index].Category);
            }
        }

        var bottom = origin.Y + tileHeight * 2f + gap;
        UiAnchors.Report("feedback.kind", new Rect(origin, new Vector2(origin.X + width, bottom)));
        return bottom;
    }

    private float KindTileHeight(float tileWidth, float scale)
    {
        var pad = CardPad * scale;
        var textWidth = MathF.Max(1f, tileWidth - pad * 2f);
        var subtitleHeight = 0f;
        var kinds = FeedbackKinds.All;
        for (var index = 0; index < kinds.Length; index++)
        {
            subtitleHeight = MathF.Max(subtitleHeight,
                Typography.MeasureWrappedBlock(Loc.T(kinds[index].Subtitle), TextStyles.Footnote, textWidth).Y);
        }

        return pad * 2f + KindIconSize * scale + KindIconGap * scale + Typography.LineHeight(TextStyles.Headline)
               + KindTitleGap * scale + subtitleHeight;
    }

    private bool DrawKindTile(ImDrawListPtr drawList, in FeedbackKind kind, Vector2 min, Vector2 max, float scale)
    {
        var pressed = DrawPressCard(drawList, kind.WireName, min, max, scale);
        var pad = CardPad * scale;
        var iconSize = KindIconSize * scale;
        FeedbackArt.CategoryTile(drawList, new Vector2(min.X + pad + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f),
            iconSize, in kind);
        var textWidth = MathF.Max(1f, max.X - min.X - pad * 2f);
        var titleTop = min.Y + pad + iconSize + KindIconGap * scale;
        var title = Typography.FitText(Loc.T(kind.Title), textWidth, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop), title, ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedLeft(
            new Vector2(min.X + pad, titleTop + Typography.LineHeight(TextStyles.Headline) + KindTitleGap * scale),
            Loc.T(kind.Subtitle), ui.MutedInk, TextStyles.Footnote, textWidth);
        return pressed;
    }

    private bool DrawPressCard(ImDrawListPtr drawList, string id, Vector2 min, Vector2 max, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.CardPressedScale);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * grow;
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, center - half, center + half, radius, true);
        if (hovered)
        {
            Squircle.Fill(drawList, center - half, center + half, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private float DrawHubHistory(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rows = hubRows.Rows(store.History, store.HistoryRevision, width, scale);
        var state = store.HistoryState;
        var showSeeAll = state == FeedbackHistoryState.Ready && (rows.Length > HubPreviewRows || store.HasMoreHistory);
        var headerHeight = DrawSectionHeader(drawList, origin, width * (showSeeAll ? 0.6f : 1f),
            Loc.T(L.Feedback.YourFeedback), ui.TitleInk);
        if (showSeeAll)
        {
            var label = Loc.T(L.Feedback.SeeAll);
            var size = Typography.Measure(label, TextStyles.Body);
            var linkMin = new Vector2(origin.X + width - size.X, origin.Y + (headerHeight - size.Y) * 0.5f);
            var linkMax = linkMin + size;
            var hovered = UiInteract.Hover(linkMin, linkMax);
            Typography.Draw(drawList, linkMin, label, hovered ? Palette.Lighten(ui.Accent, 0.2f) : ui.Accent,
                TextStyles.Body);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(linkMin, linkMax, hovered))
            {
                router.Push(FeedbackRoute.History);
            }
        }

        var top = origin.Y + headerHeight + HeaderGap * scale;
        if (state == FeedbackHistoryState.Ready && rows.Length > 0)
        {
            return DrawHubRows(drawList, new Vector2(origin.X, top), width, rows, scale);
        }

        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + StatusCardHeight * scale);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);
        if (state == FeedbackHistoryState.Ready)
        {
            DrawHubStatus(drawList, min, max, FontAwesomeIcon.PaperPlane, ui.Accent,
                Loc.T(L.Feedback.HistoryEmptyTitle), scale);
        }
        else
        {
            LoadingPulse.Spinner(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
                Metrics.Space.Md * scale, ui.Accent, 1f, drawList);
        }

        return max.Y;
    }

    private void DrawHubStatus(ImDrawListPtr drawList, Vector2 min, Vector2 max, FontAwesomeIcon icon, Vector4 tint,
        string title, float scale)
    {
        var pad = CardPad * scale;
        var iconSize = StatusIconSize * scale;
        var center = new Vector2(min.X + pad + iconSize * 0.5f, (min.Y + max.Y) * 0.5f);
        FeedbackArt.GlyphTile(drawList, center, iconSize, icon, tint);
        var textLeft = center.X + iconSize * 0.5f + KindIconGap * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, max.X - pad - textLeft), TextStyles.Headline);
        var titleSize = Typography.Measure(fitted, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, center.Y - titleSize.Y * 0.5f), fitted, ui.TitleInk,
            TextStyles.Headline);
    }

    private float DrawHubRows(ImDrawListPtr drawList, Vector2 origin, float width, HistoryRow[] rows, float scale)
    {
        var count = Math.Min(rows.Length, HubPreviewRows);
        var height = 0f;
        for (var index = 0; index < count; index++)
        {
            height += rows[index].Height;
        }

        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius, true);
        var rowTop = origin.Y;
        for (var index = 0; index < count; index++)
        {
            var rowMin = new Vector2(min.X, rowTop);
            var rowMax = new Vector2(max.X, rowTop + rows[index].Height);
            var hovered = UiInteract.Hover(rowMin, rowMax);
            if (hovered)
            {
                drawList.PushClipRect(rowMin, rowMax, true);
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
                drawList.PopClipRect();
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            DrawHistoryRow(drawList, new Rect(rowMin, rowMax), in rows[index], hubRows, scale);
            if (index < count - 1)
            {
                FeedCell.Hairline(drawList, rowMin.X + FeedbackHistoryRows.TextInset(scale), rowMax.X, rowMax.Y, ui.Hairline);
            }

            if (UiInteract.Click(rowMin, rowMax, hovered))
            {
                router.Push(FeedbackRoute.Detail(rows[index].Id));
            }

            rowTop = rowMax.Y;
        }

        return max.Y;
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 tip, float size, Vector4 color, float scale)
    {
        var packed = ImGui.GetColorU32(color);
        var thickness = Metrics.Stroke.Ring * scale;
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    private void StartCompose(FeedbackCategory category)
    {
        if (draft.Category != category)
        {
            draft.Category = category;
            sendFailure.Clear();
        }

        router.Push(FeedbackRoute.Compose);
    }
}
