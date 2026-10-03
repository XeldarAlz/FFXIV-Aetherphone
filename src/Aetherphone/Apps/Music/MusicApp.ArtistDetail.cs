using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float ArtistHeroFraction = 0.42f;
    private const float ArtistHeroMax = 168f;
    private const float FollowPillWidth = 132f;
    private const float FollowPillHeight = 34f;
    private const float ArtistSpinnerRadius = 11f;
    private const int ArtistTopCount = 10;
    private const string ArtistTopContextPrefix = "artist.top.";
    private const string ArtistLibraryContextPrefix = "artist.library.";

    private string artistChannel = "\0";
    private string artistName = string.Empty;
    private string artistTopContext = string.Empty;
    private string artistLibraryContext = string.Empty;
    private string artistThumbnail = string.Empty;
    private Song[] artistLibrarySongs = Array.Empty<Song>();
    private Song[] artistAllSongs = Array.Empty<Song>();
    private Song[] artistTopApplied = Array.Empty<Song>();
    private volatile Song[] artistTopSongs = Array.Empty<Song>();
    private volatile bool artistTopLoading;
    private int artistLibraryVersion = -1;
    private CancellationTokenSource? artistTopFetch;

    private void DrawArtistDetail(in PhoneContext context, in MusicRoute route)
    {
        EnsureArtist(route.Key, route.Label);
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            var textWidth = MathF.Max(1f, width - MusicUi.Inset * 2f * scale);
            DrawArtistHeader(width, textWidth, scale);
            LibraryKit.Gap(Metrics.Space.Lg);
            var (play, shuffle) = LibraryKit.PlayShuffleRow(ui, Loc.T(L.Music.Library.Play), Loc.T(L.Music.Shuffle),
                artistAllSongs.Length > 0);
            if (play)
            {
                playback.PlaySongs(artistAllSongs, 0, artistTopContext, artistName);
            }
            else if (shuffle)
            {
                playback.PlaySongsShuffled(artistAllSongs, artistTopContext, artistName);
            }

            SectionHeader.Draw(ui, Loc.T(L.Music.TopSongs), false);
            DrawArtistTopSongs(width, textWidth, scale);
            if (artistLibrarySongs.Length > 0)
            {
                SectionHeader.Draw(ui, Loc.T(L.Music.Library.InYourLibrary), false);
                DrawSongRows(artistLibrarySongs, artistLibraryContext, artistName);
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, artistName);
    }

    private void EnsureArtist(string channelId, string name)
    {
        if (!string.Equals(artistChannel, channelId, StringComparison.Ordinal) ||
            !string.Equals(artistName, name, StringComparison.Ordinal))
        {
            artistChannel = channelId;
            artistName = name;
            var key = channelId.Length > 0 ? channelId : name;
            artistTopContext = ArtistTopContextPrefix + key;
            artistLibraryContext = ArtistLibraryContextPrefix + key;
            artistLibraryVersion = -1;
            artistTopApplied = Array.Empty<Song>();
            artistTopSongs = Array.Empty<Song>();
            StartArtistTopSongs(channelId, name);
        }

        var top = artistTopSongs;
        if (artistLibraryVersion == library.Version && ReferenceEquals(top, artistTopApplied))
        {
            return;
        }

        artistLibraryVersion = library.Version;
        artistTopApplied = top;
        artistLibrarySongs = LibraryArtists.Filter(library.LibrarySongs(), channelId, name);
        artistAllSongs = MergeArtistSongs(top, artistLibrarySongs);
        artistThumbnail = ArtistThumbnail(channelId, top);
    }

    private static Song[] MergeArtistSongs(Song[] top, Song[] owned)
    {
        var merged = new List<Song>(top.Length + owned.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < top.Length; index++)
        {
            if (seen.Add(top[index].VideoId))
            {
                merged.Add(top[index]);
            }
        }

        for (var index = 0; index < owned.Length; index++)
        {
            if (seen.Add(owned[index].VideoId))
            {
                merged.Add(owned[index]);
            }
        }

        return merged.ToArray();
    }

    private string ArtistThumbnail(string channelId, Song[] top)
    {
        var artists = library.Artists;
        for (var index = 0; index < artists.Count; index++)
        {
            if (channelId.Length > 0 && string.Equals(artists[index].ChannelId, channelId, StringComparison.Ordinal) &&
                artists[index].ThumbnailUrl.Length > 0)
            {
                return artists[index].ThumbnailUrl;
            }
        }

        if (artistLibrarySongs.Length > 0)
        {
            return artistLibrarySongs[0].ThumbnailUrl;
        }

        return top.Length > 0 ? top[0].ThumbnailUrl : string.Empty;
    }

    private void StartArtistTopSongs(string channelId, string name)
    {
        artistTopFetch?.Cancel();
        artistTopFetch?.Dispose();
        artistTopFetch = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            artistTopLoading = false;
            return;
        }

        artistTopFetch = new CancellationTokenSource();
        artistTopLoading = true;
        _ = LoadArtistTopSongsAsync(channelId, name, artistTopFetch.Token);
    }

    private async Task LoadArtistTopSongsAsync(string channelId, string name, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(name, SongSearchScope.Songs, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        var matched = new List<Song>(ArtistTopCount);
        for (var index = 0; index < found.Length && matched.Count < ArtistTopCount; index++)
        {
            var song = found[index];
            var belongs = channelId.Length > 0
                ? string.Equals(song.ChannelId, channelId, StringComparison.Ordinal)
                : string.Equals(song.Author, name, StringComparison.OrdinalIgnoreCase);
            if (belongs)
            {
                matched.Add(song);
            }
        }

        artistTopSongs = matched.ToArray();
        artistTopLoading = false;
    }

    private void DrawArtistHeader(float width, float textWidth, float scale)
    {
        LibraryKit.Gap(Metrics.Space.Md);
        var origin = ImGui.GetCursorScreenPos();
        var radius = MathF.Min(width * ArtistHeroFraction, ArtistHeroMax * scale) * 0.5f;
        LibraryArt.DrawCircle(ImGui.GetWindowDrawList(), images, new Vector2(origin.X + width * 0.5f, origin.Y + radius),
            radius, artistThumbnail, artistName);
        ImGui.Dummy(new Vector2(width, radius * 2f));
        LibraryKit.Gap(Metrics.Space.Md);
        LibraryKit.CenteredText(artistName, TextStyles.Title1, ui.TitleInk, textWidth);
        if (artistChannel.Length == 0)
        {
            return;
        }

        LibraryKit.Gap(Metrics.Space.Sm);
        var pillOrigin = ImGui.GetCursorScreenPos();
        var pillWidth = FollowPillWidth * scale;
        var rect = new Rect(new Vector2(pillOrigin.X + (width - pillWidth) * 0.5f, pillOrigin.Y),
            new Vector2(pillOrigin.X + (width + pillWidth) * 0.5f, pillOrigin.Y + FollowPillHeight * scale));
        var follows = library.FollowsArtist(artistChannel);
        var tapped = ui.PillButton(rect, Loc.T(follows ? L.Music.FollowingStation : L.Music.FollowStation), !follows,
            "##musicFollowArtist");
        ImGui.SetCursorScreenPos(pillOrigin);
        ImGui.Dummy(new Vector2(width, rect.Height));
        if (tapped)
        {
            library.SetFollowArtist(artistChannel, artistName, artistThumbnail, !follows);
        }
    }

    private void DrawArtistTopSongs(float width, float textWidth, float scale)
    {
        if (artistTopLoading)
        {
            var origin = ImGui.GetCursorScreenPos();
            var radius = ArtistSpinnerRadius * scale;
            LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + radius * 2f), radius, ui.Accent, 1f,
                ImGui.GetWindowDrawList());
            ImGui.Dummy(new Vector2(width, radius * 4f));
            return;
        }

        if (artistTopApplied.Length == 0)
        {
            LibraryKit.CenteredText(Loc.T(L.Music.Library.NoTopSongs), TextStyles.Footnote, ui.MutedInk, textWidth);
            return;
        }

        DrawSongRows(artistTopApplied, artistTopContext, artistName);
    }
}
