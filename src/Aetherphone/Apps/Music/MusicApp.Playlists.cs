using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private enum OverlayMode : byte
    {
        None,
        Pick,
        Name,
    }

    private const float PlaylistTileHeight = 74f;
    private const float PickHeaderHeight = 60f;
    private const float PickRowHeight = 56f;
    private const float PickNewRowHeight = 56f;
    private const float PickPad = 10f;
    private const float DetailHeaderHeight = 64f;
    private const float DetailRowHeight = 60f;
    private const int NameLimit = 60;

    private string selectedPlaylistId = string.Empty;
    private OverlayMode overlay = OverlayMode.None;
    private OverlayMode lastOverlay = OverlayMode.Pick;
    private Spring overlayPresence;
    private Song pendingSong;
    private bool nameIsRename;
    private string nameTargetId = string.Empty;
    private string nameDraft = string.Empty;
    private bool focusNameField;
    private Rect playlistMenuAnchor;
    private readonly DropdownMenu playlistMenu = new();

    private Song CurrentSong()
    {
        var songs = playback.Songs;
        return new Song(songs.CurrentVideoId, songs.CurrentTitle, songs.CurrentAuthor, songs.CurrentThumbnail,
            (int)songs.Duration);
    }

    private static string SongCountLabel(int count)
    {
        return count == 1 ? Loc.T(L.Music.SongOne) : string.Format(Loc.T(L.Music.SongsMany), count);
    }

    private void OpenPicker(in Song song)
    {
        if (string.IsNullOrEmpty(song.VideoId))
        {
            return;
        }

        pendingSong = song;
        playlistMenu.Close();
        overlay = OverlayMode.Pick;
    }

    private void OpenPlaylist(string id)
    {
        selectedPlaylistId = id;
        router.Push(View.PlaylistDetail);
    }

    private void BeginCreateFromPicker()
    {
        nameIsRename = false;
        nameTargetId = string.Empty;
        nameDraft = string.Empty;
        focusNameField = true;
        overlay = OverlayMode.Name;
    }

    private void BeginCreateFromHome()
    {
        pendingSong = default;
        nameIsRename = false;
        nameTargetId = string.Empty;
        nameDraft = string.Empty;
        focusNameField = true;
        overlay = OverlayMode.Name;
    }

    private void BeginRenamePlaylist(string id)
    {
        if (playlists.Find(id) is not { } record)
        {
            return;
        }

        pendingSong = default;
        nameIsRename = true;
        nameTargetId = id;
        nameDraft = record.Name;
        focusNameField = true;
        overlay = OverlayMode.Name;
    }

    private void CommitName()
    {
        var name = nameDraft.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (nameIsRename)
        {
            playlists.Rename(nameTargetId, name);
            overlay = OverlayMode.None;
            nameDraft = string.Empty;
            return;
        }

        var id = playlists.Create(name);
        nameDraft = string.Empty;
        if (!string.IsNullOrEmpty(pendingSong.VideoId))
        {
            playlists.Add(id, pendingSong);
            overlay = OverlayMode.Pick;
            return;
        }

        overlay = OverlayMode.None;
        OpenPlaylist(id);
    }

    private void CancelName()
    {
        nameDraft = string.Empty;
        overlay = !nameIsRename && !string.IsNullOrEmpty(pendingSong.VideoId) ? OverlayMode.Pick : OverlayMode.None;
    }

    private void DismissOverlay(bool snap)
    {
        overlay = OverlayMode.None;
        nameDraft = string.Empty;
        playlistMenu.Close();
        if (snap)
        {
            overlayPresence.SnapTo(0f);
        }
    }

    private void AskDeletePlaylist(string id)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Music.DeletePlaylistConfirm),
            ConfirmLabel = Loc.T(L.Music.DeletePlaylistButton),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () =>
            {
                playlists.Delete(id);
                router.Pop();
            },
        });
    }

    private void DrawPlaylistShelf(float scale, float available)
    {
        var list = playlists.All;
        var gap = CardGap * scale;
        var tileWidth = (available - gap) * 0.5f;
        var tileHeight = PlaylistTileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var total = list.Count + 1;
        var rows = (total + 1) / 2;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < total; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var rounding = 10f * scale;
            var hovered = UiInteract.Hover(min, max);
            if (index == list.Count)
            {
                DrawNewPlaylistTile(drawList, min, max, rounding, hovered, scale);
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    BeginCreateFromHome();
                }

                continue;
            }

            var playlist = list[index];
            drawList.AddImageRounded(artwork.HandleForName(playlist.Name), min, max, Vector2.Zero, Vector2.One,
                0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
            Squircle.FillVerticalGradient(drawList, min, max, rounding, 0x00000000u, 0xC0000000u);
            if (hovered)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), rounding);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var textWidth = tileWidth - 24f * scale;
            var name = Typography.FitText(playlist.Name, textWidth, TextStyles.SubheadlineEmphasized);
            Typography.Draw(new Vector2(min.X + 12f * scale, max.Y - 34f * scale), name, White,
                TextStyles.SubheadlineEmphasized);
            var count = Typography.FitText(SongCountLabel(playlist.Songs.Count), textWidth, TextStyles.Caption1);
            Typography.Draw(new Vector2(min.X + 12f * scale, max.Y - 18f * scale), count,
                new Vector4(1f, 1f, 1f, 0.82f), TextStyles.Caption1);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenPlaylist(playlist.Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * tileHeight + (rows - 1) * gap));
    }

    private void DrawNewPlaylistTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered,
        float scale)
    {
        var fill = Palette.WithAlpha(ui.TitleInk, hovered ? 0.14f : 0.07f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.55f)), 1.2f);
        var center = new Vector2(min.X + (max.X - min.X) * 0.5f, min.Y + (max.Y - min.Y) * 0.5f);
        AppSkin.Icon(new Vector2(center.X, center.Y - 10f * scale), FontAwesomeIcon.Plus.ToIconString(), ui.Accent, 0.95f);
        var label = Loc.T(L.Music.NewPlaylist);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 14f * scale), label, ui.TitleInk,
            TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawPlaylistOverlay(Rect content, float scale, float presence, float delta)
    {
        if (presence <= 0.003f && overlay == OverlayMode.None)
        {
            return;
        }

        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##musicPlaylistOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f * presence)));

            if (overlay != OverlayMode.None)
            {
                lastOverlay = overlay;
            }

            var mode = overlay != OverlayMode.None ? overlay : lastOverlay;
            var margin = 10f * scale;
            var cardWidth = content.Width - margin * 2f;
            var cardLeft = content.Min.X + margin;
            var maxCardHeight = content.Height * 0.66f;
            var cardHeight = mode == OverlayMode.Name
                ? 184f * scale
                : PickCardHeight(scale, maxCardHeight);
            var slide = (1f - presence) * (cardHeight + 40f * scale);
            var cardBottom = content.Max.Y - margin + slide;
            var cardMin = new Vector2(cardLeft, cardBottom - cardHeight);
            var cardMax = new Vector2(cardLeft + cardWidth, cardBottom);
            var rounding = 22f * scale;
            drawList.AddRectFilled(cardMin + new Vector2(0f, 6f * scale), cardMax + new Vector2(0f, 8f * scale),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f * presence)), rounding);
            var fill = Palette.Mix(AppPalettes.Music.BackdropTop, White, 0.08f);
            Squircle.Fill(drawList, cardMin, cardMax, rounding, ImGui.GetColorU32(fill));
            Squircle.Stroke(drawList, cardMin, cardMax, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), 1f);
            var handleWidth = 36f * scale;
            var handleCenterX = (cardMin.X + cardMax.X) * 0.5f;
            drawList.AddRectFilled(new Vector2(handleCenterX - handleWidth * 0.5f, cardMin.Y + 8f * scale),
                new Vector2(handleCenterX + handleWidth * 0.5f, cardMin.Y + 12f * scale),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), 2f * scale);

            var interactive = presence > 0.6f && overlay != OverlayMode.None;
            var cardRect = new Rect(cardMin, cardMax);
            if (mode == OverlayMode.Name)
            {
                DrawNameCard(drawList, cardRect, scale, interactive);
            }
            else
            {
                DrawPickCard(drawList, cardRect, scale, interactive);
            }

            if (interactive && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                !ImGui.IsMouseHoveringRect(cardMin, cardMax))
            {
                if (overlay == OverlayMode.Name)
                {
                    CancelName();
                }
                else
                {
                    DismissOverlay(false);
                }
            }
        }
    }

    private float PickCardHeight(float scale, float maxCardHeight)
    {
        var count = playlists.Count;
        var rowsHeight = count == 0 ? 96f * scale : count * PickRowHeight * scale;
        var desired = PickHeaderHeight * scale + rowsHeight + PickNewRowHeight * scale + PickPad * scale * 2f;
        return MathF.Min(desired, maxCardHeight);
    }

    private void DrawPickCard(ImDrawListPtr drawList, Rect card, float scale, bool interactive)
    {
        var pad = 16f * scale;
        var title = Loc.T(L.Music.AddToPlaylist);
        Typography.Draw(new Vector2(card.Min.X + pad, card.Min.Y + 20f * scale), title, ui.TitleInk,
            TextStyles.Title3);
        if (!string.IsNullOrEmpty(pendingSong.Title))
        {
            var songLine = Typography.FitText(pendingSong.Title, card.Width - pad * 2f, TextStyles.Caption1);
            Typography.Draw(new Vector2(card.Min.X + pad, card.Min.Y + 42f * scale), songLine, ui.MutedInk,
                TextStyles.Caption1);
        }

        var newRowTop = card.Max.Y - PickPad * scale - PickNewRowHeight * scale;
        var rowsRegion = new Rect(new Vector2(card.Min.X + 6f * scale, card.Min.Y + PickHeaderHeight * scale),
            new Vector2(card.Max.X - 6f * scale, newRowTop));
        ImGui.SetCursorScreenPos(rowsRegion.Min);
        using (ImRaii.Child("##musicPickRows", rowsRegion.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var list = playlists.All;
            if (list.Count == 0)
            {
                Typography.DrawWrappedCentered(rowsRegion.Center, Loc.T(L.Music.NoPlaylistsYet), ui.MutedInk,
                    TextStyles.Subheadline, rowsRegion.Width - 32f * scale);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 2f * scale));
                for (var index = 0; index < list.Count; index++)
                {
                    DrawPickRow(list[index], scale, interactive);
                }
            }
        }

        drawList.AddLine(new Vector2(card.Min.X + pad, newRowTop), new Vector2(card.Max.X - pad, newRowTop),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), 1f);
        var newMin = new Vector2(card.Min.X + 6f * scale, newRowTop);
        var newMax = new Vector2(card.Max.X - 6f * scale, card.Max.Y - PickPad * scale);
        var newHovered = interactive && UiInteract.Hover(newMin, newMax);
        if (newHovered)
        {
            Squircle.Fill(drawList, newMin, newMax, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var newCenterY = (newMin.Y + newMax.Y) * 0.5f;
        var iconCenter = new Vector2(newMin.X + 26f * scale, newCenterY);
        drawList.AddCircleFilled(iconCenter, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.18f)), 24);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Plus.ToIconString(), ui.Accent, 0.9f);
        var newLabelSize = Typography.Measure(Loc.T(L.Music.NewPlaylist), TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(iconCenter.X + 26f * scale, newCenterY - newLabelSize.Y * 0.5f),
            Loc.T(L.Music.NewPlaylist), ui.Accent, TextStyles.BodyEmphasized);
        if (newHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            BeginCreateFromPicker();
        }
    }

    private void DrawPickRow(PlaylistRecord playlist, float scale, bool interactive)
    {
        var rowHeight = PickRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var contains = playlists.Contains(playlist.Id, pendingSong.VideoId);
        var hovered = interactive && UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 40f * scale;
        var artMin = new Vector2(min.X + 4f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        drawList.AddImageRounded(artwork.HandleForName(playlist.Name), artMin, artMax, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, 8f * scale, ImDrawFlags.RoundCornersAll);
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - 44f * scale - textLeft;
        var name = Typography.FitText(playlist.Name, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(textLeft, min.Y + 9f * scale), name, ui.TitleInk, TextStyles.BodyEmphasized);
        var count = Typography.FitText(SongCountLabel(playlist.Songs.Count), textWidth, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, min.Y + 30f * scale), count, ui.MutedInk, TextStyles.Caption1);
        var indicatorCenter = new Vector2(max.X - 24f * scale, min.Y + rowHeight * 0.5f);
        if (contains)
        {
            drawList.AddCircleFilled(indicatorCenter, 12f * scale, ImGui.GetColorU32(ui.Accent), 24);
            AppSkin.Icon(indicatorCenter, FontAwesomeIcon.Check.ToIconString(), White, 0.72f);
        }
        else
        {
            drawList.AddCircle(indicatorCenter, 12f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.35f)), 24, 1.4f * scale);
            AppSkin.Icon(indicatorCenter, FontAwesomeIcon.Plus.ToIconString(), ui.MutedInk, 0.7f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (contains)
        {
            playlists.Remove(playlist.Id, pendingSong.VideoId);
        }
        else
        {
            playlists.Add(playlist.Id, pendingSong);
        }
    }

    private void DrawNameCard(ImDrawListPtr drawList, Rect card, float scale, bool interactive)
    {
        var pad = 16f * scale;
        var title = nameIsRename ? Loc.T(L.Music.RenamePlaylist) : Loc.T(L.Music.NewPlaylist);
        Typography.Draw(new Vector2(card.Min.X + pad, card.Min.Y + 20f * scale), title, ui.TitleInk,
            TextStyles.Title3);

        var fieldMin = new Vector2(card.Min.X + pad, card.Min.Y + 58f * scale);
        var fieldMax = new Vector2(card.Max.X - pad, fieldMin.Y + 42f * scale);
        var fieldRadius = (fieldMax.Y - fieldMin.Y) * 0.5f;
        Squircle.Fill(drawList, fieldMin, fieldMax, fieldRadius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        var centerY = (fieldMin.Y + fieldMax.Y) * 0.5f;
        var hint = Loc.T(L.Music.PlaylistNameHint);
        if (focusNameField)
        {
            focusNameField = false;
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 14f * scale, centerY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 28f * scale);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(nameDraft);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            submitted = ImGui.InputTextWithHint("##playlistName", hint, ref nameDraft, NameLimit,
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var buttonHeight = 44f * scale;
        var buttonTop = card.Max.Y - pad - buttonHeight;
        var buttonGap = 10f * scale;
        var buttonWidth = (card.Width - pad * 2f - buttonGap) * 0.5f;
        var cancelRect = new Rect(new Vector2(card.Min.X + pad, buttonTop),
            new Vector2(card.Min.X + pad + buttonWidth, buttonTop + buttonHeight));
        var createRect = new Rect(new Vector2(cancelRect.Max.X + buttonGap, buttonTop),
            new Vector2(card.Max.X - pad, buttonTop + buttonHeight));
        var canCreate = nameDraft.Trim().Length > 0;
        if (ui.PillButton(cancelRect, Loc.T(L.Common.Cancel), false) && interactive)
        {
            CancelName();
        }

        var createClicked = AppSkin.PillButton(createRect, Loc.T(L.Music.CreatePlaylist), true, canCreate, theme);
        if ((submitted || (createClicked && interactive)) && canCreate)
        {
            CommitName();
        }
    }

    private void DrawPlaylistDetail(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        if (playlists.Find(selectedPlaylistId) is not { } record)
        {
            router.Pop();
            return;
        }

        DrawPlaylistDetailTopBar(content, record, scale);
        var songs = playlists.Songs(record.Id);
        var headerTop = content.Min.Y + TopBarHeight * scale;
        var headerBottom = headerTop + DetailHeaderHeight * scale;
        DrawPlaylistDetailHeader(content, record, songs, headerTop, scale);
        var body = new Rect(new Vector2(content.Min.X, headerBottom),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        if (songs.Length == 0)
        {
            var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
            Typography.DrawCentered(center, Loc.T(L.Music.PlaylistEmptyTitle), ui.TitleInk, TextStyles.Title3);
            Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale),
                Loc.T(L.Music.PlaylistEmptySub), ui.MutedInk, TextStyles.Subheadline, body.Width - 48f * scale);
        }
        else
        {
            using (AppSurface.Begin(body))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < songs.Length; index++)
                {
                    DrawPlaylistSongRow(scale, songs[index], index, songs, record);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }
        }

        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        var items = new[]
        {
            new DropdownMenu.Item(Loc.T(L.Music.RenamePlaylist), FontAwesomeIcon.Pen.ToIconString()),
            new DropdownMenu.Item(Loc.T(L.Music.DeletePlaylist), FontAwesomeIcon.TrashAlt.ToIconString(), true),
        };
        var picked = playlistMenu.Draw(screen, theme, items);
        if (picked == 0)
        {
            BeginRenamePlaylist(record.Id);
        }
        else if (picked == 1)
        {
            AskDeletePlaylist(record.Id);
        }
    }

    private void DrawPlaylistDetailTopBar(Rect content, PlaylistRecord record, float scale)
    {
        var rowCenterY = content.Min.Y + TopBarHeight * scale * 0.5f;
        var backHovered = UiInteract.Hover(content.Min,
            new Vector2(content.Min.X + 40f * scale, content.Min.Y + TopBarHeight * scale));
        if (BackButton.Draw("music.playlist.back", new Vector2(content.Min.X + 18f * scale, rowCenterY), 15f * scale,
                ui.TitleInk, backHovered, scale))
        {
            router.Pop();
        }

        var titleLeft = content.Min.X + 38f * scale;
        var titleRight = content.Max.X - 46f * scale;
        var fitted = Typography.FitText(record.Name, titleRight - titleLeft, TextStyles.Title2);
        var titleSize = Typography.Measure(fitted, TextStyles.Title2);
        Typography.Draw(new Vector2(titleLeft, rowCenterY - titleSize.Y * 0.5f), fitted, ui.TitleInk,
            TextStyles.Title2);
        var menuCenter = new Vector2(content.Max.X - 22f * scale, rowCenterY);
        if (ui.IconButton(menuCenter, 15f * scale, FontAwesomeIcon.EllipsisV.ToIconString(), ui.TitleInk,
                AppSkin.Transparent, 0.8f))
        {
            playlistMenuAnchor = new Rect(menuCenter - new Vector2(16f * scale, 16f * scale),
                menuCenter + new Vector2(16f * scale, 16f * scale));
            playlistMenu.Toggle("music.playlistMenu", playlistMenuAnchor);
        }
    }

    private void DrawPlaylistDetailHeader(Rect content, PlaylistRecord record, Song[] songs, float headerTop,
        float scale)
    {
        var centerY = headerTop + DetailHeaderHeight * scale * 0.5f;
        var playRect = new Rect(new Vector2(content.Min.X + 16f * scale, centerY - 20f * scale),
            new Vector2(content.Min.X + 146f * scale, centerY + 20f * scale));
        if (AppSkin.PillButton(playRect, Loc.T(L.Music.PlayAll), true, songs.Length > 0, theme) && songs.Length > 0)
        {
            PlaySong(songs, 0, record.Name);
        }

        var countLabel = SongCountLabel(songs.Length);
        var countSize = Typography.Measure(countLabel, TextStyles.Subheadline);
        Typography.Draw(new Vector2(content.Max.X - 16f * scale - countSize.X, centerY - countSize.Y * 0.5f),
            countLabel, ui.MutedInk, TextStyles.Subheadline);
    }

    private void DrawPlaylistSongRow(float scale, Song song, int index, Song[] songs, PlaylistRecord record)
    {
        var rowHeight = DetailRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var current = IsCurrentSong(song);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 44f * scale;
        var artMin = new Vector2(min.X + 6f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        DrawCover(drawList, artMin, artMax, song.ThumbnailUrl, song.Title, 6f * scale);
        var showRemove = hovered && !current;
        var trailing = current ? 32f * scale : showRemove ? 40f * scale : 10f * scale;
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - trailing - textLeft;
        var title = Typography.FitText(song.Title, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(textLeft, min.Y + 10f * scale), title, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);
        var subtitle = Typography.FitText(SongRowSubtitle(song), textWidth, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, min.Y + 34f * scale), subtitle, ui.MutedInk, TextStyles.Caption1);
        var removeClicked = false;
        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - 18f * scale, min.Y + rowHeight * 0.5f), scale, 17f * scale,
                clock, ui.Accent, 1f, playback.IsPlaying);
        }
        else if (showRemove)
        {
            removeClicked = ui.IconButton(new Vector2(max.X - 22f * scale, min.Y + rowHeight * 0.5f), 15f * scale,
                FontAwesomeIcon.Minus.ToIconString(), ui.MutedInk, AppSkin.Transparent, 0.82f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (removeClicked)
        {
            playlists.Remove(record.Id, song.VideoId);
            return;
        }

        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (current)
        {
            playback.TogglePlayPause();
        }
        else
        {
            PlaySong(songs, index, record.Name);
        }
    }
}
