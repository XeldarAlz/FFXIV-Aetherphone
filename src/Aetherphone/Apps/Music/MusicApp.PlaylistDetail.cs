using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float DetailCoverFraction = 0.66f;
    private const float DetailCoverMax = 250f;
    private const float DescriptionFieldHeight = 76f;
    private const float EditButtonRadius = 13f;
    private const float EditButtonScale = 0.95f;
    private const float RemoveBadgeFraction = 0.78f;
    private const float RemovePressedAlpha = 0.7f;
    private const float GripWidth = 44f;
    private const float GripGlyphScale = 0.8f;
    private const float FloatingShadowAlpha = 0.28f;
    private const float FloatingShadowDrop = 4f;
    private const float FloatingLift = 0.06f;
    private const float AutoScrollZone = 72f;
    private const float AutoScrollSpeed = 720f;
    private const float RefreshGlyphScale = 0.7f;
    private const float RefreshHitRadius = 13f;
    private const float LinkGlyphScale = 0.6f;

    private static readonly Vector4 RemoveInk = new(1f, 1f, 1f, 1f);

    private readonly NavBarButton[] detailButtons = new NavBarButton[2];
    private Song[] playlistSongs = Array.Empty<Song>();
    private string playlistSongsId = string.Empty;
    private int playlistSongsVersion = -1;
    private string playlistMetaLanguage = string.Empty;
    private CoverArt playlistArt = CoverArt.None;
    private string playlistMeta = string.Empty;
    private bool playlistEditing;
    private string editPlaylistId = string.Empty;
    private string editName = string.Empty;
    private string editDescription = string.Empty;
    private bool focusEditName;
    private bool focusEditDescription;
    private int playlistEditFrame;
    private Spring[] rowSprings = Array.Empty<Spring>();
    private int dragIndex = -1;
    private int dragTarget = -1;
    private float dragGrab;
    private float dragPointer;
    private ImagePickCrop? coverPicker;
    private bool coverPicking;
    private string coverPlaylistId = string.Empty;
    private volatile bool coverSaving;
    private volatile bool coverFailed;
    private string? coverSavedPath;
    private PlaylistImporter? refreshImporter;
    private string refreshPlaylistId = string.Empty;

    private bool Refreshing(string playlistId) =>
        refreshImporter is { Busy: true } && string.Equals(refreshPlaylistId, playlistId, StringComparison.Ordinal);

    private void DrawPlaylistDetail(in PhoneContext context, in MusicRoute route)
    {
        if (coverPicking && string.Equals(coverPlaylistId, route.Key, StringComparison.Ordinal))
        {
            playlistEditFrame = ImGui.GetFrameCount();
            DrawCoverPicker(context);
            return;
        }

        if (library.FindPlaylist(route.Key) is not { } playlist)
        {
            var missing = BeginPage(context);
            EmptyState.Draw(Unobstructed(missing.Body), ui, FontAwesomeIcon.ListUl, Loc.T(L.Music.NoPlaylistsYet),
                Loc.T(L.Music.PlaylistEmptySub));
            EndPage(in missing, context, Loc.T(L.Music.LibraryPlaylists));
            return;
        }

        EnsurePlaylistDetail(playlist);
        var editing = playlistEditing && string.Equals(editPlaylistId, playlist.Id, StringComparison.Ordinal);
        if (editing)
        {
            playlistEditFrame = ImGui.GetFrameCount();
        }

        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawPlaylistHeader(playlist, editing, scale);
            if (editing)
            {
                if (LibraryKit.ActionRow(ui, IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Music.Library.AddSongs)))
                {
                    AddSongsPicker.Open(playlist.Id);
                }

                DrawEditableRows(playlist.Id, scale);
                if (dragIndex >= 0)
                {
                    surface.CancelDrag();
                }
            }
            else if (playlistSongs.Length == 0)
            {
                if (LibraryKit.ActionRow(ui, IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Music.Library.AddSongs)))
                {
                    AddSongsPicker.Open(playlist.Id);
                }
            }
            else
            {
                DrawSongRows(playlistSongs, playlist.Id, playlist.Name);
            }

            LibraryKit.Gap(Metrics.Space.Lg);
            if (playlistSongs.Length > 0)
            {
                LibraryKit.CenteredText(playlistMeta, TextStyles.Footnote, ui.MutedInk,
                    ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale);
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        var buttons = BuildDetailButtons(editing);
        var pressed = EndPage(in frame, context, playlist.Name, buttons);
        HandleDetailButton(playlist.Id, editing, pressed);
    }

    private ReadOnlySpan<NavBarButton> BuildDetailButtons(bool editing)
    {
        if (editing)
        {
            detailButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Check), Loc.T(L.Music.Library.Done));
            return detailButtons.AsSpan(0, 1);
        }

        detailButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.EllipsisH), Loc.T(L.Music.MoreOptions));
        detailButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Pen), Loc.T(L.Music.Library.Edit));
        return detailButtons.AsSpan(0, 2);
    }

    private void HandleDetailButton(string playlistId, bool editing, int pressed)
    {
        if (pressed < 0)
        {
            return;
        }

        if (editing)
        {
            EndPlaylistEdit();
            return;
        }

        if (pressed == 0)
        {
            OpenPlaylistMenu(playlistId);
            return;
        }

        BeginPlaylistEdit(playlistId, false, false);
    }

    private void EnsurePlaylistDetail(PlaylistRecord playlist)
    {
        var language = Loc.Current.Code;
        if (playlistSongsVersion == library.Version &&
            string.Equals(playlistSongsId, playlist.Id, StringComparison.Ordinal) &&
            string.Equals(playlistMetaLanguage, language, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(playlistSongsId, playlist.Id, StringComparison.Ordinal))
        {
            dragIndex = -1;
        }

        playlistSongsVersion = library.Version;
        playlistSongsId = playlist.Id;
        playlistMetaLanguage = language;
        playlistSongs = library.PlaylistSongs(playlist.Id);
        playlistArt = CoverArt.Of(playlist);
        playlistMeta = string.Format(Loc.Culture, Loc.T(L.Music.Library.Summary),
            MusicUi.SongCount(playlistSongs.Length), MusicUi.LongDuration(MusicUi.TotalSeconds(playlistSongs)));
        if (rowSprings.Length != playlistSongs.Length)
        {
            rowSprings = new Spring[playlistSongs.Length];
        }
    }

    private void DrawPlaylistHeader(PlaylistRecord playlist, bool editing, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var textWidth = MathF.Max(1f, width - inset * 2f);
        LibraryKit.Gap(Metrics.Space.Md);
        var origin = ImGui.GetCursorScreenPos();
        var side = MathF.Min(width * DetailCoverFraction, DetailCoverMax * scale);
        var coverMin = new Vector2(origin.X + (width - side) * 0.5f, origin.Y);
        var drawList = ImGui.GetWindowDrawList();
        LibraryArt.DrawCover(drawList, images, wallpaperImages, coverMin, side, playlistArt, playlist.Name);
        var coverMax = coverMin + new Vector2(side, side);
        var coverHovered = editing && UiInteract.Hover(coverMin, coverMax);
        ArtworkTile.DrawPressed(drawList, coverMin, side, coverHovered);
        ImGui.Dummy(new Vector2(width, side));
        if (editing && UiInteract.Click(coverMin, coverMax, coverHovered))
        {
            OpenCoverPicker(playlist.Id);
        }

        LibraryKit.Gap(Metrics.Space.Md);
        if (editing)
        {
            DrawCoverLinks(playlist, width, scale);
            DrawEditFields(textWidth, inset, scale);
            LibraryKit.Gap(Metrics.Space.Md);
            return;
        }

        LibraryKit.CenteredText(playlist.Name, TextStyles.Title2, ui.TitleInk, textWidth);
        if (playlist.SourceUrl.Length > 0)
        {
            DrawSourceLine(playlist, width, scale);
        }

        if (playlist.Description.Length > 0)
        {
            LibraryKit.Gap(Metrics.Space.Xs);
            var top = ImGui.GetCursorScreenPos();
            var height = LibraryKit.CenteredText(playlist.Description, TextStyles.Subheadline, ui.MutedInk, textWidth);
            var min = new Vector2(top.X + inset, top.Y);
            var max = new Vector2(top.X + width - inset, top.Y + height);
            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                BeginPlaylistEdit(playlist.Id, false, true);
            }
        }

        LibraryKit.Gap(Metrics.Space.Lg);
        var (play, shuffle) = LibraryKit.PlayShuffleRow(ui, Loc.T(L.Music.Library.Play), Loc.T(L.Music.Shuffle),
            playlistSongs.Length > 0);
        if (play)
        {
            playback.PlaySongs(playlistSongs, 0, playlist.Id, playlist.Name);
        }
        else if (shuffle)
        {
            playback.PlaySongsShuffled(playlistSongs, playlist.Id, playlist.Name);
        }

        LibraryKit.Gap(Metrics.Space.Md);
    }

    private void DrawSourceLine(PlaylistRecord playlist, float width, float scale)
    {
        LibraryKit.Gap(Metrics.Space.Xxs);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(Refreshing(playlist.Id) ? L.Music.Library.Refreshing : L.Music.Library.ImportedFrom);
        var height = MathF.Max(Typography.LineHeight(TextStyles.Footnote), RefreshHitRadius * 2f * scale);
        var glyphBox = Typography.LineHeight(TextStyles.Footnote);
        var buttonBox = RefreshHitRadius * 2f * scale;
        var gap = Metrics.Space.Xs * scale;
        var available = MathF.Max(1f, width - MusicUi.Inset * 2f * scale - glyphBox - buttonBox - gap * 2f);
        var fitted = Typography.FitText(label, available, TextStyles.Footnote);
        var labelSize = Typography.Measure(fitted, TextStyles.Footnote);
        var total = glyphBox + gap + labelSize.X + gap + buttonBox;
        var left = origin.X + (width - total) * 0.5f;
        var centerY = origin.Y + height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + glyphBox * 0.5f, centerY), IconGlyph.Of(FontAwesomeIcon.Link),
            ui.MutedInk, LinkGlyphScale);
        Typography.Draw(drawList, new Vector2(left + glyphBox + gap, centerY - labelSize.Y * 0.5f), fitted,
            ui.MutedInk, TextStyles.Footnote);
        var buttonCenter = new Vector2(left + total - buttonBox * 0.5f, centerY);
        if (Refreshing(playlist.Id))
        {
            LoadingPulse.Spinner(buttonCenter, buttonBox * 0.3f, ui.Accent, 1f, drawList);
        }
        else if (ui.IconButton(buttonCenter, RefreshHitRadius * scale, IconGlyph.Of(FontAwesomeIcon.Sync), ui.Accent,
                     AppSkin.Transparent, RefreshGlyphScale, Loc.T(L.Common.Refresh)))
        {
            StartPlaylistRefresh(playlist);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawCoverLinks(PlaylistRecord playlist, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var change = Loc.T(L.Music.Library.ChangeCover);
        var remove = Loc.T(L.Music.Library.RemoveCover);
        var hasCover = playlist.CoverPath.Length > 0;
        var gap = Metrics.Space.Xl * scale;
        var changeWidth = Typography.Measure(change, TextStyles.SubheadlineEmphasized).X;
        var removeWidth = hasCover ? Typography.Measure(remove, TextStyles.SubheadlineEmphasized).X : 0f;
        var total = changeWidth + (hasCover ? gap + removeWidth : 0f);
        var left = origin.X + MathF.Max(0f, (width - total) * 0.5f);
        if (TextLink(drawList, new Vector2(left, origin.Y), change, ui.Accent, height))
        {
            OpenCoverPicker(playlist.Id);
        }

        if (hasCover && TextLink(drawList, new Vector2(left + changeWidth + gap, origin.Y), remove, ui.Theme.Danger,
                height))
        {
            var old = playlist.CoverPath;
            library.SetPlaylistCover(playlist.Id, string.Empty);
            PlaylistCovers.Delete(old);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        LibraryKit.Gap(Metrics.Space.Md);
    }

    private static bool TextLink(ImDrawListPtr drawList, Vector2 min, string label, Vector4 ink, float height)
    {
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var max = new Vector2(min.X + size.X, min.Y + height);
        var hovered = UiInteract.Hover(min, max);
        var color = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? Palette.WithAlpha(ink, 0.6f) : ink;
        Typography.Draw(drawList, min, label, color, TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void DrawEditFields(float textWidth, float inset, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var nameField = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + inset + textWidth, origin.Y + LibraryKit.FieldHeight * scale));
        var submitted = LibraryKit.TextField(nameField, "##musicPlaylistName", Loc.T(L.Music.PlaylistNameHint),
            ref editName, LibraryStore.NameLimit, focusEditName, ui, out var nameActive);
        focusEditName = false;
        if (submitted || !nameActive)
        {
            CommitPlaylistName();
        }

        var descriptionTop = nameField.Max.Y + Metrics.Space.Sm * scale;
        var descriptionField = new Rect(new Vector2(nameField.Min.X, descriptionTop),
            new Vector2(nameField.Max.X, descriptionTop + DescriptionFieldHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, descriptionField.Min, descriptionField.Max, LibraryKit.ButtonRadius * scale,
            ImGui.GetColorU32(ui.FieldSurface));
        var pad = Metrics.Space.Sm * scale;
        var innerMin = descriptionField.Min + new Vector2(pad, pad * 0.5f);
        var innerSize = descriptionField.Size - new Vector2(pad * 2f, pad);
        if (editDescription.Length == 0)
        {
            Typography.Draw(drawList, innerMin + ImGui.GetStyle().FramePadding, Loc.T(L.Music.Library.AddDescription),
                ui.MutedInk, TextStyles.Body);
        }

        ImGui.SetCursorScreenPos(innerMin);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            var wrapWidth = innerSize.X - ImGui.GetStyle().FramePadding.X * 2f - Metrics.Space.Xxs * scale;
            if (focusEditDescription)
            {
                focusEditDescription = false;
                ImGui.SetKeyboardFocusHere();
            }

            SoftWrapField.Multiline("##musicPlaylistDescription", ref editDescription, LibraryStore.DescriptionLimit,
                innerSize, wrapWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, descriptionField.Max.Y - origin.Y));
    }

    private void BeginPlaylistEdit(string playlistId, bool focusName, bool focusDescription)
    {
        if (playlistEditing)
        {
            CommitPlaylistEdit();
        }

        var record = library.FindPlaylist(playlistId);
        playlistEditing = true;
        editPlaylistId = playlistId;
        editName = record?.Name ?? string.Empty;
        editDescription = record?.Description ?? string.Empty;
        focusEditName = focusName && !focusDescription;
        focusEditDescription = focusDescription;
        playlistEditFrame = ImGui.GetFrameCount();
        dragIndex = -1;
    }

    private void EndPlaylistEdit()
    {
        CommitPlaylistEdit();
        playlistEditing = false;
        dragIndex = -1;
    }

    private void CommitPlaylistName()
    {
        if (library.FindPlaylist(editPlaylistId) is not { } record)
        {
            return;
        }

        var name = editName.Trim();
        if (name.Length == 0 || string.Equals(name, record.Name, StringComparison.Ordinal))
        {
            return;
        }

        library.RenamePlaylist(editPlaylistId, name);
    }

    private void CommitPlaylistEdit()
    {
        CommitPlaylistName();
        if (library.FindPlaylist(editPlaylistId) is not { } record)
        {
            return;
        }

        var description = editDescription.Trim();
        if (!string.Equals(description, record.Description, StringComparison.Ordinal))
        {
            library.SetPlaylistDescription(editPlaylistId, description);
        }
    }

    private void PumpPlaylistEdit()
    {
        if (!playlistEditing || ImGui.GetFrameCount() - playlistEditFrame <= 1)
        {
            return;
        }

        EndPlaylistEdit();
    }

    private void DrawEditableRows(string playlistId, float scale)
    {
        var count = playlistSongs.Length;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pitch = SongRow.Height * scale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        var drawList = ImGui.GetWindowDrawList();
        if (dragIndex >= count)
        {
            dragIndex = -1;
        }

        if (dragIndex >= 0)
        {
            UpdateRowDrag(playlistId, origin, pitch, count, scale, delta);
        }

        var clipTop = drawList.GetClipRectMin().Y;
        var clipBottom = drawList.GetClipRectMax().Y;
        var removeIndex = -1;
        for (var index = 0; index < count; index++)
        {
            var displacement = 0f;
            if (dragIndex >= 0 && index != dragIndex)
            {
                if (index > dragIndex && index <= dragTarget)
                {
                    displacement = -pitch;
                }
                else if (index < dragIndex && index >= dragTarget)
                {
                    displacement = pitch;
                }
            }

            var offset = rowSprings[index].Step(displacement, Motion.PageSettle, delta);
            if (index == dragIndex)
            {
                continue;
            }

            var top = origin.Y + index * pitch + offset;
            if (top + pitch < clipTop || top > clipBottom)
            {
                continue;
            }

            if (DrawEditRow(drawList, index, new Vector2(origin.X, top), width, pitch, scale, false))
            {
                removeIndex = index;
            }
        }

        if (dragIndex >= 0)
        {
            DrawEditRow(drawList, dragIndex, new Vector2(origin.X, origin.Y + dragPointer - dragGrab), width, pitch,
                scale, true);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, count * pitch));
        if (removeIndex >= 0 && dragIndex < 0)
        {
            library.RemoveFromPlaylist(playlistId, playlistSongs[removeIndex].VideoId);
        }
    }

    private void UpdateRowDrag(string playlistId, Vector2 origin, float pitch, int count, float scale, float delta)
    {
        var mouseY = ImGui.GetMousePos().Y;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var target = Math.Clamp(dragTarget, 0, count - 1);
            var floatingTop = dragPointer - dragGrab;
            var from = dragIndex;
            dragIndex = -1;
            for (var index = 0; index < rowSprings.Length; index++)
            {
                rowSprings[index].SnapTo(0f);
            }

            rowSprings[target].SnapTo(floatingTop - target * pitch);
            if (target != from)
            {
                library.MoveInPlaylist(playlistId, from, target);
                playlistSongs = library.PlaylistSongs(playlistId);
            }

            return;
        }

        UiInteract.CancelPendingTap();
        AutoScroll(mouseY, scale, delta);
        dragPointer = mouseY - origin.Y;
        dragTarget = Math.Clamp((int)MathF.Round((dragPointer - dragGrab) / pitch), 0, count - 1);
    }

    private void AutoScroll(float mouseY, float scale, float delta)
    {
        var windowTop = ImGui.GetWindowPos().Y;
        var windowBottom = windowTop + ImGui.GetWindowHeight() - bottomChrome;
        var zone = AutoScrollZone * scale;
        var speed = 0f;
        if (mouseY < windowTop + zone)
        {
            speed = -(windowTop + zone - mouseY) / zone;
        }
        else if (mouseY > windowBottom - zone)
        {
            speed = (mouseY - windowBottom + zone) / zone;
        }

        if (speed == 0f)
        {
            return;
        }

        var step = Math.Clamp(speed, -1f, 1f) * AutoScrollSpeed * scale * delta;
        ImGui.SetScrollY(Math.Clamp(ImGui.GetScrollY() + step, 0f, ImGui.GetScrollMaxY()));
    }

    private bool DrawEditRow(ImDrawListPtr drawList, int index, Vector2 min, float width, float pitch, float scale,
        bool floating)
    {
        var song = playlistSongs[index];
        var max = new Vector2(min.X + width, min.Y + pitch);
        var inset = MusicUi.Inset * scale;
        if (floating)
        {
            var drop = new Vector2(0f, FloatingShadowDrop * scale);
            drawList.AddRectFilled(min + drop, max + drop,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, FloatingShadowAlpha)));
            drawList.AddRectFilled(min, max,
                ImGui.GetColorU32(Palette.Lighten(ui.BackdropColor, FloatingLift)));
        }

        var buttonRadius = EditButtonRadius * scale;
        var buttonCenter = new Vector2(min.X + inset + buttonRadius, min.Y + pitch * 0.5f);
        var removed = DrawRemoveBadge(drawList, buttonCenter, buttonRadius, !floating && dragIndex < 0);

        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(buttonCenter.X + buttonRadius + Metrics.Space.Md * scale,
            min.Y + (pitch - side) * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, side, song.ThumbnailUrl, song.Title);
        var gripWidth = GripWidth * scale;
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        DrawRowText(drawList, new Rect(min, max), textLeft, max.X - gripWidth - Metrics.Space.Xs * scale, song.Title,
            song.Author);
        var gripCenter = new Vector2(max.X - gripWidth * 0.5f - Metrics.Space.Xs * scale, min.Y + pitch * 0.5f);
        AppSkin.Icon(drawList, gripCenter, IconGlyph.Of(FontAwesomeIcon.Bars), ui.MutedInk, GripGlyphScale);
        FeedCell.Hairline(drawList, textLeft, max.X, max.Y, ui.Hairline);
        DrawGrip(index, new Vector2(max.X - gripWidth - Metrics.Space.Xs * scale, min.Y),
            new Vector2(max.X, max.Y), min.Y);
        return removed;
    }

    private bool DrawRemoveBadge(ImDrawListPtr drawList, Vector2 center, float radius, bool interactive)
    {
        var extent = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - extent, center + extent);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var fill = pressed ? Palette.WithAlpha(ui.Theme.Danger, RemovePressedAlpha) : ui.Theme.Danger;
        drawList.AddCircleFilled(center, radius * RemoveBadgeFraction, ImGui.GetColorU32(fill));
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Minus), RemoveInk, EditButtonScale * 0.6f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return interactive && UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void DrawGrip(int index, Vector2 gripMin, Vector2 gripMax, float rowTop)
    {
        var saved = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(gripMin);
        ImGui.PushID(index);
        ImGui.InvisibleButton("##musicGrip", gripMax - gripMin);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(gripMin, gripMax);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.PopID();
        ImGui.SetCursorScreenPos(saved);
        if (hovered || dragIndex == index)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        }

        if (!activated || dragIndex >= 0)
        {
            return;
        }

        var mouseY = ImGui.GetMousePos().Y;
        dragIndex = index;
        dragTarget = index;
        var listTop = rowTop - index * SongRow.Height * UiScale.Current - rowSprings[index].Value;
        dragPointer = mouseY - listTop;
        dragGrab = mouseY - rowTop;
        UiInteract.CancelPendingTap();
    }

    private void OpenCoverPicker(string playlistId)
    {
        coverPicker ??= new ImagePickCrop(photoLibrary, wallpaperImages);
        coverPicker.Open();
        coverPicking = true;
        coverPlaylistId = playlistId;
        coverFailed = false;
    }

    private void DrawCoverPicker(in PhoneContext context)
    {
        if (coverPicker is null)
        {
            coverPicking = false;
            return;
        }

        var labels = new ImagePickCropLabels(Loc.T(L.Music.Library.CoverTitle), Loc.T(L.Common.ImportFromPc),
            Loc.T(L.Common.NoPhotos), Loc.T(L.Account.MoveAndScale), Loc.T(L.Account.Use), Loc.T(L.Account.Saving),
            Loc.T(L.Account.GestureHint));
        var result = coverPicker.Draw(context.Content, context, labels, ui.Accent, coverSaving);
        if (result == ImagePickCropEvent.Cancelled)
        {
            coverPicking = false;
            return;
        }

        if (result != ImagePickCropEvent.Committed || coverSaving || coverPicker.SourcePath.Length == 0)
        {
            return;
        }

        coverSaving = true;
        var folder = PlaylistCovers.Folder;
        var playlistId = coverPlaylistId;
        var source = coverPicker.SourcePath;
        var crop = coverPicker.Crop;
        _ = Task.Run(() =>
        {
            try
            {
                Volatile.Write(ref coverSavedPath, PlaylistCovers.Save(folder, playlistId, source, crop));
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Playlist cover could not be saved");
                coverFailed = true;
            }
            finally
            {
                coverSaving = false;
            }
        });
    }

    private void PumpCoverWork()
    {
        var saved = Interlocked.Exchange(ref coverSavedPath, null);
        if (saved is not null)
        {
            coverPicking = false;
            if (library.FindPlaylist(coverPlaylistId) is not { } target)
            {
                PlaylistCovers.Delete(saved);
                return;
            }

            var old = target.CoverPath;
            library.SetPlaylistCover(coverPlaylistId, saved);
            PlaylistCovers.Delete(old);
            return;
        }

        if (!coverFailed)
        {
            return;
        }

        coverFailed = false;
        coverPicking = false;
        ShellToast.Show(Loc.T(L.Music.Library.CoverFailed));
    }

    private void StartPlaylistRefresh(PlaylistRecord playlist)
    {
        if (playlist.SourceUrl.Length == 0)
        {
            return;
        }

        refreshImporter ??= new PlaylistImporter(songResolver);
        if (refreshImporter.Busy)
        {
            return;
        }

        refreshPlaylistId = playlist.Id;
        refreshImporter.Start(playlist.SourceUrl);
    }

    private void PumpRefreshWork()
    {
        if (refreshImporter is null)
        {
            return;
        }

        var phase = refreshImporter.Phase;
        if (phase == ImportPhase.Ready && refreshImporter.Preview is { } preview)
        {
            var added = library.AddRangeToPlaylist(refreshPlaylistId, preview.Songs);
            ShellToast.Show(added > 0
                ? Loc.Plural(L.Music.Library.RefreshAdded, added)
                : Loc.T(L.Music.Library.RefreshNone));
            refreshImporter.Reset();
            return;
        }

        if (phase != ImportPhase.Failed)
        {
            return;
        }

        ShellToast.Show(Loc.T(refreshImporter.Failure == ImportFailure.NotInstalled
            ? L.Music.Library.NotInstalled
            : L.Music.Library.RefreshFailed));
        refreshImporter.Reset();
    }
}
