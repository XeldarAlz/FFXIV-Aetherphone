using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const float PhotoGlyphScale = 0.62f;
    private const float PhotoGlyphGap = 5f;
    private const float KindInkLighten = 0.3f;
    private const float UnseenDotRadius = 4f;
    private const float AccessoryMinWidth = 24f;

    private readonly FeedbackHistoryRows hubRows = new();
    private readonly FeedbackHistoryRows listRows = new();
    private readonly PullToRefresh historyRefresh = new();

    private void DrawHistory(Rect area)
    {
        DropThanksUnderHistory();
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        var state = store.HistoryState;
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            historyRefresh.Draw(body, surface.Pull, surface.Dragging, store.RefreshingHistory, ui.MutedInk,
                refreshHistory);
            var scale = UiScale.Current;
            var width = ScrollLayout.StableContentWidth();
            var rows = listRows.Rows(store.History, store.HistoryRevision, width, scale);
            if (state != FeedbackHistoryState.Ready || rows.Length == 0)
            {
                DrawHistoryState(body, state, width, scale);
            }
            else
            {
                DrawHistoryList(rows, scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "feedback.history.nav", Loc.T(L.Feedback.YourFeedback),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
        if (state == FeedbackHistoryState.Unsupported && router.Current.Screen == FeedbackScreen.History)
        {
            router.Pop(false);
        }
    }

    private void DropThanksUnderHistory()
    {
        if (router.IsTransitioning || router.Current.Screen != FeedbackScreen.History ||
            !router.TryGetView(router.Depth - 2, out var previous) || previous.Screen != FeedbackScreen.Sent)
        {
            return;
        }

        router.Reset();
        router.Push(FeedbackRoute.History, false);
    }

    private void DrawHistoryList(HistoryRow[] rows, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        using (ZeroItemSpacing())
        {
            for (var index = 0; index < rows.Length; index++)
            {
                var cell = FeedCell.Begin(drawList, rows[index].Height, ui.HoverWash);
                DrawHistoryRow(drawList, cell.Bounds, in rows[index], listRows, scale);
                FeedCell.Hairline(drawList, cell.Bounds.Min.X + FeedbackHistoryRows.TextInset(scale),
                    cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
                FeedCell.End(drawList, in cell, ui.Hairline, false);
                if (cell.Tapped)
                {
                    router.Push(FeedbackRoute.Detail(rows[index].Id));
                }
            }
        }

        if (store.LoadingMoreHistory)
        {
            InfiniteScroll.DrawLoadingRow(ImGui.GetCursorScreenPos().X + ScrollLayout.StableContentWidth() * 0.5f,
                ui.MutedInk);
        }
        else if (store.HasMoreHistory && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreHistory();
        }

        ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
    }

    private void DrawHistoryState(Rect body, FeedbackHistoryState state, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var textWidth = MathF.Max(1f, width - inset * 2f);
        var centerX = origin.X + width * 0.5f;
        if (state != FeedbackHistoryState.Ready)
        {
            LoadingPulse.Draw(new Vector2(centerX, body.Min.Y + body.Height * 0.3f), Metrics.Space.Md * scale,
                ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            ReserveTo(origin, width, body.Max.Y - Metrics.Space.Xxl * scale);
            return;
        }

        var title = Loc.T(L.Feedback.HistoryEmptyTitle);
        var hint = Loc.T(L.Feedback.HistoryEmptyHint);
        var height = FeedbackArt.StatePanelHeight(title, hint, true, textWidth, scale);
        var top = MathF.Max(origin.Y + Metrics.Space.Xxl * scale, body.Center.Y - height * 0.6f);
        if (FeedbackArt.StatePanel(ui, top, centerX, textWidth, FontAwesomeIcon.PaperPlane, ui.Accent, title, hint,
                Loc.T(L.Feedback.NewSection), "feedback.history.new"))
        {
            router.Pop();
        }

        ReserveTo(origin, width, top + height);
    }

    private void DrawHistoryRow(ImDrawListPtr drawList, Rect row, in HistoryRow model, FeedbackHistoryRows layout,
        float scale)
    {
        ref readonly var kind = ref FeedbackKinds.Of(model.Category);
        var pad = FeedbackHistoryRows.Pad * scale;
        var padY = FeedbackHistoryRows.PadY * scale;
        var iconSize = FeedbackHistoryRows.IconSize * scale;
        FeedbackArt.CategoryTile(drawList, new Vector2(row.Min.X + pad + iconSize * 0.5f,
            row.Min.Y + padY + iconSize * 0.5f), iconSize, in kind);

        var textLeft = row.Min.X + FeedbackHistoryRows.TextInset(scale);
        var textRight = row.Max.X - pad;
        var cursorY = row.Min.Y + padY;
        var unseen = store.IsUnseen(model.Id);
        var dateSize = Typography.Measure(model.Date, TextStyles.Footnote);
        var dateLeft = textRight - dateSize.X;
        Typography.Draw(drawList, new Vector2(dateLeft, cursorY), model.Date, unseen ? ui.Accent : ui.MutedInk,
            TextStyles.Footnote);
        if (unseen)
        {
            var dotRadius = UnseenDotRadius * scale;
            dateLeft -= dotRadius * 2f + Metrics.Space.Xs * scale;
            drawList.AddCircleFilled(new Vector2(dateLeft + dotRadius, cursorY + layout.MetaHeight * 0.5f), dotRadius,
                ImGui.GetColorU32(ui.Accent), 16);
        }

        var kindLabel = Typography.FitText(Loc.T(kind.Title),
            MathF.Max(1f, dateLeft - Metrics.Space.Sm * scale - textLeft), TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), kindLabel, Palette.Lighten(kind.Tint, KindInkLighten),
            TextStyles.FootnoteEmphasized);
        cursorY += layout.MetaHeight + FeedbackHistoryRows.MetaGap * scale;

        var lineHeight = layout.LineHeight;
        for (var index = 0; index < model.Preview.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), model.Preview[index], ui.TitleInk,
                TextStyles.Subheadline);
            cursorY += lineHeight;
        }

        cursorY += FeedbackHistoryRows.PillGap * scale;
        var statusLabel = Loc.T(FeedbackStatuses.Label(model.Status));
        FeedbackArt.StatusPill(drawList, new Vector2(textLeft, cursorY), statusLabel,
            FeedbackStatuses.Tint(model.Status), scale);
        var accessoryLeft = textLeft + FeedbackArt.PillWidth(statusLabel, scale) + Metrics.Space.Md * scale;
        if (model.HasReply)
        {
            accessoryLeft = DrawRowAccessory(drawList, accessoryLeft, textRight, cursorY, FontAwesomeIcon.Reply,
                Loc.T(L.Feedback.RowReplied), ui.Accent, scale) + Metrics.Space.Md * scale;
        }

        if (model.Photos.Length > 0)
        {
            DrawRowAccessory(drawList, accessoryLeft, textRight, cursorY, FontAwesomeIcon.Image, model.Photos,
                ui.MutedInk, scale);
        }
    }

    private static float DrawRowAccessory(ImDrawListPtr drawList, float left, float right, float top,
        FontAwesomeIcon icon, string label, Vector4 ink, float scale)
    {
        var labelLeft = left + Metrics.Space.Xs * scale * 2f + PhotoGlyphGap * scale;
        var available = right - labelLeft;
        if (available < AccessoryMinWidth * scale)
        {
            return left;
        }

        var pillHeight = FeedbackArt.PillHeight * scale;
        var glyphCenter = new Vector2(left + Metrics.Space.Xs * scale, top + pillHeight * 0.5f);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(icon), ink, PhotoGlyphScale);
        var fitted = Typography.FitText(label, available, TextStyles.Footnote);
        var labelSize = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, top + (pillHeight - labelSize.Y) * 0.5f), fitted, ink,
            TextStyles.Footnote);
        return labelLeft + labelSize.X;
    }
}
