using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawBrowse(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        AppHeader.Draw(context, DisplayName);
        EnsureFeatured();
        var barRect = SearchBarRect(content, scale);
        UiAnchors.Report("music.search", barRect);
        if (DrawSearchBar(barRect, theme))
        {
            router.Push(View.Search);
            BeginSearch(searchDraft);
        }

        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            DrawRecentSection(theme, scale);
            DrawFeaturedSection(theme, scale);
            DrawSectionHeader(theme, scale, Loc.T(L.Music.RadioStations));
            DrawCategoryGrid(theme, scale);
            ImGui.Dummy(new Vector2(0f, 6f * scale));
        }

        DrawMiniPlayer(context, scale);
    }

    private static void DrawSectionHeader(PhoneTheme theme, float scale, string title)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(origin, title, theme.TextStrong, 1.15f);
        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private void DrawRecentSection(PhoneTheme theme, float scale)
    {
        var recents = history.Recent(RecentTiles);
        if (recents.Length == 0)
        {
            return;
        }

        DrawSectionHeader(theme, scale, Loc.T(L.Music.RecentlyPlayed));
        var gap = 8f * scale;
        var available = ImGui.GetContentRegionAvail().X;
        var cardWidth = (available - gap) * 0.5f;
        var cardHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (recents.Length + 1) / 2;
        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < recents.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (cardWidth + gap), origin.Y + row * (cardHeight + gap));
            var max = min + new Vector2(cardWidth, cardHeight);
            var rounding = 12f * scale;
            var song = recents[index];
            var playing = IsCurrentSong(song);
            var hovered = ImGui.IsMouseHoveringRect(min, max);
            var fill = Palette.WithAlpha(playing ? Accent : theme.TextStrong, playing ? 0.14f :
                hovered ? 0.10f : 0.05f);
            Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(fill));
            var artSize = cardHeight - 12f * scale;
            var artMin = new Vector2(min.X + 6f * scale, min.Y + 6f * scale);
            var artMax = artMin + new Vector2(artSize, artSize);
            DrawThumb(dl, artMin, artMax, song.ThumbnailUrl, song.Title, 8f * scale);
            var textLeft = artMax.X + 9f * scale;
            Typography.Draw(new Vector2(textLeft, min.Y + 11f * scale), Truncate(song.Title, 14),
                playing ? Accent : theme.TextStrong, 0.86f);
            Typography.Draw(new Vector2(textLeft, min.Y + 31f * scale), Truncate(song.Author, 16),
                Palette.WithAlpha(theme.TextStrong, 0.6f), 0.72f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    PlaySong(recents, index);
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * cardHeight + (rows - 1) * gap));
    }

    private void DrawFeaturedSection(PhoneTheme theme, float scale)
    {
        if (featured.Length == 0)
        {
            if (!featuredLoading)
            {
                return;
            }

            DrawSectionHeader(theme, scale, featuredTitle);
            var caption = ImGui.GetCursorScreenPos();
            Typography.Draw(caption, Loc.T(L.Common.Loading), theme.TextMuted, 0.84f);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 22f * scale));
            return;
        }

        DrawSectionHeader(theme, scale, featuredTitle);
        var gap = 10f * scale;
        var available = ImGui.GetContentRegionAvail().X;
        var cardWidth = (available - gap) * 0.5f;
        var artSize = cardWidth;
        var cardHeight = artSize + 42f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (featured.Length + 1) / 2;
        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < featured.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (cardWidth + gap), origin.Y + row * (cardHeight + gap));
            var artMin = min;
            var artMax = artMin + new Vector2(artSize, artSize);
            var rounding = 14f * scale;
            var song = featured[index];
            var playing = IsCurrentSong(song);
            var hovered = ImGui.IsMouseHoveringRect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
            dl.AddRectFilled(artMin + new Vector2(0f, 5f * scale), artMax + new Vector2(0f, 6f * scale),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)), rounding);
            DrawThumb(dl, artMin, artMax, song.ThumbnailUrl, song.Title, rounding);
            if (hovered || playing)
            {
                dl.AddRectFilled(artMin, artMax, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.18f : 0.30f)),
                    rounding);
                PlayBadge.Draw(dl, (artMin + artMax) * 0.5f, 16f * scale, Accent,
                    playing && playback.Songs.State == SongPlaybackState.Playing);
            }

            Typography.Draw(new Vector2(artMin.X, artMax.Y + 7f * scale), Truncate(song.Title, 16),
                playing ? Accent : theme.TextStrong, 0.86f);
            Typography.Draw(new Vector2(artMin.X, artMax.Y + 25f * scale), Truncate(song.Author, 18),
                Palette.WithAlpha(theme.TextStrong, 0.6f), 0.72f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    PlaySong(featured, index);
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * cardHeight + (rows - 1) * gap));
    }

    private void DrawCategoryGrid(PhoneTheme theme, float scale)
    {
        var categories = RadioService.Categories;
        var gap = 10f * scale;
        var available = ImGui.GetContentRegionAvail().X;
        var tileWidth = (available - gap) * 0.5f;
        var tileHeight = TileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (categories.Length + 1) / 2;
        UiAnchors.Report("music.categories",
            new Rect(origin, origin + new Vector2(available, rows * tileHeight + (rows - 1) * gap)));
        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < categories.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var rounding = 16f * scale;
            var hovered = ImGui.IsMouseHoveringRect(min, max);
            var seed = ArtGradient.Seed(categories[index].Tag);
            dl.AddImageRounded(artwork.Handle(seed), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - tileHeight * 0.6f), max, 0u, 0u, 0x66000000u,
                0x66000000u);
            if (hovered)
            {
                dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), rounding);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var label = CatalogLabels.RadioCategory(categories[index].Display);
            var labelPosition = new Vector2(min.X + 12f * scale, max.Y - 26f * scale);
            Typography.Draw(labelPosition + new Vector2(1f, 1f), label, new Vector4(0f, 0f, 0f, 0.5f), 1.0f);
            Typography.Draw(labelPosition, label, new Vector4(1f, 1f, 1f, 1f), 1.0f);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenCategory(index);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * tileHeight + (rows - 1) * gap + 8f * scale));
    }

    private void DrawStations(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        AppHeader.Draw(context, CategoryTitle(), GoToBrowse);
        var body = ScrollBody(content, scale);
        if (loading)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Music.TuningIn));
            DrawMiniPlayer(context, scale);
            return;
        }

        if (stations.Length == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Music.NoStations), theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < stations.Length; index++)
            {
                DrawStationRow(theme, scale, stations[index], index);
            }
        }

        DrawMiniPlayer(context, scale);
    }

    private void DrawStationRow(PhoneTheme theme, float scale, RadioStation station, int index)
    {
        var rowHeight = 68f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var dl = ImGui.GetWindowDrawList();
        var playing = IsCurrentStation(station);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered || playing)
        {
            Squircle.Fill(dl, min, max, 14f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(playing ? Accent : theme.TextStrong, playing ? 0.10f : 0.06f)));
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var discRadius = 25f * scale;
        var discCenter = new Vector2(min.X + 10f * scale + discRadius, min.Y + rowHeight * 0.5f);
        dl.AddCircleFilled(discCenter + new Vector2(0f, 2.5f * scale), discRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 48);
        ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(station.Name), 1f);
        var trailing = playing ? 26f * scale : 12f * scale;
        var textLeft = discCenter.X + discRadius + 14f * scale;
        var nameColor = playing ? Accent : theme.TextStrong;
        Typography.Draw(new Vector2(textLeft, min.Y + 14f * scale), Truncate(station.Name, 22), nameColor, 1.2f);
        Typography.Draw(new Vector2(textLeft, min.Y + 41f * scale), StationSubtitle(station),
            Palette.WithAlpha(theme.TextStrong, 0.62f), 0.8f);
        if (playing)
        {
            Equalizer.Draw(dl, new Vector2(max.X - trailing, discCenter.Y), scale, 17f * scale, clock, Accent, 1f,
                playback.Radio.State == RadioPlaybackState.Playing);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            playback.PlayStations(stations, index);
        }
    }

    private string CategoryTitle()
    {
        return categoryIndex >= 0
            ? CatalogLabels.RadioCategory(RadioService.Categories[categoryIndex].Display)
            : DisplayName;
    }

    private static string StationSubtitle(RadioStation station)
    {
        var key = (Loc.Current.Code, station.Bitrate, station.Country);
        if (StationSubtitleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitrate = station.Bitrate > 0 ? $"{station.Bitrate}kbps" : Loc.T(L.Music.LiveLower);
        var subtitle = string.IsNullOrEmpty(station.Country) ? bitrate : $"{bitrate} · {station.Country}";
        StationSubtitleCache[key] = subtitle;
        return subtitle;
    }
}
