using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const float DetailIconSize = 44f;
    private const float TimelineHeight = 70f;
    private const float TimelineNodeRadius = 7f;
    private const float TimelineLineThickness = 2f;
    private const float TimelineLabelGap = 8f;
    private const int DetailImageColumns = 3;
    private const float DetailImageGap = 6f;
    private const float ReplyIconSize = 30f;
    private const float ReplyWashAlpha = 0.1f;

    private MyFeedbackDto? detailCacheItem;
    private LanguageInfo? detailCacheLanguage;
    private int detailCacheClock = -1;
    private DateTime detailCacheDay;
    private long detailCacheMinute = -1;
    private string detailSentStamp = string.Empty;
    private string detailResolvedStamp = string.Empty;
    private string detailReplyAgo = string.Empty;

    private void DrawDetail(Rect area, string feedbackId)
    {
        var item = FindHistoryItem(feedbackId);
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var category = FeedbackKinds.Parse(item?.Category);
        ref readonly var kind = ref FeedbackKinds.Of(category);
        if (item is not null)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var scale = UiScale.Current;
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                RefreshDetailStamps(item);
                NoteDetailViewed(item.Id);
                var cursorY = DrawDetailStatus(drawList, origin, width, item, in kind, scale);
                var reply = item.Reply ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    cursorY += SectionGap * scale;
                    cursorY = DrawDetailReply(drawList, new Vector2(origin.X, cursorY), width, reply, scale);
                }

                cursorY += SectionGap * scale;
                cursorY += DrawSectionHeader(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Feedback.YourMessage), ui.TitleInk);
                cursorY += HeaderGap * scale;
                cursorY = DrawDetailMessage(drawList, new Vector2(origin.X, cursorY), width, item.Text ?? string.Empty,
                    scale);
                var urls = item.ImageUrls ?? Array.Empty<string>();
                if (urls.Length > 0)
                {
                    cursorY += SectionGap * scale;
                    cursorY += DrawSectionHeader(drawList, new Vector2(origin.X, cursorY), width,
                        Loc.T(L.Feedback.Screenshots), ui.TitleInk);
                    cursorY += HeaderGap * scale;
                    cursorY = DrawDetailImages(drawList, new Vector2(origin.X, cursorY), width, urls, scale);
                }

                ReserveTo(origin, width, cursorY + BottomBreathing * scale);
            }
        }

        var backTitle = router.TryGetView(router.Depth - 2, out var previous) &&
                        previous.Screen == FeedbackScreen.History
            ? Loc.T(L.Feedback.YourFeedback)
            : DisplayName;
        AppHeader.EndLargeTitle(in navBar, context, "feedback.detail.nav", Loc.T(kind.Title), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
        if (item is null && router.Current.Screen == FeedbackScreen.Detail)
        {
            router.Pop(false);
        }
    }

    private MyFeedbackDto? FindHistoryItem(string feedbackId)
    {
        var items = store.History;
        for (var index = 0; index < items.Length; index++)
        {
            if (string.Equals(items[index].Id, feedbackId, StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        return null;
    }

    private void NoteDetailViewed(string feedbackId)
    {
        store.NoteViewing(feedbackId);
        if (store.IsUnseen(feedbackId))
        {
            store.MarkViewed(feedbackId);
        }
    }

    private void RefreshDetailStamps(MyFeedbackDto item)
    {
        var today = DateTime.Today;
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        if (ReferenceEquals(detailCacheItem, item) && ReferenceEquals(detailCacheLanguage, Loc.Current) &&
            detailCacheClock == TimeText.FormatVersion && detailCacheDay == today && detailCacheMinute == minute)
        {
            return;
        }

        detailCacheItem = item;
        detailCacheLanguage = Loc.Current;
        detailCacheClock = TimeText.FormatVersion;
        detailCacheDay = today;
        detailCacheMinute = minute;
        detailSentStamp = TimeText.Stamp(item.CreatedAtUnix);
        detailResolvedStamp = item.ResolvedAtUnix > 0 ? TimeText.Stamp(item.ResolvedAtUnix) : string.Empty;
        detailReplyAgo = item.RepliedAtUnix > 0 ? TimeText.Ago(item.RepliedAtUnix) : string.Empty;
    }

    private float DrawDetailStatus(ImDrawListPtr drawList, Vector2 origin, float width, MyFeedbackDto item,
        in FeedbackKind kind, float scale)
    {
        var status = FeedbackStatuses.Parse(item.Status);
        var reason = FeedbackStatuses.ParseReason(item.Reason);
        var pad = CardPad * scale;
        var iconSize = DetailIconSize * scale;
        var textWidth = width - pad * 2f;
        var hint = Loc.T(FeedbackStatuses.Explanation(status, reason));
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        var height = pad + iconSize + Metrics.Space.Md * scale + TimelineHeight * scale + Metrics.Space.Sm * scale
                     + hintHeight + pad;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);

        var iconCenter = new Vector2(min.X + pad + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        FeedbackArt.CategoryTile(drawList, iconCenter, iconSize, in kind);
        var statusLabel = Loc.T(FeedbackStatuses.Label(status));
        var pillWidth = FeedbackArt.PillWidth(statusLabel, scale);
        var pillHeight = FeedbackArt.PillHeight * scale;
        FeedbackArt.StatusPill(drawList, new Vector2(max.X - pad - pillWidth, iconCenter.Y - pillHeight * 0.5f),
            statusLabel, FeedbackStatuses.Tint(status), scale);
        var textLeft = iconCenter.X + iconSize * 0.5f + Metrics.Space.Md * scale;
        var labelWidth = MathF.Max(1f, max.X - pad - pillWidth - Metrics.Space.Sm * scale - textLeft);
        var title = Typography.FitText(Loc.T(kind.Title), labelWidth, TextStyles.Headline);
        var stamp = Typography.FitText(detailSentStamp, labelWidth, TextStyles.Footnote);
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        var stampHeight = Typography.Measure(stamp, TextStyles.Footnote).Y;
        var textTop = iconCenter.Y - (titleHeight + stampHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), title, ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight), stamp, ui.MutedInk,
            TextStyles.Footnote);

        var timelineTop = min.Y + pad + iconSize + Metrics.Space.Md * scale;
        DrawTimeline(drawList, new Vector2(min.X + pad, timelineTop), textWidth, status, scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, timelineTop + (TimelineHeight + Metrics.Space.Sm) * scale),
            hint, ui.MutedInk, TextStyles.Subheadline, textWidth);
        return max.Y;
    }

    private void DrawTimeline(ImDrawListPtr drawList, Vector2 origin, float width, FeedbackStatus status,
        float scale)
    {
        var steps = FeedbackStatuses.TimelineSteps(status);
        var doneSteps = FeedbackStatuses.CompletedSteps(status);
        var columnWidth = width / steps;
        var nodeRadius = TimelineNodeRadius * scale;
        var nodeY = origin.Y + nodeRadius + Metrics.Space.Xxs * scale;
        var finalTint = FeedbackStatuses.Tint(status);
        for (var step = 0; step < steps - 1; step++)
        {
            var fromX = origin.X + columnWidth * (step + 0.5f);
            var toX = fromX + columnWidth;
            var lineDone = step + 1 < doneSteps;
            drawList.AddLine(new Vector2(fromX + nodeRadius, nodeY), new Vector2(toX - nodeRadius, nodeY),
                ImGui.GetColorU32(lineDone ? StepTint(step + 1, steps, finalTint) : ui.Hairline),
                TimelineLineThickness * scale);
        }

        for (var step = 0; step < steps; step++)
        {
            var centerX = origin.X + columnWidth * (step + 0.5f);
            var center = new Vector2(centerX, nodeY);
            var done = step < doneSteps;
            var current = done && step == doneSteps - 1;
            var tint = current ? finalTint : StepTint(step, steps, finalTint);
            if (done)
            {
                drawList.AddCircleFilled(center, nodeRadius, ImGui.GetColorU32(tint), 20);
                if (current)
                {
                    drawList.AddCircle(center, nodeRadius * 1.7f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.35f)),
                        24, TimelineLineThickness * scale);
                }
            }
            else
            {
                drawList.AddCircle(center, nodeRadius, ImGui.GetColorU32(ui.MutedInk), 20,
                    TimelineLineThickness * scale);
            }

            var label = Typography.FitText(Loc.T(FeedbackStatuses.StepLabel(status, step)),
                columnWidth - Metrics.Space.Xs * scale, TextStyles.FootnoteEmphasized);
            var labelTop = nodeY + nodeRadius + TimelineLabelGap * scale;
            var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, new Vector2(centerX, labelTop + labelHeight * 0.5f), label,
                done ? ui.TitleInk : ui.MutedInk, TextStyles.FootnoteEmphasized);
            var date = StepDate(step, steps, done);
            if (date.Length == 0)
            {
                continue;
            }

            var fittedDate = Typography.FitText(date, columnWidth - Metrics.Space.Xs * scale, TextStyles.Footnote);
            Typography.DrawCentered(drawList,
                new Vector2(centerX, labelTop + labelHeight + Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                fittedDate, ui.MutedInk, TextStyles.Footnote);
        }
    }

    private Vector4 StepTint(int step, int steps, Vector4 finalTint) => step == steps - 1 ? finalTint : ui.Accent;

    private string StepDate(int step, int steps, bool done)
    {
        if (step == 0)
        {
            return detailSentStamp;
        }

        return done && step == steps - 1 ? detailResolvedStamp : string.Empty;
    }

    private float DrawDetailReply(ImDrawListPtr drawList, Vector2 origin, float width, string reply, float scale)
    {
        var pad = CardPad * scale;
        var textWidth = width - pad * 2f;
        var iconSize = ReplyIconSize * scale;
        var gap = Metrics.Space.Sm * scale;
        var bodyHeight = Typography.MeasureWrappedBlock(reply, TextStyles.Body, textWidth).Y;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + pad + iconSize + gap + bodyHeight + pad);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius, true);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, ReplyWashAlpha)));

        var iconCenter = new Vector2(min.X + pad + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        FeedbackArt.GlyphTile(drawList, iconCenter, iconSize, FontAwesomeIcon.Reply, ui.Accent);
        var textLeft = iconCenter.X + iconSize * 0.5f + Metrics.Space.Md * scale;
        var agoWidth = detailReplyAgo.Length > 0 ? Typography.Measure(detailReplyAgo, TextStyles.Footnote).X : 0f;
        if (agoWidth > 0f)
        {
            var agoHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(max.X - pad - agoWidth, iconCenter.Y - agoHeight * 0.5f),
                detailReplyAgo, ui.MutedInk, TextStyles.Footnote);
        }

        var titleWidth = MathF.Max(1f, max.X - pad - agoWidth - Metrics.Space.Sm * scale - textLeft);
        var title = Typography.FitText(Loc.T(L.Feedback.ReplyFrom), titleWidth, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, iconCenter.Y - titleHeight * 0.5f), title, ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + iconSize + gap), reply, ui.TitleInk,
            TextStyles.Body, textWidth);
        return max.Y;
    }

    private float DrawDetailMessage(ImDrawListPtr drawList, Vector2 origin, float width, string text, float scale)
    {
        var pad = CardPad * scale;
        var textWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Body, textWidth).Y;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + textHeight + pad * 2f);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), text, ui.TitleInk, TextStyles.Body,
            textWidth);
        return max.Y;
    }

    private float DrawDetailImages(ImDrawListPtr drawList, Vector2 origin, float width, string[] urls, float scale)
    {
        var gap = DetailImageGap * scale;
        var cell = (width - gap * (DetailImageColumns - 1)) / DetailImageColumns;
        var radius = AttachmentRadius * scale;
        var opened = -1;
        for (var index = 0; index < urls.Length; index++)
        {
            var column = index % DetailImageColumns;
            var row = index / DetailImageColumns;
            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + row * (cell + gap));
            var max = min + new Vector2(cell, cell);
            var texture = remoteImages.Sized(urls[index], cell);
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.FieldSurface));
                LoadingPulse.Spinner((min + max) * 0.5f, Metrics.Space.Sm * scale, ui.Accent, 0.6f, drawList);
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
            }

            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                opened = index;
            }
        }

        if (opened >= 0)
        {
            photoViewer.Open(this, urls.Length, opened, page => remoteImages.Get(urls[page]));
        }

        var rows = (urls.Length + DetailImageColumns - 1) / DetailImageColumns;
        return origin.Y + rows * cell + (rows - 1) * gap;
    }
}
