using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal enum QueueRowKind : byte
{
    Header,
    History,
    Queued,
    Autoplay,
    Jam,
    Empty,
}

internal enum QueueHeaderKind : byte
{
    None,
    History,
    PlayingNext,
    Upcoming,
    ContinuePlaying,
    Jam,
}

internal readonly record struct QueueRow(QueueRowKind Kind, QueueHeaderKind Header, int EntryId, Song Song,
    string Caption, int ReorderIndex, float Top, float Height, bool ShowClear);

internal sealed partial class MusicApp
{
    private const float QueueBarHeight = 50f;
    private const float QueueHeaderHeight = 40f;
    private const float QueueRowHeight = 58f;
    private const float QueueEmptyHeight = 80f;
    private const float QueueArtUnits = 42f;
    private const float QueueRowRounding = 12f;
    private const float QueueHandleWidth = 30f;
    private const float QueueMenuRadius = 14f;
    private const float QueueToggleRadius = 15f;
    private const float QueueToggleSpacing = 38f;
    private const float QueueToggleGlyph = 7f;
    private const float QueueBottomPadding = 16f;
    private const float QueueAutoscrollEdge = 44f;
    private const float QueueAutoscrollSpeed = 520f;
    private const float QueueDragLift = 1.02f;
    private const float QueueHistoryAlpha = 0.55f;
    private const float QueueHoverAlpha = 0.06f;
    private const float QueueDragFillAlpha = 0.32f;
    private const float RepeatBadgeRadius = 6f;
    private const int QueueInitialCapacity = 32;
    private const uint QueuePressSalt = 2654435761u;

    private readonly PaneScroll queueScroll = new();
    private readonly ActionSheet queueMenu = new();
    private readonly ActionSheet.Item[] queueMenuItems = new ActionSheet.Item[2];
    private QueueRow[] queueRows = new QueueRow[QueueInitialCapacity];
    private float[] reorderCenters = new float[QueueInitialCapacity];
    private Spring[] reorderShift = new Spring[QueueInitialCapacity];
    private int queueRowCount;
    private int reorderCount;
    private float queueContentHeight;
    private int builtQueueVersion = -1;
    private int builtJamVersion = -1;
    private bool builtInJam;
    private bool builtHistoryExpanded;
    private bool builtAutoplay;
    private float builtQueueScale;
    private string builtQueueLanguage = string.Empty;
    private bool historyExpanded;
    private bool queueDragActive;
    private int queueDragEntry;
    private int queueDragSource;
    private int queueDragTarget;
    private float queueDragGrab;
    private float queueDragTopContent;
    private int queueSettleEntry = -1;
    private bool queueSettlePending;
    private float queueSettleFrom;
    private Spring queueSettle;
    private QueueRow queueMenuRow;

    private bool QueueDragging => queueDragActive;

    private void CancelQueueDrag()
    {
        queueDragActive = false;
        queueSettleEntry = -1;
        queueSettlePending = false;
        for (var index = 0; index < reorderShift.Length; index++)
        {
            reorderShift[index].SnapTo(0f);
        }
    }

    private void DrawUpNextPane(ImDrawListPtr drawList, Rect area, bool interactive, float scale, float delta)
    {
        EnsureQueueRows(scale);
        var inset = NowPlayingSide * scale;
        var bar = new Rect(new Vector2(area.Min.X + inset, area.Min.Y),
            new Vector2(area.Max.X - inset, area.Min.Y + QueueBarHeight * scale));
        DrawQueueBar(drawList, bar, interactive, scale);
        var list = new Rect(new Vector2(area.Min.X, bar.Max.Y), area.Max);
        if (list.Height <= 1f)
        {
            return;
        }

        var maximum = queueContentHeight + QueueBottomPadding * scale - list.Height;
        queueScroll.Update(list, queueContentHeight, maximum, interactive, queueDragActive || OverQueueHandle(list),
            delta);
        UpdateQueueDrag(list, interactive, scale, delta);
        EnsureQueueRows(scale);
        drawList.PushClipRect(list.Min, list.Max, true);
        var offset = queueScroll.Offset;
        var dragRow = -1;
        for (var rowIndex = 0; rowIndex < queueRowCount; rowIndex++)
        {
            ref readonly var row = ref queueRows[rowIndex];
            if (queueDragActive && row.ReorderIndex >= 0 && row.EntryId == queueDragEntry)
            {
                dragRow = rowIndex;
                continue;
            }

            var top = list.Min.Y + row.Top - offset + RowShift(row, delta);
            if (top > list.Max.Y || top + row.Height < list.Min.Y)
            {
                continue;
            }

            DrawQueueRow(drawList, list, row, top, interactive && !queueDragActive, scale);
        }

        if (dragRow >= 0)
        {
            DrawDraggedQueueRow(drawList, list, queueRows[dragRow], scale);
        }

        drawList.PopClipRect();
    }

    private float RowShift(in QueueRow row, float delta)
    {
        var shift = 0f;
        if (row.ReorderIndex >= 0 && row.ReorderIndex < reorderShift.Length)
        {
            shift += reorderShift[row.ReorderIndex].Value;
        }

        if (row.EntryId != queueSettleEntry || row.ReorderIndex < 0)
        {
            return shift;
        }

        if (queueSettlePending)
        {
            queueSettlePending = false;
            queueSettle.SnapTo(queueSettleFrom - row.Top);
        }

        var settle = queueSettle.Step(0f, Motion.PageSettle, delta);
        if (MathF.Abs(settle) < 0.25f)
        {
            queueSettleEntry = -1;
        }

        return shift + settle;
    }

    private void EnsureQueueRows(float scale)
    {
        var queue = playback.Queue;
        var inJam = jam.InJam;
        var language = Loc.Current.Code;
        if (builtQueueVersion == queue.Version && builtJamVersion == jam.QueueVersion && builtInJam == inJam &&
            builtHistoryExpanded == historyExpanded && builtAutoplay == playback.AutoplayEnabled &&
            builtQueueScale == scale && ReferenceEquals(builtQueueLanguage, language))
        {
            return;
        }

        builtQueueVersion = queue.Version;
        builtJamVersion = jam.QueueVersion;
        builtInJam = inJam;
        builtHistoryExpanded = historyExpanded;
        builtAutoplay = playback.AutoplayEnabled;
        builtQueueScale = scale;
        builtQueueLanguage = language;
        queueRowCount = 0;
        reorderCount = 0;
        queueContentHeight = 0f;
        if (inJam)
        {
            BuildJamRows(scale);
        }
        else
        {
            BuildLocalRows(queue, scale);
        }

        RemapQueueDrag();
    }

    private void BuildLocalRows(PlayQueue queue, float scale)
    {
        if (queue.HistoryCount > 0)
        {
            AddHeader(QueueHeaderKind.History, false, scale);
            if (historyExpanded)
            {
                for (var index = 0; index < queue.HistoryCount; index++)
                {
                    var entry = queue.HistoryAt(index);
                    AddSong(QueueRowKind.History, entry.Id, entry.Song, entry.Song.Author, -1, scale);
                }
            }
        }

        if (queue.PlayingNextCount > 0)
        {
            AddHeader(QueueHeaderKind.PlayingNext, true, scale);
            for (var index = 0; index < queue.PlayingNextCount; index++)
            {
                var entry = queue.PlayingNextAt(index);
                AddSong(QueueRowKind.Queued, entry.Id, entry.Song, entry.Song.Author, index, scale);
            }
        }

        if (queue.UpcomingCount > 0)
        {
            AddHeader(QueueHeaderKind.Upcoming, queue.PlayingNextCount == 0, scale);
            for (var index = 0; index < queue.UpcomingCount; index++)
            {
                var entry = queue.UpcomingAt(index);
                AddSong(QueueRowKind.Queued, entry.Id, entry.Song, entry.Song.Author, queue.PlayingNextCount + index,
                    scale);
            }
        }

        var showAutoplay = playback.AutoplayEnabled && queue.AutoplayCount > 0;
        if (queue.QueuedCount == 0 && !showAutoplay)
        {
            AddRow(new QueueRow(QueueRowKind.Empty, QueueHeaderKind.None, 0, default, string.Empty, -1,
                queueContentHeight, QueueEmptyHeight * scale, false));
        }

        if (!showAutoplay)
        {
            return;
        }

        AddHeader(QueueHeaderKind.ContinuePlaying, false, scale);
        for (var index = 0; index < queue.AutoplayCount; index++)
        {
            var entry = queue.AutoplayAt(index);
            AddSong(QueueRowKind.Autoplay, entry.Id, entry.Song, entry.Song.Author, -1, scale);
        }
    }

    private void BuildJamRows(float scale)
    {
        var items = jam.Queue;
        AddHeader(QueueHeaderKind.Jam, false, scale);
        if (items.Length == 0)
        {
            AddRow(new QueueRow(QueueRowKind.Empty, QueueHeaderKind.None, 0, default, string.Empty, -1,
                queueContentHeight, QueueEmptyHeight * scale, false));
            return;
        }

        var addedBy = Loc.T(L.Music.NowPlaying.AddedBy);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var caption = item.AddedByName.Length > 0
                ? string.Format(Loc.Culture, addedBy, item.AddedByName)
                : item.Song.Author;
            AddSong(QueueRowKind.Jam, item.EntryId, item.Song, caption, index, scale);
        }
    }

    private void AddHeader(QueueHeaderKind header, bool showClear, float scale)
    {
        AddRow(new QueueRow(QueueRowKind.Header, header, 0, default, string.Empty, -1, queueContentHeight,
            QueueHeaderHeight * scale, showClear));
    }

    private void AddSong(QueueRowKind kind, int entryId, in Song song, string caption, int reorderIndex, float scale)
    {
        var height = QueueRowHeight * scale;
        if (reorderIndex >= 0)
        {
            EnsureReorderCapacity(reorderIndex + 1);
            reorderCenters[reorderIndex] = queueContentHeight + height * 0.5f;
            reorderCount = Math.Max(reorderCount, reorderIndex + 1);
        }

        AddRow(new QueueRow(kind, QueueHeaderKind.None, entryId, song, caption, reorderIndex, queueContentHeight,
            height, false));
    }

    private void AddRow(in QueueRow row)
    {
        if (queueRowCount == queueRows.Length)
        {
            Array.Resize(ref queueRows, queueRows.Length * 2);
        }

        queueRows[queueRowCount++] = row;
        queueContentHeight += row.Height;
    }

    private void EnsureReorderCapacity(int needed)
    {
        if (needed <= reorderCenters.Length)
        {
            return;
        }

        var size = Math.Max(needed, reorderCenters.Length * 2);
        Array.Resize(ref reorderCenters, size);
        Array.Resize(ref reorderShift, size);
    }

    private void RemapQueueDrag()
    {
        if (!queueDragActive)
        {
            return;
        }

        for (var rowIndex = 0; rowIndex < queueRowCount; rowIndex++)
        {
            if (queueRows[rowIndex].ReorderIndex >= 0 && queueRows[rowIndex].EntryId == queueDragEntry)
            {
                queueDragSource = queueRows[rowIndex].ReorderIndex;
                return;
            }
        }

        CancelQueueDrag();
    }

    private bool OverQueueHandle(Rect list)
    {
        var handleLeft = list.Max.X - NowPlayingSide * 0.5f * UiScale.Current - QueueHandleWidth * UiScale.Current;
        return reorderCount > 0 && UiInteract.Hover(new Vector2(handleLeft, list.Min.Y), list.Max);
    }

    private void UpdateQueueDrag(Rect list, bool interactive, float scale, float delta)
    {
        if (!queueSettlePending && queueSettleEntry < 0)
        {
            queueSettle.SnapTo(0f);
        }

        if (!queueDragActive)
        {
            StepReorderShifts(-1, -1, scale, delta);
            return;
        }

        if (!interactive || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            EndQueueDrag();
            return;
        }

        UiInteract.BlockThisFrame();
        var mouseY = ImGui.GetMousePos().Y;
        var speed = QueueReorder.AutoscrollSpeed(mouseY, list.Min.Y, list.Max.Y, QueueAutoscrollEdge * scale,
            QueueAutoscrollSpeed * scale);
        if (speed != 0f)
        {
            queueScroll.Sync(queueScroll.Offset + speed * delta);
        }

        var rowHeight = QueueRowHeight * scale;
        queueDragTopContent = mouseY - queueDragGrab - list.Min.Y + queueScroll.Offset;
        queueDragTarget = QueueReorder.TargetIndex(reorderCenters.AsSpan(0, reorderCount), queueDragSource,
            queueDragTopContent + rowHeight * 0.5f);
        StepReorderShifts(queueDragSource, queueDragTarget, scale, delta);
    }

    private void StepReorderShifts(int source, int target, float scale, float delta)
    {
        var rowHeight = QueueRowHeight * scale;
        for (var index = 0; index < reorderCount; index++)
        {
            var wanted = source < 0 ? 0f : QueueReorder.Shift(index, source, target) * rowHeight;
            reorderShift[index].Step(wanted, Motion.PageSettle, delta);
        }
    }

    private void BeginQueueDrag(in QueueRow row, float rowTop)
    {
        queueDragActive = true;
        queueDragEntry = row.EntryId;
        queueDragSource = row.ReorderIndex;
        queueDragTarget = row.ReorderIndex;
        queueDragGrab = ImGui.GetMousePos().Y - rowTop;
        UiInteract.CancelPendingTap();
    }

    private void EndQueueDrag()
    {
        queueDragActive = false;
        queueSettleEntry = queueDragEntry;
        queueSettleFrom = queueDragTopContent;
        queueSettlePending = true;
        for (var index = 0; index < reorderShift.Length; index++)
        {
            reorderShift[index].SnapTo(0f);
        }

        if (queueDragTarget != queueDragSource)
        {
            playback.MoveQueued(queueDragEntry, queueDragTarget);
        }
    }

    private void DrawQueueBar(ImDrawListPtr drawList, Rect bar, bool interactive, float scale)
    {
        var right = bar.Max.X;
        if (!jam.InJam)
        {
            right = DrawQueueToggles(drawList, bar, interactive, scale);
        }

        var caption = jam.InJam ? Loc.T(L.Music.Jam.Title) : Loc.T(L.Music.PlayingFrom);
        var title = jam.InJam ? jam.Title : playback.Queue.ContextTitle;
        if (title.Length == 0)
        {
            title = jam.InJam ? Loc.T(L.Music.Jam.Title) : Loc.T(L.Music.NowPlaying.UpNext);
        }

        var width = MathF.Max(1f, right - Metrics.Space.Md * scale - bar.Min.X);
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var top = bar.Center.Y - (captionHeight + titleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(bar.Min.X, top), Typography.FitText(caption, width, TextStyles.Footnote),
            NowPlayingMuted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(bar.Min.X, top + captionHeight),
            Typography.FitText(title, width, TextStyles.Headline), NowPlayingInk, TextStyles.Headline);
    }

    private float DrawQueueToggles(ImDrawListPtr drawList, Rect bar, bool interactive, float scale)
    {
        var radius = QueueToggleRadius * scale;
        var spacing = QueueToggleSpacing * scale;
        var autoplayCenter = new Vector2(bar.Max.X - radius, bar.Center.Y);
        var repeatCenter = new Vector2(autoplayCenter.X - spacing, bar.Center.Y);
        var shuffleCenter = new Vector2(repeatCenter.X - spacing, bar.Center.Y);
        var glyph = QueueToggleGlyph * scale;
        if (ToggleCircle(drawList, shuffleCenter, radius, playback.ShuffleEnabled, interactive,
                Loc.T(L.Music.Shuffle), out var shuffleInk))
        {
            playback.ToggleShuffle();
        }

        MediaGlyph.Shuffle(drawList, shuffleCenter, glyph, ImGui.GetColorU32(shuffleInk));
        var repeat = playback.RepeatMode;
        if (ToggleCircle(drawList, repeatCenter, radius, repeat != SongRepeatMode.Off, interactive,
                Loc.T(L.Music.Repeat), out var repeatInk))
        {
            playback.ToggleRepeat();
        }

        MediaGlyph.Repeat(drawList, repeatCenter, glyph, ImGui.GetColorU32(repeatInk));
        if (repeat == SongRepeatMode.One)
        {
            DrawRepeatOneBadge(drawList, repeatCenter, radius, scale);
        }

        if (ToggleCircle(drawList, autoplayCenter, radius, playback.AutoplayEnabled, interactive,
                Loc.T(L.Music.NowPlaying.Autoplay), out var autoplayInk))
        {
            playback.SetAutoplay(!playback.AutoplayEnabled);
        }

        AppSkin.Icon(drawList, autoplayCenter, IconGlyph.Of(FontAwesomeIcon.Infinity), autoplayInk, RowButtonGlyph);
        return shuffleCenter.X - radius;
    }

    private static void DrawRepeatOneBadge(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var badgeRadius = RepeatBadgeRadius * scale;
        var badgeCenter = center + new Vector2(radius * 0.72f, -radius * 0.72f);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(ActiveGlyphInk));
        drawList.AddCircle(badgeCenter, badgeRadius, ImGui.GetColorU32(NowPlayingInk), 0, scale);
        Typography.DrawCentered(drawList, badgeCenter, "1", NowPlayingInk, TextStyles.Caption1);
    }

    private static bool ToggleCircle(ImDrawListPtr drawList, Vector2 center, float radius, bool active,
        bool interactive, string tooltip, out Vector4 ink)
    {
        var hit = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - hit, center + hit);
        if (active)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(NowPlayingInk with { W = 0.92f }));
        }
        else
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? NowPlayingRail : NowPlayingWash));
        }

        ink = active ? ActiveGlyphInk : NowPlayingInk;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        }

        return interactive && UiInteract.Click(center - hit, center + hit, hovered);
    }

    private void DrawQueueRow(ImDrawListPtr drawList, Rect list, in QueueRow row, float top, bool interactive,
        float scale)
    {
        switch (row.Kind)
        {
            case QueueRowKind.Header:
                DrawQueueHeader(drawList, list, row, top, interactive, scale);
                return;
            case QueueRowKind.Empty:
                var center = new Vector2(list.Center.X, top + row.Height * 0.5f);
                Typography.DrawWrappedCentered(drawList, center, Loc.T(L.Music.NowPlaying.QueueEmpty),
                    NowPlayingMuted, TextStyles.Subheadline, list.Width - NowPlayingSide * 2f * scale);
                return;
            default:
                DrawQueueSong(drawList, list, row, top, interactive, scale);
                return;
        }
    }

    private void DrawQueueHeader(ImDrawListPtr drawList, Rect list, in QueueRow row, float top, bool interactive,
        float scale)
    {
        var left = list.Min.X + NowPlayingSide * scale;
        var right = list.Max.X - NowPlayingSide * scale;
        var centerY = top + row.Height * 0.5f;
        var label = HeaderLabel(row.Header);
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        var clearRight = right;
        if (row.ShowClear)
        {
            var clear = Loc.T(L.Music.NowPlaying.Clear);
            var size = Typography.Measure(clear, TextStyles.Subheadline);
            var clearMin = new Vector2(right - size.X, centerY - size.Y * 0.5f);
            var clearMax = new Vector2(right, centerY + size.Y * 0.5f);
            var hovered = interactive && UiInteract.Hover(clearMin, clearMax);
            Typography.Draw(drawList, clearMin, clear, hovered ? NowPlayingInk : NowPlayingMuted,
                TextStyles.Subheadline);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (interactive && UiInteract.Click(clearMin, clearMax, hovered))
            {
                playback.ClearQueued();
            }

            clearRight = clearMin.X - Metrics.Space.Md * scale;
        }

        var labelWidth = MathF.Max(1f, clearRight - left);
        Typography.Draw(drawList, new Vector2(left, centerY - labelHeight * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.Headline), NowPlayingInk, TextStyles.Headline);
        if (row.Header != QueueHeaderKind.History)
        {
            return;
        }

        var chevronCenter = new Vector2(MathF.Min(left + Typography.Measure(label, TextStyles.Headline).X,
            clearRight) + Metrics.Space.Md * scale, centerY);
        AppSkin.Icon(drawList, chevronCenter,
            IconGlyph.Of(historyExpanded ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight),
            NowPlayingMuted, VolumeIconScale);
        var rowMin = new Vector2(left, top);
        var rowMax = new Vector2(right, top + row.Height);
        if (interactive && UiInteract.HoverClick(rowMin, rowMax))
        {
            historyExpanded = !historyExpanded;
        }
    }

    private static string HeaderLabel(QueueHeaderKind header)
    {
        return header switch
        {
            QueueHeaderKind.History => Loc.T(L.Music.NowPlaying.History),
            QueueHeaderKind.PlayingNext => Loc.T(L.Music.NowPlaying.PlayingNext),
            QueueHeaderKind.Upcoming => Loc.T(L.Music.NowPlaying.UpNext),
            QueueHeaderKind.ContinuePlaying => Loc.T(L.Music.NowPlaying.ContinuePlaying),
            QueueHeaderKind.Jam => Loc.T(L.Music.NowPlaying.JamQueue),
            _ => string.Empty,
        };
    }

    private void DrawQueueSong(ImDrawListPtr drawList, Rect list, in QueueRow row, float top, bool interactive,
        float scale)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var halfInset = NowPlayingSide * 0.5f * scale;
        var rowMin = new Vector2(list.Min.X + halfInset, top);
        var rowMax = new Vector2(list.Max.X - halfInset, top + row.Height);
        var hovered = interactive && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            Squircle.Fill(drawList, rowMin, rowMax, QueueRowRounding * scale,
                ImGui.GetColorU32(NowPlayingInk with { W = QueueHoverAlpha }));
        }

        var overChild = DrawQueueSongBody(drawList, row, rowMin, rowMax, interactive, scale);
        if (row.Kind == QueueRowKind.History)
        {
            LayerCompositor.Fade(drawList, firstVertex, QueueHistoryAlpha);
        }

        if (hovered && !overChild)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!overChild && UiInteract.Click(rowMin, rowMax, hovered))
        {
            PlayQueueRow(row);
        }
    }

    private bool DrawQueueSongBody(ImDrawListPtr drawList, in QueueRow row, Vector2 rowMin, Vector2 rowMax,
        bool interactive, float scale)
    {
        var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
        var left = rowMin.X + NowPlayingSide * 0.5f * scale;
        var artSide = QueueArtUnits * scale;
        var artMin = new Vector2(left, centerY - artSide * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, artSide, row.Song.ThumbnailUrl, row.Song.Title);
        var right = rowMax.X - Metrics.Space.Sm * scale;
        var overChild = false;
        if (row.ReorderIndex >= 0)
        {
            var handleMin = new Vector2(right - QueueHandleWidth * scale, rowMin.Y);
            var handleCenter = new Vector2(right - QueueHandleWidth * 0.5f * scale, centerY);
            var overHandle = interactive && UiInteract.Hover(handleMin, rowMax);
            overChild |= overHandle;
            AppSkin.Icon(drawList, handleCenter, IconGlyph.Of(FontAwesomeIcon.Bars),
                overHandle ? NowPlayingInk : NowPlayingFaint, VolumeIconScale);
            if (overHandle)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    BeginQueueDrag(row, rowMin.Y);
                }
            }

            right = handleMin.X;
        }

        var menuRadius = QueueMenuRadius * scale;
        var menuCenter = new Vector2(right - menuRadius, centerY);
        var menuHit = new Vector2(menuRadius, menuRadius);
        overChild |= interactive && UiInteract.Hover(menuCenter - menuHit, menuCenter + menuHit);
        if (RoundGlyphButton(drawList, unchecked((uint)row.EntryId * QueuePressSalt), menuCenter, menuRadius,
                IconGlyph.Of(FontAwesomeIcon.EllipsisH), NowPlayingMuted, interactive, Loc.T(L.Music.MoreOptions)))
        {
            OpenQueueMenu(row);
        }

        var textLeft = artMin.X + artSide + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, menuCenter.X - menuRadius - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + captionHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(row.Song.Title, textWidth,
            TextStyles.Body), NowPlayingInk, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(row.Caption, textWidth, TextStyles.Footnote), NowPlayingMuted, TextStyles.Footnote);
        return overChild;
    }

    private void DrawDraggedQueueRow(ImDrawListPtr drawList, Rect list, in QueueRow row, float scale)
    {
        var top = list.Min.Y + queueDragTopContent - queueScroll.Offset;
        var halfInset = NowPlayingSide * 0.5f * scale;
        var center = new Vector2(list.Center.X, top + row.Height * 0.5f);
        var half = new Vector2((list.Width * 0.5f - halfInset) * QueueDragLift, row.Height * 0.5f * QueueDragLift);
        var rounding = QueueRowRounding * scale;
        Elevation.Card(drawList, center - half, center + half, rounding, scale);
        Squircle.Fill(drawList, center - half, center + half, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, QueueDragFillAlpha)));
        Squircle.Fill(drawList, center - half, center + half, rounding,
            ImGui.GetColorU32(NowPlayingInk with { W = QueueHoverAlpha * 2f }));
        DrawQueueSongBody(drawList, row, center - half, center + half, false, scale);
    }

    private void PlayQueueRow(in QueueRow row)
    {
        switch (row.Kind)
        {
            case QueueRowKind.History:
                playback.PlayNext(row.Song);
                playback.Next();
                return;
            case QueueRowKind.Queued:
            case QueueRowKind.Autoplay:
            case QueueRowKind.Jam:
                playback.JumpTo(row.EntryId);
                return;
        }
    }

    private void OpenQueueMenu(in QueueRow row)
    {
        queueMenuRow = row;
        queueMenu.Open();
    }

    private void DrawQueueMenu(Rect screen)
    {
        if (!queueMenu.CapturesPointer)
        {
            return;
        }

        var count = 0;
        queueMenuItems[count++] = new ActionSheet.Item(Loc.T(L.Music.PlayNext), IconGlyph.Of(FontAwesomeIcon.Reply));
        if (queueMenuRow.Kind != QueueRowKind.History)
        {
            queueMenuItems[count++] = new ActionSheet.Item(Loc.T(L.Music.NowPlaying.RemoveFromQueue),
                IconGlyph.Of(FontAwesomeIcon.Trash), true);
        }

        var picked = queueMenu.Draw(screen, ActionSheetStyle.From(ui), queueMenuItems.AsSpan(0, count),
            Loc.T(L.Common.Cancel), false, queueMenuRow.Song.Title);
        if (picked == 0)
        {
            PlayQueueRowNext(queueMenuRow);
        }
        else if (picked == 1)
        {
            playback.RemoveQueued(queueMenuRow.EntryId);
        }
    }

    private void PlayQueueRowNext(in QueueRow row)
    {
        switch (row.Kind)
        {
            case QueueRowKind.Queued:
            case QueueRowKind.Jam:
                playback.MoveQueued(row.EntryId, 0);
                return;
            case QueueRowKind.Autoplay:
                playback.PlayNext(row.Song);
                playback.RemoveQueued(row.EntryId);
                return;
            case QueueRowKind.History:
                playback.PlayNext(row.Song);
                return;
        }
    }
}
