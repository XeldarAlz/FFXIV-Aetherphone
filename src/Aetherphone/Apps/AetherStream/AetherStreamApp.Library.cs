using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.AetherStream;

internal enum LibrarySegment : byte
{
    UpNext,
    History,
    Playlists,
}

internal sealed partial class AetherStreamApp
{
    private const float SegmentHeight = 36f;
    private const float SuggestionRowHeight = 56f;
    private const float QueueDragThreshold = 7f;
    private const float GripReserve = 34f;
    private const float PlaylistHeroHeight = 96f;

    private const int RowActionPlay = 0;
    private const int RowActionPlayNext = 1;
    private const int RowActionAddToQueue = 2;
    private const int RowActionRemove = 3;
    private const int RowActionSuggest = 4;
    private const int QueueActionShuffle = 10;
    private const int QueueActionSave = 11;
    private const int QueueActionClear = 12;

    private readonly string[] librarySegmentLabels = new string[3];
    private LibrarySegment librarySegment;
    private int queueDragIndex = -1;
    private Vector2 queueDragStart;
    private float queueDragY;
    private bool queueDragActive;
    private int upNextDrawnFrame = -1;
    private TextCache queueCountText;
    private TextCache playlistCountText;

    private void DrawLibraryTab(Rect body, float scale)
    {
        librarySegmentLabels[0] = Loc.T(L.AetherStream.UpNext);
        librarySegmentLabels[1] = Loc.T(L.AetherStream.History);
        librarySegmentLabels[2] = Loc.T(L.AetherStream.Playlists);
        var strip = new Rect(new Vector2(body.Min.X + PadX * scale, body.Min.Y + Metrics.Space.Xs * scale),
            new Vector2(body.Max.X - PadX * scale, body.Min.Y + (Metrics.Space.Xs + SegmentHeight) * scale));
        librarySegment = (LibrarySegment)SegmentStrip.Draw("aetherstream.library", strip, librarySegmentLabels,
            (int)librarySegment, ui.Palette);

        var list = new Rect(new Vector2(body.Min.X, strip.Max.Y + Metrics.Space.Sm * scale), body.Max);
        using (ImRaii.PushId((int)librarySegment))
        using (var surface = AppSurface.BeginEdgeToEdge(list))
        {
            switch (librarySegment)
            {
                case LibrarySegment.History:
                    DrawHistoryList(list, scale);
                    break;
                case LibrarySegment.Playlists:
                    DrawPlaylistList(list, scale);
                    break;
                default:
                    DrawUpNextList(list, scale);
                    break;
            }

            if (queueDragIndex >= 0)
            {
                surface.CancelDrag();
            }

            Gap(FabClearance);
        }

        if (ComposeFab.Draw(TabBar.ContentArea(body, scale), "##aetherstreamLibraryFab", ui.Accent, PhoneIcons.Plus,
                Loc.T(L.AetherStream.AddVideoTitle), phoneGlyph: true))
        {
            OpenAddSheet();
        }
    }

    private void DrawEmptyList(Rect list, FontAwesomeIcon icon, string title, string hint, float scale)
    {
        var block = BeginBlock(MathF.Max(200f * scale, list.Height * 0.62f));
        EmptyState.Draw(block, ui, icon, title, hint);
        EndBlock();
    }

    private void DrawUpNextList(Rect list, float scale)
    {
        if (watchAlong.IsViewing)
        {
            DrawHostQueue(list, scale);
            return;
        }

        if (watchAlong.IsHosting)
        {
            suggestionNotifier.StampAttention();
            var frame = ImGui.GetFrameCount();
            if (frame - upNextDrawnFrame > 1)
            {
                suggestionNotifier.ClearShellNotices();
            }

            upNextDrawnFrame = frame;
        }

        var drawList = ImGui.GetWindowDrawList();
        var suggestions = watchAlong.PendingQueueSuggestions;
        if (watchAlong.IsHosting && suggestions.Count > 0)
        {
            SectionLabel(Loc.T(L.AetherStream.QueueSuggestionsHeader));
            for (var index = 0; index < suggestions.Count; index++)
            {
                DrawSuggestionRow(drawList, suggestions[index], scale);
            }

            Gap(Metrics.Space.Sm);
        }

        if (queue.Current is { } current)
        {
            SectionLabel(Loc.T(L.AetherStream.NowPlayingHeader));
            var cell = BeginMediaRow(drawList, current.Url, current.ThumbnailUrl, current.Title,
                video.State == VideoPlaybackState.Loading ? Loc.T(L.AetherStream.LoadingVideo) : current.Subtitle,
                0f, false, true);
            EndMediaRow(drawList, cell);
            Gap(Metrics.Space.Sm);
        }

        var entries = queue.Entries;
        if (entries.Count == 0)
        {
            queueDragIndex = -1;
            queueDragActive = false;
            if (queue.Current is null && suggestions.Count == 0)
            {
                DrawEmptyList(list, FontAwesomeIcon.ListUl, Loc.T(L.AetherStream.UpNextEmpty),
                    Loc.T(L.AetherStream.UpNextEmptyHint), scale);
            }

            return;
        }

        DrawQueueHeader(entries.Count, scale);
        UpdateQueueDrag(entries.Count, MediaRowHeight * scale + ImGui.GetStyle().ItemSpacing.Y);
        for (var index = 0; index < entries.Count; index++)
        {
            DrawQueueRow(drawList, entries[index], index, scale);
        }
    }

    private void DrawQueueHeader(int count, float scale)
    {
        var row = BeginBlock(34f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var labelHeight = Typography.LineHeight(SectionStyle);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            queueCountText.Format(Loc.T(L.AetherStream.UpNextCount), count), Ink.FaintInk, SectionStyle);
        var moreRadius = 14f * scale;
        var moreCenter = new Vector2(row.Max.X - moreRadius, row.Center.Y);
        if (ui.IconButton(moreCenter, moreRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), Ink.TitleInk,
                Ink.ButtonFill, 0.62f, Loc.T(L.AetherStream.MoreOptions)))
        {
            actionEntry = null;
            actionHistory = null;
            BeginActions(SheetPurpose.QueueRow, string.Empty);
            AddAction(QueueActionShuffle, Loc.T(L.AetherStream.ShuffleQueue));
            AddAction(QueueActionSave, Loc.T(L.AetherStream.SaveQueueAsPlaylist));
            AddAction(QueueActionClear, Loc.T(L.AetherStream.ClearQueue), danger: true);
            actions.Open();
        }

        EndBlock();
    }

    private void UpdateQueueDrag(int count, float rowPitch)
    {
        if (!queueDragActive)
        {
            if (queueDragIndex >= 0 && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                queueDragIndex = -1;
            }

            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var targetIndex = Math.Clamp(queueDragIndex + (int)MathF.Round(queueDragY / rowPitch), 0, count - 1);
            if (targetIndex != queueDragIndex)
            {
                queue.Reorder(queueDragIndex, targetIndex);
            }

            queueDragActive = false;
            queueDragIndex = -1;
            return;
        }

        queueDragY = ImGui.GetMousePos().Y - queueDragStart.Y;
        UiInteract.CancelPendingTap();
    }

    private void DrawQueueRow(ImDrawListPtr drawList, VideoQueueEntry entry, int index, float scale)
    {
        var dragging = queueDragActive && index == queueDragIndex;
        if (dragging)
        {
            var origin = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + queueDragY));
            var floating = BeginMediaRow(drawList, entry.Url, entry.ThumbnailUrl, entry.Title, entry.Subtitle,
                GripReserve, false, true);
            DrawGrip(drawList, floating.Bounds, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(floating.Bounds.Width, floating.Bounds.Height));
            return;
        }

        var cell = BeginMediaRow(drawList, entry.Url, entry.ThumbnailUrl, entry.Title, entry.Subtitle, GripReserve,
            !queueDragActive);
        var gripCenter = DrawGrip(drawList, cell.Bounds, scale);
        var gripExtent = new Vector2(GripReserve * 0.5f * scale, cell.Bounds.Height * 0.5f);
        var overGrip = UiInteract.Hover(gripCenter - gripExtent, gripCenter + gripExtent);
        if (overGrip)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !queueDragActive)
            {
                queueDragIndex = index;
                queueDragStart = ImGui.GetMousePos();
                queueDragY = 0f;
            }
        }

        if (queueDragIndex == index && !queueDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left)
            && Vector2.Distance(ImGui.GetMousePos(), queueDragStart) > QueueDragThreshold * scale)
        {
            queueDragActive = true;
        }

        EndMediaRow(drawList, cell);
        if (cell.Tapped && !overGrip && !queueDragActive)
        {
            actionEntry = entry;
            actionHistory = null;
            BeginActions(SheetPurpose.QueueRow, entry.Title);
            AddAction(RowActionPlay, Loc.T(L.AetherStream.PlayNow));
            if (index > 0)
            {
                AddAction(RowActionPlayNext, Loc.T(L.AetherStream.PlayNext));
            }

            AddAction(RowActionRemove, Loc.T(L.AetherStream.Remove), danger: true);
            actions.Open();
        }
    }

    private static Vector2 DrawGrip(ImDrawListPtr drawList, Rect row, float scale)
    {
        var center = new Vector2(row.Max.X - (PadX + GripReserve * 0.5f - 6f) * scale, row.Center.Y);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.GripLines), Ink.FaintInk, 0.62f);
        return center;
    }

    private void HandleQueueRowAction(int code)
    {
        switch (code)
        {
            case QueueActionShuffle:
                queue.Shuffle();
                return;
            case QueueActionSave:
                SaveQueueAsPlaylist();
                return;
            case QueueActionClear:
                confirm.Ask(new ConfirmRequest
                {
                    Message = Loc.T(L.AetherStream.ClearQueueConfirm),
                    ConfirmLabel = Loc.T(L.AetherStream.ClearQueue),
                    CancelLabel = Loc.T(L.AetherStream.Keep),
                    Sheet = true,
                    Confirm = queue.ClearUpcoming,
                });
                return;
        }

        if (actionEntry is not { } entry)
        {
            return;
        }

        actionEntry = null;
        switch (code)
        {
            case RowActionPlay:
                CancelPendingJoin();
                queue.PlayNow(entry);
                activeTab = StreamTab.Watch;
                return;
            case RowActionPlayNext:
                queue.Insert(entry, QueueAddMode.PlayNext);
                return;
            case RowActionRemove:
                queue.Remove(entry);
                return;
        }
    }

    private void SaveQueueAsPlaylist()
    {
        var entries = queue.Entries;
        if (entries.Count == 0)
        {
            return;
        }

        var records = new List<VideoQueueRecord>(entries.Count + 1);
        if (queue.Current is { } current)
        {
            records.Add(current.ToRecord());
        }

        for (var index = 0; index < entries.Count; index++)
        {
            records.Add(entries[index].ToRecord());
        }

        var name = library.NextPlaylistName(Loc.T(L.AetherStream.PlaylistDefaultName), Loc.Culture);
        if (library.SavePlaylist(name, string.Empty, records) is not null)
        {
            ShellToast.Show(Loc.T(L.AetherStream.PlaylistSaved));
        }
    }

    private void DrawSuggestionRow(ImDrawListPtr drawList, QueueSuggestion suggestion, float scale)
    {
        var cell = FeedCell.Begin(drawList, SuggestionRowHeight * scale, ui.HoverWash, false);
        var row = cell.Bounds;
        var circleRadius = 15f * scale;
        var denyCenter = new Vector2(row.Max.X - PadX * scale - circleRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - circleRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);
        var textLeft = row.Min.X + PadX * scale;
        var textWidth = approveCenter.X - circleRadius - Metrics.Space.Md * scale - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var urlHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (nameHeight + urlHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(suggestion.DisplayName, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(suggestion.Url, textWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);

        if (ui.IconButton(approveCenter, circleRadius, IconGlyph.Of(FontAwesomeIcon.Check), Ink.PresenceGreen,
                Palette.WithAlpha(Ink.PresenceGreen, 0.18f), 0.62f, Loc.T(L.AetherStream.QueueSuggestionAdd)))
        {
            watchAlong.ApproveQueueSuggestion(suggestion.SuggestionId);
        }

        if (ui.IconButton(denyCenter, circleRadius, IconGlyph.Of(FontAwesomeIcon.Times), Ink.Danger,
                Palette.WithAlpha(Ink.Danger, 0.16f), 0.62f, Loc.T(L.AetherStream.QueueSuggestionDismiss)))
        {
            watchAlong.DenyQueueSuggestion(suggestion.SuggestionId);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private void DrawHostQueue(Rect list, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (watchAlong.ViewingEntry is { } playing)
        {
            SectionLabel(Loc.T(L.AetherStream.NowPlayingHeader));
            var cell = BeginMediaRow(drawList, playing.Url, playing.ThumbnailUrl, playing.Title, playing.Subtitle,
                0f, false, true);
            EndMediaRow(drawList, cell);
            Gap(Metrics.Space.Sm);
        }

        var items = watchAlong.HostQueue;
        if (items.Count == 0)
        {
            if (watchAlong.ViewingEntry is null)
            {
                DrawEmptyList(list, FontAwesomeIcon.ListUl, Loc.T(L.AetherStream.UpNextEmpty),
                    Loc.T(watchAlong.CanAddDirectly ? L.AetherStream.HostQueueAddHint : L.AetherStream.SuggestHint),
                    scale);
            }

            return;
        }

        SectionLabel(Loc.T(L.AetherStream.UpNextHostQueue));
        for (var index = 0; index < items.Count; index++)
        {
            var cell = BeginMediaRow(drawList, items[index].Url, null, items[index].Title, string.Empty, 0f, false);
            EndMediaRow(drawList, cell);
        }
    }

    private void DrawHistoryList(Rect list, float scale)
    {
        var history = library.History;
        if (history.Count == 0)
        {
            DrawEmptyList(list, FontAwesomeIcon.History, Loc.T(L.AetherStream.HistoryEmpty),
                Loc.T(L.AetherStream.HistoryEmptyHint), scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        VideoHistoryRecord? tapped = null;
        for (var index = 0; index < history.Count; index++)
        {
            var record = history[index];
            var cell = BeginMediaRow(drawList, record.Url, record.ThumbnailUrl, record.Title,
                VideoLibrary.Subtitle(record), 0f, true);
            EndMediaRow(drawList, cell);
            if (cell.Tapped)
            {
                tapped = record;
            }
        }

        Gap(Metrics.Space.Md);
        var clear = BeginBlock(Typography.LineHeight(TextStyles.SubheadlineEmphasized) + 16f * scale);
        if (TextLink(clear.Center, Loc.T(L.AetherStream.HistoryClear), Ink.Danger))
        {
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.AetherStream.HistoryClearConfirm),
                ConfirmLabel = Loc.T(L.AetherStream.HistoryClear),
                CancelLabel = Loc.T(L.Common.Cancel),
                Sheet = true,
                Confirm = library.ClearHistory,
            });
        }

        EndBlock();
        if (tapped is not null)
        {
            OpenHistoryActions(tapped);
        }
    }

    private void OpenHistoryActions(VideoHistoryRecord record)
    {
        actionHistory = record;
        actionEntry = null;
        BeginActions(SheetPurpose.HistoryRow, record.Title);
        if (watchAlong.IsViewing)
        {
            if (!MediaInput.LooksLikeLocalPath(record.Url))
            {
                AddAction(RowActionSuggest, Loc.T(watchAlong.CanAddDirectly
                    ? L.AetherStream.AddToQueue
                    : L.AetherStream.SuggestAction));
            }
        }
        else
        {
            AddAction(RowActionPlay, Loc.T(record.PositionSeconds > 0d
                ? L.AetherStream.ResumeAction
                : L.AetherStream.PlayNow));
            AddAction(RowActionPlayNext, Loc.T(L.AetherStream.PlayNext));
            AddAction(RowActionAddToQueue, Loc.T(L.AetherStream.AddToQueue));
        }

        AddAction(RowActionRemove, Loc.T(L.AetherStream.HistoryRemove), danger: true);
        actions.Open();
    }

    private void HandleHistoryRowAction(int code)
    {
        if (actionHistory is not { } record)
        {
            return;
        }

        actionHistory = null;
        switch (code)
        {
            case RowActionPlay:
                PlayHistory(record, QueueAddMode.PlayNow);
                return;
            case RowActionPlayNext:
                PlayHistory(record, QueueAddMode.PlayNext);
                return;
            case RowActionAddToQueue:
                PlayHistory(record, QueueAddMode.AddToQueue);
                return;
            case RowActionSuggest:
                SubmitInput(record.Url, QueueAddMode.AddToQueue);
                return;
            case RowActionRemove:
                library.RemoveHistory(record);
                return;
        }
    }

    private void DrawPlaylistList(Rect list, float scale)
    {
        var playlists = library.Playlists;
        if (playlists.Count == 0)
        {
            DrawEmptyList(list, FontAwesomeIcon.Film, Loc.T(L.AetherStream.PlaylistsEmpty),
                Loc.T(L.AetherStream.PlaylistsEmptyHint), scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < playlists.Count; index++)
        {
            var playlist = playlists[index];
            var cover = playlist.Entries.Count > 0 ? playlist.Entries[0] : null;
            var cell = BeginMediaRow(drawList, cover?.Url, cover?.ThumbnailUrl, playlist.Name,
                PlaylistCount(playlist), 22f, true);
            PhoneIcon.Draw(drawList, new Vector2(cell.Bounds.Max.X - PadX * scale - 6f * scale, cell.Bounds.Center.Y),
                PhoneIcons.ChevronRight, Ink.FaintInk, 16f * scale);
            EndMediaRow(drawList, cell);
            if (cell.Tapped)
            {
                router.Push(new StreamRoute(StreamScreen.Playlist, playlist.Id));
            }
        }
    }

    private static string PlaylistCount(VideoPlaylistRecord playlist)
    {
        var format = Loc.T(L.AetherStream.PlaylistVideoCount);
        if (playlist.CountLabel is null || !ReferenceEquals(playlist.CountFormat, format))
        {
            playlist.CountFormat = format;
            playlist.CountLabel = string.Format(Loc.Culture, format, playlist.Entries.Count);
        }

        return playlist.CountLabel;
    }

    private VideoPlaylistRecord? FindPlaylist(string id)
    {
        var playlists = library.Playlists;
        for (var index = 0; index < playlists.Count; index++)
        {
            if (string.Equals(playlists[index].Id, id, StringComparison.Ordinal))
            {
                return playlists[index];
            }
        }

        return null;
    }

    private void DrawPlaylist(Rect area, string id, float scale)
    {
        var playlist = FindPlaylist(id);
        SocialChrome.DrawScreenHeader(area, playlist?.Name ?? Loc.T(L.AetherStream.Playlists), Ink, back,
            ScreenTitleStyle, SocialChrome.HeaderReserve(1));
        if (playlist is null)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var trashCenter = SocialChrome.HeaderSlot(area, 0);
        if (SocialChrome.DrawHeaderIcon(drawList, trashCenter, SocialChrome.HeaderIconRadius * scale, PhoneIcons.Trash,
                20f, Loc.T(L.AetherStream.PlaylistDelete), Ink, Ink.TitleInk))
        {
            confirm.Ask(new ConfirmRequest
            {
                Message = playlistCountText.Format(Loc.T(L.AetherStream.PlaylistDeleteConfirm), playlist.Name),
                ConfirmLabel = Loc.T(L.Common.Delete),
                CancelLabel = Loc.T(L.Common.Cancel),
                Sheet = true,
                Confirm = () =>
                {
                    library.RemovePlaylist(playlist);
                    router.Pop(false);
                },
            });
        }

        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(content))
        {
            Gap(Metrics.Space.Xs);
            DrawPlaylistHero(playlist, scale);
            Gap(Metrics.Space.Md);
            var rowDrawList = ImGui.GetWindowDrawList();
            var viewing = watchAlong.IsViewing;
            VideoQueueRecord? tapped = null;
            for (var index = 0; index < playlist.Entries.Count; index++)
            {
                var record = playlist.Entries[index];
                var cell = BeginMediaRow(rowDrawList, record.Url, record.ThumbnailUrl, record.Title, record.Source,
                    0f, true);
                EndMediaRow(rowDrawList, cell);
                if (cell.Tapped)
                {
                    tapped = record;
                }
            }

            Gap(Metrics.Space.Lg);
            if (tapped is not null)
            {
                if (viewing)
                {
                    SubmitInput(tapped.Url, QueueAddMode.AddToQueue);
                }
                else
                {
                    CancelPendingJoin();
                    queue.PlayNow(AetherStreamQueue.FromRecord(tapped));
                    activeTab = StreamTab.Watch;
                    router.Pop();
                }
            }
        }
    }

    private void DrawPlaylistHero(VideoPlaylistRecord playlist, float scale)
    {
        var card = BeginBlock(PlaylistHeroHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var cover = playlist.Entries.Count > 0 ? playlist.Entries[0] : null;
        var thumbHeight = card.Height;
        var thumb = new Rect(card.Min, card.Min + new Vector2(thumbHeight * ThumbAspect, thumbHeight));
        DrawThumb(drawList, thumb, cover?.Url, cover?.ThumbnailUrl, Metrics.Radius.Md * scale);

        var left = thumb.Max.X + Metrics.Space.Md * scale;
        var width = card.Max.X - left;
        Typography.Draw(drawList, new Vector2(left, card.Min.Y),
            Typography.FitText(PlaylistCount(playlist), width, TextStyles.Subheadline), Ink.MutedInk,
            TextStyles.Subheadline);
        if (watchAlong.IsViewing)
        {
            EndBlock();
            return;
        }

        var buttonHeight = SmallButtonHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var play = new Rect(new Vector2(left, card.Max.Y - buttonHeight * 2f - gap),
            new Vector2(card.Max.X, card.Max.Y - buttonHeight - gap));
        var add = new Rect(new Vector2(left, card.Max.Y - buttonHeight), card.Max);
        if (SmallButton(play, Loc.T(L.AetherStream.PlayAction), true))
        {
            CancelPendingJoin();
            queue.InsertRecords(playlist.Entries, QueueAddMode.PlayNow);
            activeTab = StreamTab.Watch;
            router.Pop();
        }

        if (SmallButton(add, Loc.T(L.AetherStream.AddToQueue), false))
        {
            queue.InsertRecords(playlist.Entries, QueueAddMode.AddToQueue);
            ShellToast.Show(Loc.T(L.AetherStream.AddedToQueueToast));
        }

        EndBlock();
    }
}
