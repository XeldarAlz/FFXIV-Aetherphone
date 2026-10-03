using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music.Library;

internal sealed class AddSongsSheet : IDisposable
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float HeaderHeight = 40f;
    private const float SearchHeight = 50f;
    private const float RowHeight = 58f;
    private const float HoverAlpha = 0.07f;
    private const float MutedAlpha = 0.6f;
    private const float StateGlyphScale = 0.85f;
    private const float SpinnerRadius = 12f;
    private const int QueryLimit = 120;

    private readonly Sheet sheet = new();
    private readonly SongSearchService search;
    private string playlistId = string.Empty;
    private string query = string.Empty;
    private bool focusQuery;
    private bool searched;
    private volatile bool searching;
    private volatile Song[] results = Array.Empty<Song>();
    private CancellationTokenSource? fetch;

    public AddSongsSheet(SongSearchService search)
    {
        this.search = search;
    }

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open(string targetPlaylistId)
    {
        playlistId = targetPlaylistId;
        focusQuery = true;
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
        using (ImRaii.Child("##musicAddSongs", screen.Size, false, OverlayFlags))
        {
            var veil = SheetMetrics.VeilFor(WallpaperBackdrop.FlatAvailable);
            var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, kit.Ui.Theme,
                SheetDetents.Fitted(screen.Height * SheetMetrics.LargeFraction), veil);
            if (!frame.Visible)
            {
                return;
            }

            DrawContent(in frame, kit);
            sheet.End(in frame);
        }
    }

    public void Dispose()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = null;
    }

    private void DrawContent(in SheetFrame frame, MusicKit kit)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var headerHeight = HeaderHeight * scale;
        Typography.DrawCentered(frame.DrawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.5f),
            Loc.T(L.Music.Library.AddSongs), ink, TextStyles.Headline);
        var searchRect = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Min.Y + headerHeight + SearchHeight * scale));
        ImGui.SetCursorScreenPos(searchRect.Min);
        if (focusQuery && frame.Interactive)
        {
            focusQuery = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SearchField.DrawSubmit(searchRect, "##musicAddSongsQuery", Loc.T(L.Music.Library.AddSongsHint),
            ref query, kit.Ui.Theme, QueryLimit, MusicUi.Inset);
        if (submitted && frame.Interactive && query.Trim().Length > 0)
        {
            BeginSearch(query.Trim());
        }

        var rows = new Rect(new Vector2(content.Min.X, searchRect.Max.Y),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        if (searching)
        {
            LoadingPulse.Spinner(new Vector2(rows.Center.X, rows.Min.Y + SpinnerRadius * 4f * scale),
                SpinnerRadius * scale, kit.Ui.Accent, frame.Opacity, frame.DrawList);
            return;
        }

        var found = results;
        if (found.Length == 0)
        {
            var mutedInk = Palette.WithAlpha(ink, ink.W * MutedAlpha);
            var top = rows.Min.Y + Metrics.Space.Xxl * scale;
            var titleBottom = Typography.DrawWrappedCentered(frame.DrawList,
                Loc.T(searched ? L.Music.NoResults : L.Music.Library.AddSongsEmptyTitle), TextStyles.Headline, ink,
                new Vector2(rows.Center.X, top), rows.Width - MusicUi.Inset * 2f * scale);
            Typography.DrawWrappedCentered(frame.DrawList,
                Loc.T(searched ? L.Music.NoResultsSub : L.Music.Library.AddSongsEmptySub), TextStyles.Subheadline,
                mutedInk, new Vector2(rows.Center.X, titleBottom + Metrics.Space.Xs * scale),
                rows.Width - MusicUi.Inset * 2f * scale);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(rows))
        {
            var drawList = ImGui.GetWindowDrawList();
            for (var index = 0; index < found.Length; index++)
            {
                if (DrawResultRow(drawList, kit, found[index], ink, frame.Interactive))
                {
                    Toggle(kit.Library, found[index]);
                }
            }
        }
    }

    private bool DrawResultRow(ImDrawListPtr drawList, MusicKit kit, in Song song, Vector4 ink, bool interactive)
    {
        var scale = UiScale.Current;
        var height = RowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var hover = Palette.WithAlpha(ink, HoverAlpha);
        var cell = FeedCell.Begin(drawList, height, hover, interactive);
        var inset = MusicUi.Inset * scale;
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        ArtworkTile.Draw(drawList, kit.Images, artMin, side, song.ThumbnailUrl, song.Title);
        var added = kit.Library.PlaylistContains(playlistId, song.VideoId);
        var stateBox = Metrics.Size.TapTarget * scale * 0.5f;
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, cell.Bounds.Max.X - inset - stateBox - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = cell.Bounds.Min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(song.Title, textWidth,
            TextStyles.Body), ink, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(song.Author, textWidth, TextStyles.Subheadline),
            Palette.WithAlpha(ink, ink.W * MutedAlpha), TextStyles.Subheadline);
        var glyph = added ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.PlusCircle;
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Max.X - inset - stateBox * 0.5f,
            cell.Bounds.Min.Y + height * 0.5f), IconGlyph.Of(glyph), kit.Ui.Accent, StateGlyphScale);
        FeedCell.End(drawList, cell, hover, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, hover);
        return cell.Tapped;
    }

    private void Toggle(LibraryStore library, in Song song)
    {
        if (library.PlaylistContains(playlistId, song.VideoId))
        {
            library.RemoveFromPlaylist(playlistId, song.VideoId);
            return;
        }

        library.AddToPlaylist(playlistId, song);
    }

    private void BeginSearch(string text)
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        searching = true;
        searched = true;
        results = Array.Empty<Song>();
        _ = SearchAsync(text, fetch.Token);
    }

    private async Task SearchAsync(string text, CancellationToken token)
    {
        var found = await search.SearchAsync(text, SongSearchScope.Songs, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        results = found;
        searching = false;
    }
}
