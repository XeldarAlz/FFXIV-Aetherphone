using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music.Components;

internal sealed class PlaylistPickerSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float HeaderHeight = 40f;
    private const float RowHeight = 58f;
    private const float FieldRowHeight = 52f;
    private const float CreateWidth = 72f;
    private const float MutedAlpha = 0.6f;
    private const float HoverAlpha = 0.07f;
    private const float CheckScale = 0.7f;
    private const float PlusScale = 0.9f;
    private const float TileFillAlpha = 0.12f;

    private readonly Sheet sheet = new();
    private Song song;
    private bool naming;
    private bool focusName;
    private string nameDraft = string.Empty;

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open(in Song target)
    {
        if (target.IsEmpty)
        {
            return;
        }

        song = target;
        naming = false;
        focusName = false;
        nameDraft = string.Empty;
        sheet.Open();
    }

    public void Close() => sheet.Close();

    public void Draw(Rect screen, MusicKit kit)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##musicPlaylistPicker", screen.Size, false, OverlayFlags))
        {
            var veil = SheetMetrics.VeilFor(WallpaperBackdrop.FlatAvailable);
            var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, kit.Ui.Theme,
                SheetDetents.Standard(screen.Height), veil);
            if (!frame.Visible)
            {
                return;
            }

            DrawContent(in frame, kit);
            sheet.End(in frame);
        }
    }

    private void DrawContent(in SheetFrame frame, MusicKit kit)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var headerHeight = HeaderHeight * scale;
        Typography.DrawCentered(frame.DrawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.5f),
            Loc.T(L.Music.AddToPlaylist), ink, TextStyles.Headline);
        var rows = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        using (AppSurface.BeginEdgeToEdge(rows))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (naming)
            {
                DrawNameRow(kit, frame.Interactive);
            }
            else if (DrawNewRow(drawList, kit, ink, frame.Interactive))
            {
                naming = true;
                focusName = true;
            }

            var playlists = kit.Library.Playlists;
            for (var index = 0; index < playlists.Count; index++)
            {
                if (DrawPlaylistRow(drawList, kit, playlists[index], ink, frame.Interactive))
                {
                    AddTo(kit.Library, playlists[index].Id, playlists[index].Name);
                    return;
                }
            }
        }
    }

    private bool DrawNewRow(ImDrawListPtr drawList, MusicKit kit, Vector4 ink, bool interactive)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, RowHeight * scale, Palette.WithAlpha(ink, HoverAlpha), interactive);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + MusicUi.Inset * scale,
            cell.Bounds.Min.Y + (cell.Bounds.Height - side) * 0.5f);
        var artMax = artMin + new Vector2(side, side);
        Squircle.Fill(drawList, artMin, artMax, side * ArtworkTile.TileRadiusFraction,
            ImGui.GetColorU32(Palette.WithAlpha(ink, TileFillAlpha)));
        AppSkin.Icon(drawList, (artMin + artMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Plus), kit.Ui.Accent,
            PlusScale);
        var label = Loc.T(L.Music.NewPlaylist);
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(artMax.X + Metrics.Space.Md * scale,
            cell.Bounds.Min.Y + (cell.Bounds.Height - labelHeight) * 0.5f), label, kit.Ui.Accent, TextStyles.Body);
        FeedCell.End(drawList, cell, Palette.WithAlpha(ink, HoverAlpha));
        return cell.Tapped;
    }

    private void DrawNameRow(MusicKit kit, bool interactive)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = FieldRowHeight * scale;
        var inset = MusicUi.Inset * scale;
        var createWidth = CreateWidth * scale;
        var field = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset - createWidth - Metrics.Space.Sm * scale, origin.Y + height));
        if (focusName)
        {
            focusName = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SubmitField.Draw(field, "##musicNewPlaylist", Loc.T(L.Music.PlaylistNameHint),
            ref nameDraft, kit.Ui.Theme, LibraryStore.NameLimit, FontAwesomeIcon.Plus);
        var createRect = new Rect(new Vector2(field.Max.X + Metrics.Space.Sm * scale, field.Center.Y -
                Metrics.Size.FieldHeight * scale * 0.5f),
            new Vector2(origin.X + width - inset, field.Center.Y + Metrics.Size.FieldHeight * scale * 0.5f));
        var hasName = nameDraft.Trim().Length > 0;
        var created = AppSkin.PillButton(createRect, Loc.T(L.Music.CreatePlaylist), true, hasName && interactive,
            kit.Ui.Theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (!hasName || !interactive || (!submitted && !created))
        {
            return;
        }

        var name = nameDraft.Trim();
        var id = kit.Library.CreatePlaylist(name);
        AddTo(kit.Library, id, name);
    }

    private bool DrawPlaylistRow(ImDrawListPtr drawList, MusicKit kit, PlaylistRecord playlist, Vector4 ink,
        bool interactive)
    {
        var scale = UiScale.Current;
        var height = RowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var cell = FeedCell.Begin(drawList, height, Palette.WithAlpha(ink, HoverAlpha), interactive);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var inset = MusicUi.Inset * scale;
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        var cover = playlist.Songs.Count > 0 ? playlist.Songs[0].ThumbnailUrl : string.Empty;
        ArtworkTile.Draw(drawList, kit.Images, artMin, side, cover, playlist.Name);
        var contains = kit.Library.PlaylistContains(playlist.Id, song.VideoId);
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var textRight = cell.Bounds.Max.X - inset - (contains ? Metrics.Size.TapTarget * scale * 0.5f : 0f);
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Body);
        var countHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = cell.Bounds.Min.Y + (height - nameHeight - countHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(playlist.Name, textWidth,
            TextStyles.Body), ink, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(MusicUi.SongCount(playlist.Songs.Count), textWidth, TextStyles.Subheadline),
            Palette.WithAlpha(ink, ink.W * MutedAlpha), TextStyles.Subheadline);
        if (contains)
        {
            AppSkin.Icon(drawList, new Vector2(cell.Bounds.Max.X - inset - Metrics.Space.Sm * scale,
                cell.Bounds.Min.Y + height * 0.5f), IconGlyph.Of(FontAwesomeIcon.Check), kit.Ui.Accent, CheckScale);
        }

        FeedCell.End(drawList, cell, Palette.WithAlpha(ink, HoverAlpha));
        return cell.Tapped;
    }

    private void AddTo(LibraryStore library, string playlistId, string playlistName)
    {
        library.AddToPlaylist(playlistId, song);
        ShellToast.Show(string.Format(Loc.Culture, Loc.T(L.Music.AddedToPlaylist), playlistName));
        naming = false;
        sheet.Close();
    }
}
