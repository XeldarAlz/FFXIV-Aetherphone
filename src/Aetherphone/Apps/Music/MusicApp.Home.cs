using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int HomeRecentCount = 20;
    private const int HomeMostPlayedCount = 25;
    private const int HomeFavouritesSize = 50;
    private const int HomeDiscoverySeeds = 3;
    private const int HomeRediscoverCount = 20;
    private const int HomePickSlots = 3;
    private const int HomeMixSlots = 3;
    private const float MixTitleInset = 12f;
    private const float MixGlyphScale = 1.4f;
    private const float MixGlyphInset = 22f;
    private const float PickScrimAlpha = 0.62f;
    private const float PickBadgeRadius = 13f;
    private const float PickBadgeGlyphScale = 0.62f;
    private const float WelcomeGap = 6f;
    private const string HomeRecentContext = "home.recent";
    private const string HomeRediscoverContext = "home.rediscover";
    private const string HomeLovedContext = "home.loved";
    private const string HomeStationPrefix = "station.";

    private readonly NavBarButton[] homeButtons = new NavBarButton[1];
    private readonly ShelfRail homeRecentRail = new();
    private readonly ShelfRail homePicksRail = new();
    private readonly ShelfRail homeMixesRail = new();
    private readonly ShelfRail homeLiveRail = new();
    private readonly ShelfRail homeRediscoverRail = new();
    private readonly ShelfRail[] homeStarterRails = [new(), new(), new()];
    private readonly HomePick[] homePicks = new HomePick[HomePickSlots];
    private readonly HomeMix[] homeMixes = new HomeMix[HomeMixSlots];
    private readonly List<CommunityStationDto> homeLiveBuffer = new();
    private Song[] homeRecent = Array.Empty<Song>();
    private Song[] homeMostPlayed = Array.Empty<Song>();
    private Song[] homeLoved = Array.Empty<Song>();
    private Song[] homeFavourites = Array.Empty<Song>();
    private Song[] homeRediscover = Array.Empty<Song>();
    private CommunityStationDto[] homeLive = Array.Empty<CommunityStationDto>();
    private CommunityStationDto[]? homeLiveSource;
    private Song homeStationSeed;
    private Song homeBecauseSeed;
    private CatalogRequest homeDiscoveryRequest;
    private CatalogRequest homeBecauseRequest;
    private int homePickCount;
    private int homeMixCount;
    private int homeDataVersion = -1;
    private long homeDataDay = -1;
    private string homeBecauseTitle = string.Empty;
    private string homeBecauseLanguage = string.Empty;

    private enum HomePick : byte
    {
        Station,
        Loved,
        Because,
    }

    private enum HomeMix : byte
    {
        Favourites,
        Discovery,
        Chill,
    }

    private bool HomeIsFresh => homeRecent.Length == 0 && homeLoved.Length == 0;

    private void DrawHome(in PhoneContext context)
    {
        var scale = UiScale.Current;
        EnsureHomeData();
        EnsureHomeLive();
        community.EnsureFresh(false);
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawListeningPrompt(scale);
            DrawPcMediaCard(scale);
            if (HomeIsFresh)
            {
                DrawHomeWelcome(scale);
            }
            else
            {
                DrawHomeTopPicks();
                DrawJamHomeCard(scale);
                DrawFriendsListening(scale);
                DrawHomeRecentShelf();
                DrawHomeMadeForYou();
            }

            DrawHomeLiveNow(scale);
            if (!HomeIsFresh)
            {
                DrawHomeRediscover();
                DrawHomeReplayCard(scale);
            }

            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        if (NativeFileDialog.RunsUnderWine)
        {
            EndPage(in frame, context, Loc.T(L.Music.TabHome));
            return;
        }

        homeButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Desktop), Loc.T(L.Music.PcMedia.Source));
        if (EndPage(in frame, context, Loc.T(L.Music.TabHome), homeButtons) == 0)
        {
            pcSourceMenu.Toggle(PcSourceMenuId, AppHeader.LargeTitleButtonRect(in frame, 0, homeButtons.Length));
        }
    }

    private void EnsureHomeData()
    {
        var day = MusicMixRules.Day(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (homeDataVersion == library.Version && homeDataDay == day)
        {
            return;
        }

        homeDataVersion = library.Version;
        homeDataDay = day;
        homeRecent = library.RecentlyPlayed(HomeRecentCount);
        homeMostPlayed = library.MostPlayed(HomeMostPlayedCount);
        homeLoved = library.LovedSongs();
        homeFavourites = MusicMixRules.Favourites(homeMostPlayed, homeLoved, HomeFavouritesSize);
        var rediscoverBefore = (day - MusicMixRules.RediscoverAgeDays) * MusicMixRules.SecondsPerDay;
        homeRediscover = library.MostPlayedBefore(HomeRediscoverCount, rediscoverBefore);
        homeStationSeed = homeMostPlayed.Length > 0 ? homeMostPlayed[0] : default;
        homeBecauseSeed = MusicMixRules.BecauseSeed(homeRecent, day);
        homeBecauseRequest = homeBecauseSeed.IsEmpty ? default : CatalogRequest.Mix(homeBecauseSeed.VideoId);
        homeBecauseTitle = string.Empty;
        var seeds = MusicMixRules.PickSeeds(homeLoved, homeMostPlayed, HomeDiscoverySeeds, day);
        homeDiscoveryRequest = seeds.Length == 0
            ? default
            : CatalogRequest.Discovery(MusicMixRules.Signature(seeds, day), seeds,
                MusicMixRules.KnownIds(homeLoved, homeMostPlayed, homeRecent));
        BuildHomeCards();
    }

    private void BuildHomeCards()
    {
        homePickCount = 0;
        if (!homeStationSeed.IsEmpty)
        {
            homePicks[homePickCount++] = HomePick.Station;
        }

        if (homeLoved.Length > 0)
        {
            homePicks[homePickCount++] = HomePick.Loved;
        }

        if (!homeBecauseSeed.IsEmpty)
        {
            homePicks[homePickCount++] = HomePick.Because;
        }

        homeMixCount = 0;
        if (homeFavourites.Length > 0)
        {
            homeMixes[homeMixCount++] = HomeMix.Favourites;
        }

        if (!homeDiscoveryRequest.IsEmpty)
        {
            homeMixes[homeMixCount++] = HomeMix.Discovery;
        }

        homeMixes[homeMixCount++] = HomeMix.Chill;
    }

    private void EnsureHomeLive()
    {
        var stations = community.Stations;
        if (ReferenceEquals(stations, homeLiveSource))
        {
            return;
        }

        homeLiveSource = stations;
        homeLiveBuffer.Clear();
        for (var index = 0; index < stations.Length; index++)
        {
            if (stations[index].IsLive)
            {
                homeLiveBuffer.Add(stations[index]);
            }
        }

        homeLive = homeLiveBuffer.ToArray();
    }

    private string BecauseTitle()
    {
        var language = Loc.Current.Code;
        if (homeBecauseTitle.Length > 0 && string.Equals(language, homeBecauseLanguage, StringComparison.Ordinal))
        {
            return homeBecauseTitle;
        }

        homeBecauseLanguage = language;
        homeBecauseTitle = string.Format(Loc.Culture, Loc.T(L.Music.Home.BecauseYouPlayed), homeBecauseSeed.Title);
        return homeBecauseTitle;
    }

    private void DrawHomeWelcome(float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var textWidth = MathF.Max(1f, width - inset * 2f);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y),
            Loc.T(L.Music.Home.WelcomeTitle), ui.TitleInk, TextStyles.Title2, textWidth);
        var subTop = origin.Y + titleHeight + WelcomeGap * scale;
        var subHeight = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, subTop),
            Loc.T(L.Music.Home.WelcomeSub), ui.MutedInk, TextStyles.Subheadline, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, subTop + subHeight - origin.Y));
        var starters = MusicCatalogShelves.Starter;
        for (var index = 0; index < starters.Length; index++)
        {
            DrawCatalogShelf(homeStarterRails[index], starters[index],
                index == 0 ? ArtworkTile.LargeCard : ArtworkTile.Standard);
        }

        DrawJamHomeCard(scale);
        DrawFriendsListening(scale);
    }

    private void DrawHomeTopPicks()
    {
        if (homePickCount == 0)
        {
            return;
        }

        var side = ArtworkTile.Side(ArtworkTile.LargeCard);
        homePicksRail.Begin(ui, Loc.T(L.Music.Home.TopPicks), false, homePickCount, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < homePickCount; index++)
        {
            if (!homePicksRail.Tile(index, out var tile))
            {
                continue;
            }

            var pick = homePicks[index];
            var hovered = homePicksRail.Hover(tile);
            DrawPickCard(drawList, pick, tile.Min, side, hovered);
            if (homePicksRail.Tapped(tile, hovered))
            {
                OpenPick(pick);
            }
        }

        homePicksRail.End();
    }

    private void DrawPickCard(ImDrawListPtr drawList, HomePick pick, Vector2 min, float side, bool hovered)
    {
        switch (pick)
        {
            case HomePick.Station:
                DrawSeededArt(drawList, min, side, homeStationSeed, FontAwesomeIcon.BroadcastTower,
                    Loc.T(L.Music.Home.StationKicker));
                ArtworkTile.DrawCaption(drawList, ui, min, side, Loc.T(L.Music.Home.YourStation),
                    Loc.T(L.Music.Home.YourStationSub));
                break;
            case HomePick.Loved:
                DrawMixArt(drawList, min, side, HomeLovedContext, Loc.T(L.Music.Home.LovedMix), FontAwesomeIcon.Heart);
                ArtworkTile.DrawCaption(drawList, ui, min, side, Loc.T(L.Music.Home.LovedMix),
                    Loc.T(L.Music.Home.LovedMixSub));
                break;
            default:
                DrawSeededArt(drawList, min, side, homeBecauseSeed, FontAwesomeIcon.Headphones,
                    Loc.T(L.Music.Home.MixKicker));
                ArtworkTile.DrawCaption(drawList, ui, min, side, BecauseTitle(), Loc.T(L.Music.Home.BecauseSub));
                break;
        }

        ArtworkTile.DrawPressed(drawList, min, side, hovered);
    }

    private void OpenPick(HomePick pick)
    {
        switch (pick)
        {
            case HomePick.Station:
                StartHomeStation(homeStationSeed);
                break;
            case HomePick.Loved:
                playback.PlaySongsShuffled(homeLoved, HomeLovedContext, Loc.T(L.Music.Home.LovedMix));
                break;
            default:
                Push(MusicRoute.Genre(MusicCatalogShelves.MixBecause, BecauseTitle()));
                break;
        }
    }

    private void StartHomeStation(in Song seed)
    {
        if (seed.IsEmpty)
        {
            return;
        }

        playback.SetAutoplay(true);
        playback.PlaySongs(new[] { seed }, 0, HomeStationPrefix + seed.VideoId,
            string.Format(Loc.Culture, Loc.T(L.Music.StationFor), seed.Title));
    }

    private void DrawHomeRecentShelf()
    {
        if (DrawSongShelf(homeRecentRail, Loc.T(L.Music.RecentlyPlayed), homeRecent, CatalogState.Ready,
                HomeRecentContext, ArtworkTile.Standard))
        {
            Push(MusicRoute.Songs(SongListKind.RecentlyPlayed));
        }
    }

    private void DrawHomeMadeForYou()
    {
        var mixCatalog = Catalog;
        if (!homeDiscoveryRequest.IsEmpty)
        {
            mixCatalog.Ensure(MusicCatalogShelves.MixDiscovery, homeDiscoveryRequest);
        }

        if (!homeBecauseRequest.IsEmpty)
        {
            mixCatalog.Ensure(MusicCatalogShelves.MixBecause, homeBecauseRequest);
        }

        var chill = MusicCatalogShelves.Chill;
        mixCatalog.Ensure(chill.Key, chill.Request);
        var side = ArtworkTile.Side(ArtworkTile.Standard);
        homeMixesRail.Begin(ui, Loc.T(L.Music.MadeForYou), false, homeMixCount, side, ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < homeMixCount; index++)
        {
            if (!homeMixesRail.Tile(index, out var tile))
            {
                continue;
            }

            var mix = homeMixes[index];
            var hovered = homeMixesRail.Hover(tile);
            DrawMixCard(drawList, mix, tile.Min, side);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            if (homeMixesRail.Tapped(tile, hovered))
            {
                OpenMix(mix);
            }
        }

        homeMixesRail.End();
    }

    private void DrawMixCard(ImDrawListPtr drawList, HomeMix mix, Vector2 min, float side)
    {
        switch (mix)
        {
            case HomeMix.Favourites:
                DrawMixArt(drawList, min, side, MusicCatalogShelves.MixFavourites,
                    Loc.T(L.Music.Home.FavouritesMix), FontAwesomeIcon.Star);
                ArtworkTile.DrawCaption(drawList, ui, min, side, Loc.T(L.Music.Home.FavouritesMix),
                    Loc.T(L.Music.Home.FavouritesMixSub));
                break;
            case HomeMix.Discovery:
                DrawMixArt(drawList, min, side, MusicCatalogShelves.MixDiscovery,
                    Loc.T(L.Music.Home.DiscoveryMix), FontAwesomeIcon.Compass);
                ArtworkTile.DrawCaption(drawList, ui, min, side, Loc.T(L.Music.Home.DiscoveryMix),
                    Loc.T(L.Music.Home.DiscoveryMixSub));
                break;
            default:
                DrawMixArt(drawList, min, side, MusicCatalogShelves.Chill.Key, Loc.T(L.Music.Home.ChillMix),
                    FontAwesomeIcon.Moon);
                ArtworkTile.DrawCaption(drawList, ui, min, side, Loc.T(L.Music.Home.ChillMix),
                    Loc.T(L.Music.Home.ChillMixSub));
                break;
        }
    }

    private void OpenMix(HomeMix mix)
    {
        switch (mix)
        {
            case HomeMix.Favourites:
                Push(MusicRoute.Genre(MusicCatalogShelves.MixFavourites, Loc.T(L.Music.Home.FavouritesMix)));
                break;
            case HomeMix.Discovery:
                Push(MusicRoute.Genre(MusicCatalogShelves.MixDiscovery, Loc.T(L.Music.Home.DiscoveryMix)));
                break;
            default:
                Push(MusicRoute.Genre(MusicCatalogShelves.Chill.Key, Loc.T(L.Music.Home.ChillMix)));
                break;
        }
    }

    private void DrawMixArt(ImDrawListPtr drawList, Vector2 min, float side, string seed, string title,
        FontAwesomeIcon icon)
    {
        var scale = UiScale.Current;
        var max = min + new Vector2(side, side);
        var radius = side * ArtworkTile.TileRadiusFraction;
        var swatch = ArtGradient.FromName(seed);
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(swatch.Top),
            ImGui.GetColorU32(swatch.Bottom));
        var glyphInset = MixGlyphInset * scale;
        AppSkin.Icon(drawList, new Vector2(max.X - glyphInset, min.Y + glyphInset), IconGlyph.Of(icon), BrowseTileInk,
            MixGlyphScale);
        var pad = MixTitleInset * scale;
        var lines = Typography.WrapText(title, TextStyles.Title3, side - pad * 2f);
        var lineHeight = Typography.LineHeight(TextStyles.Title3);
        var top = max.Y - pad - lines.Length * lineHeight;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(min.X + pad, top + lineIndex * lineHeight), lines[lineIndex],
                BrowseTileInk, TextStyles.Title3);
        }
    }

    private void DrawSeededArt(ImDrawListPtr drawList, Vector2 min, float side, in Song seed, FontAwesomeIcon icon,
        string kicker)
    {
        var scale = UiScale.Current;
        var max = min + new Vector2(side, side);
        var radius = side * ArtworkTile.TileRadiusFraction;
        ArtworkTile.Draw(drawList, images, min, side, seed.ThumbnailUrl, seed.Title);
        var scrimTop = new Vector2(min.X, min.Y + side * 0.45f);
        Squircle.FillVerticalGradient(drawList, scrimTop, max, radius, ImGui.GetColorU32(Vector4.Zero),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, PickScrimAlpha)));
        var pad = MixTitleInset * scale;
        var badgeRadius = PickBadgeRadius * scale;
        var badgeCenter = new Vector2(min.X + pad + badgeRadius, max.Y - pad - badgeRadius);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(ui.Accent), 24);
        AppSkin.Icon(drawList, badgeCenter, IconGlyph.Of(icon), BrowseTileInk, PickBadgeGlyphScale);
        var kickerLeft = badgeCenter.X + badgeRadius + Metrics.Space.Xs * scale;
        var kickerHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var fitted = Typography.FitText(kicker, max.X - pad - kickerLeft, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(kickerLeft, badgeCenter.Y - kickerHeight * 0.5f), fitted, BrowseTileInk,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawHomeLiveNow(float scale)
    {
        if (homeLive.Length == 0)
        {
            return;
        }

        var side = ArtworkTile.Side(ArtworkTile.Standard);
        homeLiveRail.Begin(ui, Loc.T(L.Music.Home.LiveNow), false, homeLive.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Sm * scale;
        var liveLabel = Loc.T(L.Music.LiveBadge);
        for (var index = 0; index < homeLive.Length; index++)
        {
            if (!homeLiveRail.Tile(index, out var tile))
            {
                continue;
            }

            var station = homeLive[index];
            var hovered = homeLiveRail.Hover(tile);
            var artMax = tile.Min + new Vector2(side, side);
            DrawStationArt(drawList, tile.Min, artMax, station, side * ArtworkTile.TileRadiusFraction);
            LivePill.Draw(drawList, tile.Min + new Vector2(pad, pad), liveLabel, ui.Theme.Danger, clock, scale);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            var subtitle = station.NowPlaying.Length > 0 ? station.NowPlaying : station.Description;
            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, station.Name, subtitle,
                IsCurrentCommunityStation(station));
            if (homeLiveRail.Tapped(tile, hovered))
            {
                OpenStationPage(station);
            }
        }

        homeLiveRail.End();
    }

    private void DrawHomeRediscover()
    {
        DrawSongShelf(homeRediscoverRail, Loc.T(L.Music.Home.Rediscover), homeRediscover, CatalogState.Ready,
            HomeRediscoverContext, ArtworkTile.Standard, false);
    }
}
