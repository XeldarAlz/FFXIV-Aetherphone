using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float StationSearchHeight = 50f;
    private const float ScopeRowHeight = 42f;
    private const float StationRowHeight = 60f;
    private const float CategoryTileHeight = 64f;
    private const float CategoryGap = 12f;
    private const float FacetRowHeight = 44f;
    private const string RadioSortMenuId = "music.radioSort";

    private static readonly Dictionary<int, string> FacetCountCache = new();
    private static readonly Dictionary<(string, int, string), string> StationSubtitleCache = new();

    private readonly DropdownMenu radioSortMenu = new();
    private int categoryIndex = -1;
    private RadioStation[] stations = Array.Empty<RadioStation>();
    private RadioStation[] favoriteRadioStations = Array.Empty<RadioStation>();
    private volatile bool loading;
    private CancellationTokenSource? fetch;
    private string radioSearchDraft = string.Empty;
    private string radioQuery = string.Empty;
    private bool focusRadioSearch;
    private int stationOffset;
    private volatile bool stationHasMore;
    private volatile bool loadingMore;
    private RadioOrder radioOrder = RadioOrder.Popular;
    private string radioCountryCode = string.Empty;
    private string radioCountryName = string.Empty;
    private string radioLanguage = string.Empty;
    private string radioLanguageName = string.Empty;
    private RadioFacet[] radioCountries = Array.Empty<RadioFacet>();
    private RadioFacet[] radioLanguages = Array.Empty<RadioFacet>();
    private volatile bool facetsLoading;
    private CancellationTokenSource? facetFetch;
    private string facetSearchDraft = string.Empty;

    private void DrawWorldRadioShelf(float scale)
    {
        if (SectionHeader.Draw(ui, Loc.T(L.Music.WorldRadio), true, 0f))
        {
            OpenRadioSearch();
        }

        DrawCategoryGrid(scale, ScrollLayout.StableContentWidth());
    }

    private void DrawCategoryGrid(float scale, float available)
    {
        var categories = RadioService.Categories;
        var gap = CategoryGap * scale;
        var tileWidth = (available - gap) * 0.5f;
        var tileHeight = CategoryTileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (categories.Length + 1) / 2;
        UiAnchors.Report("music.categories",
            new Rect(origin, origin + new Vector2(available, rows * tileHeight + (rows - 1) * gap)));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < categories.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var rounding = 10f * scale;
            var hovered = UiInteract.Hover(min, max);
            var seed = ArtGradient.Seed(categories[index].Tag);
            drawList.AddImageRounded(artwork.Handle(seed), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + tileHeight * 0.7f), 0x59000000u,
                0x59000000u, 0u, 0u);
            if (hovered)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), rounding);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var label = CatalogLabels.RadioCategory(categories[index].Display);
            Marquee.DrawLeft(new MarqueeId("music.categoryTile.", categories[index].Tag), label, min.X + 12f * scale,
                min.Y + 10f * scale, tileWidth - 24f * scale, TextStyles.SubheadlineEmphasized,
                new Vector4(1f, 1f, 1f, 1f), hovered);
            if (UiInteract.Click(min, max, hovered))
            {
                OpenCategory(index);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * tileHeight + (rows - 1) * gap));
    }

    private void DrawFavoriteRadioStationsSection(float scale)
    {
        if (favoriteRadioStations.Length == 0)
        {
            return;
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.YourStations), false, 0f);
        for (var index = 0; index < favoriteRadioStations.Length; index++)
        {
            DrawStationRow(scale, favoriteRadioStations[index], index, true, 6f);
        }
    }

    private void DrawStations(in PhoneContext context)
    {
        var scale = UiScale.Current;
        radioSortMenu.Gate();
        var frame = BeginPage(context);
        var body = frame.Body;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawStationSearch(scale);
            DrawRadioFilterChips(scale);
            var placeholderTop = ImGui.GetCursorScreenPos().Y;
            var placeholder = new Rect(new Vector2(body.Min.X, placeholderTop), Unobstructed(body).Max);
            if (loading)
            {
                LoadingPulse.Draw(placeholder.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Music.TuningIn));
            }
            else if (stations.Length == 0)
            {
                DrawStationsPlaceholder(placeholder, scale);
            }
            else
            {
                DrawStationList(scale, body);
            }
        }

        EndPage(in frame, context, StationsTitle());
        DrawRadioSortMenu(context.Content);
    }

    private void DrawStationSearch(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var bar = new Rect(origin, origin + new Vector2(width, StationSearchHeight * scale));
        if (focusRadioSearch)
        {
            focusRadioSearch = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SearchField.DrawSubmit(bar, "##radioSearch", Loc.T(L.Music.SearchStations),
            ref radioSearchDraft, ui.Palette, 80, MusicUi.Inset);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bar.Height));
        if (submitted && !string.IsNullOrWhiteSpace(radioSearchDraft))
        {
            BeginRadioSearch(radioSearchDraft);
        }
    }

    private void DrawStationList(float scale, Rect body)
    {
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        for (var index = 0; index < stations.Length; index++)
        {
            DrawStationRow(scale, stations[index], index, false, FeedCell.PadX);
        }

        if (loadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (InfiniteScroll.ReachedBottom() && stationHasMore && !loadingMore)
        {
            LoadMoreStations();
        }
    }

    private void DrawStationsPlaceholder(Rect body, float scale)
    {
        if (categoryIndex >= 0 || !string.IsNullOrEmpty(radioQuery) || !CurrentRadioFilter().IsDefault)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Music.NoStations), ui.MutedInk, TextStyles.Callout);
            return;
        }

        var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
        Typography.DrawCentered(center, Loc.T(L.Music.RadioSearchTitle), ui.TitleInk, TextStyles.Title3);
        var maxWidth = body.Width - 48f * scale;
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale), Loc.T(L.Music.RadioSearchSub),
            ui.MutedInk, TextStyles.Subheadline, maxWidth);
    }

    private void DrawStationRow(float scale, RadioStation station, int index, bool fromFavorites, float sideInset)
    {
        var rowHeight = StationRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var inset = sideInset * scale;
        var current = IsCurrentStation(station);
        var artSize = 44f * scale;
        var artMin = new Vector2(min.X + inset, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        drawList.AddImageRounded(artwork.HandleForName(station.Name), artMin, artMax, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, 8f * scale, ImDrawFlags.RoundCornersAll);
        var trailing = inset + (current ? 44f : 27f) * scale;
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - trailing - textLeft;
        var stationNameY = min.Y + 10f * scale;
        var stationNameSize = Typography.Measure(station.Name, TextStyles.BodyEmphasized);
        var stationNameHovering = UiInteract.Hover(new Vector2(textLeft, stationNameY),
            new Vector2(textLeft + textWidth, stationNameY + stationNameSize.Y));
        Marquee.DrawLeft(new MarqueeId("music.stationRow.name.", station.StreamUrl), station.Name, textLeft,
            stationNameY, textWidth, TextStyles.BodyEmphasized, current ? ui.Accent : ui.TitleInk,
            stationNameHovering);
        var stationSub = StationSubtitle(station);
        var stationSubY = min.Y + 34f * scale;
        var stationSubSize = Typography.Measure(stationSub, TextStyles.Caption1);
        var stationSubHovering = UiInteract.Hover(new Vector2(textLeft, stationSubY),
            new Vector2(textLeft + textWidth, stationSubY + stationSubSize.Y));
        Marquee.DrawLeft(new MarqueeId("music.stationRow.subtitle.", station.StreamUrl), stationSub, textLeft,
            stationSubY, textWidth, TextStyles.Caption1, ui.MutedInk, stationSubHovering);
        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - inset - 12f * scale, min.Y + rowHeight * 0.5f), scale,
                17f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
        }

        var isFavoriteStation = Array.IndexOf(favoriteRadioStations, station) >= 0;
        if (isFavoriteStation || cell.Hovered)
        {
            var tooltip = Loc.T(isFavoriteStation ? L.Music.RemoveFavoriteStation : L.Music.AddFavoriteStation);
            var starX = max.X - inset - (current ? 30f : 12f) * scale;
            var starCenter = new Vector2(starX, min.Y + rowHeight * 0.5f);
            if (ui.IconButton(starCenter, 14f * scale, IconGlyph.Of(Dalamud.Interface.FontAwesomeIcon.Star),
                    isFavoriteStation ? ui.Accent : ui.MutedInk, AppSkin.Transparent, 0.82f, tooltip))
            {
                ToggleFavoriteStation(station);
            }
        }

        if (cell.Tapped)
        {
            if (current)
            {
                playback.TogglePlayPause();
            }
            else if (fromFavorites)
            {
                PlayStationFromFavorites(index);
            }
            else
            {
                PlayStation(index);
            }
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawRadioFilterChips(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = ScopeRowHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var cursorX = origin.X + MusicUi.Inset * scale;
        var gap = 8f * scale;
        var sortStart = cursorX;
        if (ui.FlowChip(ref cursorX, centerY, gap, SortLabel(radioOrder), radioOrder != RadioOrder.Popular))
        {
            var anchor = new Rect(new Vector2(sortStart, centerY - 16f * scale),
                new Vector2(cursorX - gap, centerY + 16f * scale));
            radioSortMenu.Toggle(RadioSortMenuId, anchor);
        }

        var countryLabel = radioCountryName.Length > 0 ? radioCountryName : Loc.T(L.Music.FilterCountry);
        if (ui.FlowChip(ref cursorX, centerY, gap, countryLabel, radioCountryCode.Length > 0))
        {
            OpenFacetPicker(true);
        }

        var languageLabel = radioLanguageName.Length > 0 ? radioLanguageName : Loc.T(L.Music.FilterLanguage);
        if (ui.FlowChip(ref cursorX, centerY, gap, languageLabel, radioLanguage.Length > 0))
        {
            OpenFacetPicker(false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawRadioSortMenu(Rect content)
    {
        if (!radioSortMenu.IsOpenFor(RadioSortMenuId))
        {
            return;
        }

        ReadOnlySpan<DropdownMenu.Item> items =
        [
            new DropdownMenu.Item(Loc.T(L.Music.SortPopular), Selected: radioOrder == RadioOrder.Popular),
            new DropdownMenu.Item(Loc.T(L.Music.SortTrending), Selected: radioOrder == RadioOrder.Trending),
            new DropdownMenu.Item(Loc.T(L.Music.SortTopVoted), Selected: radioOrder == RadioOrder.TopVoted),
            new DropdownMenu.Item(Loc.T(L.Music.SortName), Selected: radioOrder == RadioOrder.Name),
            new DropdownMenu.Item(Loc.T(L.Music.SortBitrate), Selected: radioOrder == RadioOrder.Bitrate),
        ];
        var clicked = radioSortMenu.Draw(content, theme, items);
        if (clicked < 0)
        {
            return;
        }

        var next = (RadioOrder)clicked;
        if (next == radioOrder)
        {
            return;
        }

        radioOrder = next;
        RefetchRadio();
    }

    private static string SortLabel(RadioOrder order)
    {
        return order switch
        {
            RadioOrder.Trending => Loc.T(L.Music.SortTrending),
            RadioOrder.TopVoted => Loc.T(L.Music.SortTopVoted),
            RadioOrder.Name => Loc.T(L.Music.SortName),
            RadioOrder.Bitrate => Loc.T(L.Music.SortBitrate),
            _ => Loc.T(L.Music.SortPopular),
        };
    }

    private void DrawStationFilter(in PhoneContext context, in MusicRoute route)
    {
        var scale = UiScale.Current;
        var isCountry = route.Key == MusicRoute.CountryFilter;
        var frame = BeginPage(context);
        var body = frame.Body;
        var facets = isCountry ? radioCountries : radioLanguages;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bar = new Rect(origin, origin + new Vector2(width, StationSearchHeight * scale));
            SearchField.Draw(new Rect(new Vector2(bar.Min.X + MusicUi.Inset * scale, bar.Min.Y),
                new Vector2(bar.Max.X - MusicUi.Inset * scale, bar.Max.Y)), "##facetSearch", Loc.T(L.Common.Search),
                ref facetSearchDraft, ui.Palette, 40);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, bar.Height));
            if (facets.Length == 0)
            {
                var placeholder = new Rect(new Vector2(body.Min.X, bar.Max.Y), Unobstructed(body).Max);
                LoadingPulse.Draw(placeholder.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            }
            else
            {
                DrawFacetRows(scale, facets, isCountry);
            }
        }

        EndPage(in frame, context, TitleOf(route));
    }

    private void DrawFacetRows(float scale, RadioFacet[] facets, bool isCountry)
    {
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        var activeValue = isCountry ? radioCountryCode : radioLanguage;
        var anyLabel = Loc.T(isCountry ? L.Music.AllCountries : L.Music.AllLanguages);
        if (DrawFacetRow(scale, anyLabel, string.Empty, activeValue.Length == 0))
        {
            ApplyFacet(isCountry, default);
        }

        var draft = facetSearchDraft.Trim();
        for (var index = 0; index < facets.Length; index++)
        {
            var facet = facets[index];
            if (draft.Length > 0 && facet.Display.IndexOf(draft, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (DrawFacetRow(scale, facet.Display, CountText(facet.Count),
                    string.Equals(activeValue, facet.Value, StringComparison.OrdinalIgnoreCase)))
            {
                ApplyFacet(isCountry, facet);
            }
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void ApplyFacet(bool isCountry, RadioFacet facet)
    {
        if (isCountry)
        {
            radioCountryCode = facet.Value ?? string.Empty;
            radioCountryName = facet.Display ?? string.Empty;
        }
        else
        {
            radioLanguage = facet.Value ?? string.Empty;
            radioLanguageName = facet.Display ?? string.Empty;
        }

        Router.Pop();
        RefetchRadio();
    }

    private bool DrawFacetRow(float scale, string label, string count, bool selected)
    {
        var rowHeight = FacetRowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, rowHeight)))
        {
            ImGui.Dummy(new Vector2(width, rowHeight));
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var textLeft = min.X + MusicUi.Inset * scale;
        var centerY = min.Y + rowHeight * 0.5f;
        var countWidth = 0f;
        if (count.Length > 0)
        {
            var countSize = Typography.Measure(count, TextStyles.Caption1);
            countWidth = countSize.X + 12f * scale;
            Typography.Draw(drawList, new Vector2(max.X - MusicUi.Inset * scale - countSize.X,
                centerY - countSize.Y * 0.5f), count, ui.MutedInk, TextStyles.Caption1);
        }

        var labelWidth = MathF.Max(1f, max.X - textLeft - countWidth - MusicUi.Inset * scale);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Marquee.DrawLeft(new MarqueeId("music.facetRow.", label), label, textLeft, centerY - labelSize.Y * 0.5f,
            labelWidth, TextStyles.Body, selected ? ui.Accent : ui.TitleInk, cell.Hovered);
        FeedCell.End(drawList, cell, ui.Hairline);
        return cell.Tapped;
    }

    private static string CountText(int count)
    {
        if (FacetCountCache.TryGetValue(count, out var cached))
        {
            return cached;
        }

        var text = count.ToString(Loc.Culture);
        FacetCountCache[count] = text;
        return text;
    }

    private void OpenCategory(int index)
    {
        categoryIndex = index;
        radioQuery = string.Empty;
        radioSearchDraft = string.Empty;
        Push(MusicRoute.Of(MusicScreen.Stations));
        BeginFetch(RadioService.Categories[index].Tags);
    }

    private void OpenRadioSearch()
    {
        fetch?.Cancel();
        categoryIndex = -1;
        radioQuery = string.Empty;
        radioSearchDraft = string.Empty;
        stations = Array.Empty<RadioStation>();
        loading = false;
        ResetPaging();
        focusRadioSearch = true;
        Push(MusicRoute.Of(MusicScreen.Stations));
        if (!CurrentRadioFilter().IsDefault)
        {
            BeginRadioSearch(string.Empty);
        }
    }

    private void OpenFacetPicker(bool country)
    {
        facetSearchDraft = string.Empty;
        EnsureRadioFacets();
        Push(MusicRoute.StationFilter(country));
    }

    private RadioFilter CurrentRadioFilter() => new(radioCountryCode, radioLanguage, radioOrder);

    private void BeginFetch(string[] tags)
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        stations = Array.Empty<RadioStation>();
        ResetPaging();
        _ = LoadCategoryPageAsync(tags, CurrentRadioFilter(), 0, token);
    }

    private void BeginRadioSearch(string query)
    {
        var trimmed = query.Trim();
        var filter = CurrentRadioFilter();
        if (trimmed.Length == 0 && filter.IsDefault)
        {
            return;
        }

        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        categoryIndex = -1;
        radioQuery = trimmed;
        stations = Array.Empty<RadioStation>();
        ResetPaging();
        _ = LoadSearchPageAsync(radioQuery, filter, 0, token);
    }

    private void RefetchRadio()
    {
        if (categoryIndex >= 0)
        {
            BeginFetch(RadioService.Categories[categoryIndex].Tags);
            return;
        }

        if (radioQuery.Length > 0 || !CurrentRadioFilter().IsDefault)
        {
            BeginRadioSearch(radioQuery);
            return;
        }

        fetch?.Cancel();
        stations = Array.Empty<RadioStation>();
        loading = false;
        ResetPaging();
    }

    private void LoadMoreStations()
    {
        if (loading || loadingMore || !stationHasMore || fetch is null)
        {
            return;
        }

        var token = fetch.Token;
        var filter = CurrentRadioFilter();
        var offset = stationOffset + RadioService.PageSize;
        loadingMore = true;
        if (categoryIndex >= 0)
        {
            _ = LoadCategoryPageAsync(RadioService.Categories[categoryIndex].Tags, filter, offset, token);
        }
        else if (radioQuery.Length > 0 || !filter.IsDefault)
        {
            _ = LoadSearchPageAsync(radioQuery, filter, offset, token);
        }
        else
        {
            loadingMore = false;
        }
    }

    private async Task LoadCategoryPageAsync(string[] tags, RadioFilter filter, int offset, CancellationToken token)
    {
        var page = await radio.FetchStationsAsync(tags, filter, offset, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        ApplyPage(page, offset);
    }

    private async Task LoadSearchPageAsync(string query, RadioFilter filter, int offset, CancellationToken token)
    {
        var page = await radio.SearchStationsAsync(query, filter, offset, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        ApplyPage(page, offset);
    }

    private void EnsureRadioFacets()
    {
        if ((radioCountries.Length > 0 && radioLanguages.Length > 0) || facetsLoading)
        {
            return;
        }

        facetsLoading = true;
        facetFetch?.Cancel();
        facetFetch?.Dispose();
        facetFetch = new CancellationTokenSource();
        _ = LoadFacetsAsync(facetFetch.Token);
    }

    private async Task LoadFacetsAsync(CancellationToken token)
    {
        var countries = await radio.FetchCountriesAsync(token).ConfigureAwait(false);
        var languages = await radio.FetchLanguagesAsync(token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        radioCountries = countries;
        radioLanguages = languages;
        facetsLoading = false;
    }

    private void ApplyPage(RadioPage page, int offset)
    {
        var basis = offset == 0 ? Array.Empty<RadioStation>() : stations;
        stations = AppendDedup(basis, page.Stations);
        stationOffset = offset;
        stationHasMore = page.HasMore;
        loading = false;
        loadingMore = false;
    }

    private void ResetPaging()
    {
        stationOffset = 0;
        stationHasMore = false;
        loadingMore = false;
    }

    private static RadioStation[] AppendDedup(RadioStation[] existing, RadioStation[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var seen = new HashSet<string>(existing.Length + incoming.Length, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < existing.Length; index++)
        {
            seen.Add(existing[index].StreamUrl);
        }

        var list = new List<RadioStation>(existing.Length + incoming.Length);
        list.AddRange(existing);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (seen.Add(incoming[index].StreamUrl))
            {
                list.Add(incoming[index]);
            }
        }

        return list.ToArray();
    }

    private void PlayStation(int index)
    {
        radio.ReportClick(stations[index].Uuid);
        playback.PlayStations(stations, index);
    }

    private void PlayStationFromFavorites(int index)
    {
        radio.ReportClick(favoriteRadioStations[index].Uuid);
        playback.PlayStations(favoriteRadioStations, index);
    }

    private bool IsCurrentStation(RadioStation station)
    {
        return playback.RadioActive && playback.Radio.CurrentStation == station.Name;
    }

    private void ToggleFavoriteStation(RadioStation station)
    {
        if (string.IsNullOrEmpty(station.StreamUrl))
        {
            return;
        }

        var streamUrl = station.StreamUrl;
        var removed = configuration.RadioFavorites.RemoveAll(record => record.StreamUrl == streamUrl);
        if (removed == 0)
        {
            configuration.RadioFavorites.Add(RadioStationRecord.From(station));
        }

        configuration.Save();
        LoadFavoriteRadioStations();
    }

    private void LoadFavoriteRadioStations()
    {
        var records = configuration.RadioFavorites;
        var loaded = new RadioStation[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            loaded[index] = records[index].ToStation();
        }

        favoriteRadioStations = loaded;
    }

    private string CategoryTitle()
    {
        return categoryIndex >= 0
            ? CatalogLabels.RadioCategory(RadioService.Categories[categoryIndex].Display)
            : Loc.T(L.Music.WorldRadio);
    }

    private string StationsTitle()
    {
        if (categoryIndex >= 0)
        {
            return CategoryTitle();
        }

        return string.IsNullOrEmpty(radioQuery) ? Loc.T(L.Music.WorldRadio) : radioQuery;
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

    private void DisposeWorldRadio()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        facetFetch?.Cancel();
        facetFetch?.Dispose();
    }
}
