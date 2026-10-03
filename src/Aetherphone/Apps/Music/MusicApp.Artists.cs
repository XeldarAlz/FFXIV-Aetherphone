using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float ArtistRowArt = 48f;

    private ArtistEntry[] libraryArtists = Array.Empty<ArtistEntry>();
    private int libraryArtistsVersion = -1;

    private void DrawLibraryArtists(in PhoneContext context)
    {
        if (libraryArtistsVersion != library.Version)
        {
            libraryArtistsVersion = library.Version;
            libraryArtists = LibraryArtists.Build(library.Artists, library.Songs);
        }

        var scale = UiScale.Current;
        var frame = BeginPage(context);
        if (libraryArtists.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Microphone,
                Loc.T(L.Music.LibraryEmptyTitle), Loc.T(L.Music.LibraryEmptySub));
            EndPage(in frame, context, Loc.T(L.Music.LibraryArtists));
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            for (var index = 0; index < libraryArtists.Length; index++)
            {
                DrawArtistRow(scale, index);
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, Loc.T(L.Music.LibraryArtists));
    }

    private void DrawArtistRow(float scale, int index)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var artist = libraryArtists[index];
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var radius = ArtistRowArt * scale * 0.5f;
        var center = new Vector2(cell.Bounds.Min.X + inset + radius, cell.Bounds.Min.Y + height * 0.5f);
        LibraryArt.DrawCircle(drawList, images, center, radius, artist.ThumbnailUrl, artist.Name);
        var textLeft = center.X + radius + Metrics.Space.Md * scale;
        var subtitle = artist.SongCount > 0 ? MusicUi.SongCount(artist.SongCount) :
            artist.Followed ? Loc.T(L.Music.FollowingStation) : string.Empty;
        DrawRowText(drawList, cell.Bounds, textLeft, cell.Bounds.Max.X - inset * 2f, artist.Name, subtitle);
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Max.X - inset, center.Y),
            IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, LibraryGlyphScale * 0.7f);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        if (cell.Tapped)
        {
            Push(MusicRoute.Artist(artist.ChannelId, artist.Name));
        }
    }
}
