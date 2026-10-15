using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp : IResumableApp, ISpotlightVenues
{
    private const int PageSize = 30;
    private const int TabCount = 4;

    private static readonly SocialInk Ink = new(AppPalettes.Venues);

    public string Id => "venues";
    public string DisplayName => Loc.T(L.Apps.Venues);
    public string Glyph => "V";
    public int BadgeCount => 0;

    private readonly VenuesService venues;
    private readonly RemoteImageCache images;
    private readonly ArtworkCache artwork;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly AppSkin ui = new(AppPalettes.Venues);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] rootButtons = new NavBarButton[1];
    private readonly ViewRouter<VenueRoute> router;
    private readonly RouterDraw<VenueRoute> drawView;
    private readonly Action back;
    private readonly Action refreshAction;
    private readonly VenueSections sections = new();
    private readonly VenueQuery listQuery = new();
    private readonly VenueQuery searchQuery = new();
    private readonly VenueLabelCache labels = new();
    private readonly VenueTextList featuredText = new("venues.featured.");
    private readonly VenueTextList liveText = new("venues.live.");
    private readonly VenueTextList laterText = new("venues.later.");
    private readonly VenueTextList nearText = new("venues.near.");
    private readonly VenueTextList recentText = new("venues.recent.");
    private readonly VenueTextList eventsText = new("venues.events.", true);
    private readonly VenueTextList savedText = new("venues.saved.");
    private readonly VenueTextList listText = new("venues.list.");
    private readonly VenueTextList searchText = new("venues.search.");
    private readonly List<string> selectedTags = new();
    private readonly Dictionary<string, string> venueLanguages = new(StringComparer.Ordinal);
    private ResolvedScope resolvedScope;
    private int resolvedScopeFrame = -1;
    private VenueTab activeTab = VenueTab.Discover;
    private string search = string.Empty;
    private int favoritesStamp;
    private int recentsStamp;
    private int tagsStamp;
    private int visibleCards = PageSize;
    private string pendingVenueId = string.Empty;
    private bool railOwnsPointer;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private CachedText filtersLabel;

    public VenuesApp(VenuesService venues, RemoteImageCache images, ArtworkCache artwork, GameData gameData,
        Configuration configuration, ConfirmService confirm, TranslationService translation)
    {
        this.venues = venues;
        this.images = images;
        this.artwork = artwork;
        this.gameData = gameData;
        this.configuration = configuration;
        this.confirm = confirm;
        this.translation = translation;
        router = new ViewRouter<VenueRoute>(VenueRoute.Home);
        MigrateScope();
        drawView = DrawView;
        back = () => router.Pop();
        refreshAction = () => venues.EnsureFresh(true);
        scopeScreen = new GeoScopeScreen(Ink, ui, TextStyles.Headline);
    }

    private VenueArt Art => new(images, artwork);

    public void OnOpened()
    {
        router.Reset();
        activeTab = VenueTab.Discover;
        search = string.Empty;
        searchQueryText = string.Empty;
        ResetScrollState();
        venues.EnsureFresh(false);
    }

    public void OnResumed() => venues.EnsureFresh(false);

    public void OnClosed()
    {
        tagSearch = string.Empty;
        tagQuery = string.Empty;
    }

    public void RequestVenue(string venueId) => pendingVenueId = venueId;

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        venues.EnsureFresh(false);
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        ConsumePendingVenue();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        if (router.Depth == 1 && venues.Events.Count > 0)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }
    }

    private void ConsumePendingVenue()
    {
        if (pendingVenueId.Length == 0)
        {
            return;
        }

        var events = venues.Events;
        for (var index = 0; index < events.Count; index++)
        {
            if (!string.Equals(events[index].Id, pendingVenueId, StringComparison.Ordinal))
            {
                continue;
            }

            pendingVenueId = string.Empty;
            var current = router.Current;
            if (current.Screen == VenueScreen.Detail &&
                string.Equals(current.Venue?.Id, events[index].Id, StringComparison.Ordinal))
            {
                return;
            }

            router.Reset();
            OpenDetail(events[index], TabTitle(), false);
            return;
        }

        if (venues.State == VenueState.Failed || (venues.State == VenueState.Ready && !venues.Busy))
        {
            pendingVenueId = string.Empty;
        }
    }

    private void DrawView(VenueRoute route, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case VenueScreen.Filters:
                DrawFilters(context, route);
                break;
            case VenueScreen.Detail:
                DrawDetail(context, route);
                break;
            case VenueScreen.Scope:
                DrawScopeScreen(context, route);
                break;
            case VenueScreen.List:
                DrawList(context, route);
                break;
            default:
                DrawHome(context);
                break;
        }
    }

    private void DrawHome(in PhoneContext context)
    {
        var scale = UiScale.Current;
        RefreshSections();
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case VenueTab.Live:
                    DrawLiveTab(context);
                    break;
                case VenueTab.Events:
                    DrawEventsTab(context);
                    break;
                case VenueTab.Saved:
                    DrawSavedTab(context);
                    break;
                default:
                    DrawDiscoverTab(context);
                    break;
            }
        }

        DrawTabBar(context.Content);
    }

    private string TabTitle() =>
        activeTab switch
        {
            VenueTab.Live => Loc.T(L.Venues.LiveNowLabel),
            VenueTab.Events => Loc.T(L.Venues.Events),
            VenueTab.Saved => Loc.T(L.Venues.Favorites),
            _ => DisplayName,
        };

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)VenueTab.Discover] = new TabItem(Loc.T(L.Venues.Discover), PhoneIcons.Compass,
            PhoneIcons.CompassFilled);
        tabItems[(int)VenueTab.Live] = new TabItem(Loc.T(L.Venues.LiveNow), PhoneIcons.Flame, PhoneIcons.FlameFilled,
            sections.Live.Count, "venues.live");
        tabItems[(int)VenueTab.Events] = new TabItem(Loc.T(L.Venues.Events), PhoneIcons.Calendar,
            PhoneIcons.CalendarFilled);
        tabItems[(int)VenueTab.Saved] = new TabItem(Loc.T(L.Venues.Favorites), PhoneIcons.Star, PhoneIcons.StarFilled);
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((VenueTab)result.Tapped);
    }

    private void SelectTab(VenueTab tab)
    {
        if (tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        visibleCards = PageSize;
        UiFeedback.Play(UiSound.Tap);
    }

    private void EndRootTitle(in NavBarFrame navBar, in PhoneContext context, string id, string title,
        bool filters = true)
    {
        rootButtons[0] = new NavBarButton(PhoneIcons.AdjustmentsHorizontal, FiltersLabel());
        var pressed = AppHeader.EndLargeTitle(in navBar, context, id, title, NavBarStyle.From(ui),
            rootButtons.AsSpan(0, filters ? 1 : 0));
        if (pressed == 0)
        {
            OpenFilters(title);
        }
    }

    private string FiltersLabel()
    {
        var count = ActiveFilterCount;
        if (count == 0)
        {
            return Loc.T(L.Venues.Filters);
        }

        return filtersLabel.IsCurrent(count)
            ? filtersLabel.Value
            : filtersLabel.Store(count, Loc.T(L.Venues.FiltersCount, count.ToString(Loc.Culture)));
    }

    private void OpenFilters(string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        tagSearch = string.Empty;
        tagQuery = string.Empty;
        tagsExpanded = false;
        router.Push(VenueRoute.Filters(backTitle));
    }

    private void OpenScope(string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(VenueRoute.Scope(backTitle));
    }

    private readonly record struct ResolvedScope(IReadOnlySet<string>? DataCenters, string World, string Label);

    private ResolvedScope ResolveScope()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == resolvedScopeFrame)
        {
            return resolvedScope;
        }

        resolvedScopeFrame = frame;
        resolvedScope = ComputeScope();
        return resolvedScope;
    }

    private ResolvedScope ComputeScope()
    {
        var everywhere = new ResolvedScope(null, string.Empty, Loc.T(L.Venues.Everywhere));
        var value = configuration.VenueScopeValue;
        switch (configuration.VenueScope)
        {
            case GeoScopeKind.Everywhere:
                return everywhere;
            case GeoScopeKind.Region:
                return int.TryParse(value, out var regionId) && WorldGeography.RegionById(regionId) is { } picked
                    ? new ResolvedScope(picked.Set, string.Empty, Loc.T(picked.Label))
                    : everywhere;
            case GeoScopeKind.DataCenter:
                return WorldGeography.DataCenter(value) is { } dataCenter
                    ? new ResolvedScope(dataCenter.Set, string.Empty, dataCenter.Name)
                    : everywhere;
            case GeoScopeKind.World:
                return WorldGeography.DataCenterOfWorld(value) is { } worldCenter
                    ? new ResolvedScope(worldCenter.Set, WorldName(worldCenter, value), WorldName(worldCenter, value))
                    : everywhere;
        }

        var homeWorld = CurrentWorld();
        var home = WorldGeography.DataCenterOfWorld(homeWorld);
        if (home is null)
        {
            return everywhere;
        }

        return configuration.VenueScope switch
        {
            GeoScopeKind.MyWorld => new ResolvedScope(home.Set, homeWorld, homeWorld),
            GeoScopeKind.MyRegion when WorldGeography.RegionById(home.RegionId) is { } region =>
                new ResolvedScope(region.Set, string.Empty, Loc.T(region.Label)),
            _ => new ResolvedScope(home.Set, string.Empty, home.Name),
        };
    }

    private static string WorldName(GeoDataCenterInfo dataCenter, string world)
    {
        for (var index = 0; index < dataCenter.Worlds.Length; index++)
        {
            if (string.Equals(dataCenter.Worlds[index], world, StringComparison.OrdinalIgnoreCase))
            {
                return dataCenter.Worlds[index];
            }
        }

        return world;
    }

    private void MigrateScope()
    {
        if (!configuration.VenueAllDataCenters)
        {
            return;
        }

        configuration.VenueAllDataCenters = false;
        configuration.VenueScope = GeoScopeKind.Everywhere;
        configuration.Save();
    }

    private void SetScope(GeoScopeKind kind, string value)
    {
        configuration.VenueScope = kind;
        configuration.VenueScopeValue = value;
        configuration.Save();
        resolvedScopeFrame = -1;
        ResetScrollState();
    }

    private string CurrentWorld() => gameData.WorldName(gameData.LocalCurrentWorldId);

    private static long CurrentMinute(DateTime nowUtc) => nowUtc.Ticks / TimeSpan.TicksPerMinute;

    private bool CheckLanguage()
    {
        if (!labels.LanguageChanged())
        {
            return false;
        }

        sections.Invalidate();
        listQuery.Invalidate();
        searchQuery.Invalidate();
        detailVersion = -1;
        filtersLabel.Reset();
        tagLabelsStale = true;
        return true;
    }

    private void RefreshSections()
    {
        CheckLanguage();
        var nowUtc = DateTime.UtcNow;
        var scope = ResolveScope();
        var key = new VenueSectionsKey(venues.Version, configuration.VenueSourceFilter, scope.DataCenters, scope.World,
            scope.World.Length > 0 ? string.Empty : CurrentWorld(), favoritesStamp, tagsStamp, CurrentMinute(nowUtc),
            configuration.VenueHideAdult, recentsStamp);
        if (!sections.Update(key, venues.Events, configuration.VenueFavorites, configuration.VenueRecents,
                selectedTags, nowUtc))
        {
            return;
        }

        featuredText.Fill(sections.Featured, nowUtc);
        liveText.Fill(sections.Live, nowUtc);
        laterText.Fill(sections.LaterRail, nowUtc);
        nearText.Fill(sections.NearRail, nowUtc);
        recentText.Fill(sections.Recents, nowUtc);
        eventsText.Fill(sections.Events, nowUtc);
        savedText.Fill(sections.Saved, nowUtc);
        RebuildSectionLabels();
        RebuildAgenda();
        RebuildSavedGroups(nowUtc);
    }

    private void OpenDetail(VenueEvent venue, string backTitle, bool animate = true)
    {
        if (animate)
        {
            UiFeedback.Play(UiSound.Tap);
        }

        hoursExpanded = false;
        RecordRecent(venue.Id);
        router.Push(VenueRoute.Detail(venue, backTitle), animate);
    }

    private void OpenList(VenueListKind kind, int category, string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        visibleCards = PageSize;
        router.Push(VenueRoute.ListOf(kind, category, backTitle));
    }

    private void RecordRecent(string id)
    {
        var recents = configuration.VenueRecents;
        if (recents.Count > 0 && string.Equals(recents[0], id, StringComparison.Ordinal))
        {
            return;
        }

        for (var index = recents.Count - 1; index >= 0; index--)
        {
            if (string.Equals(recents[index], id, StringComparison.Ordinal))
            {
                recents.RemoveAt(index);
            }
        }

        recents.Insert(0, id);
        if (recents.Count > VenueSections.MaxRecents)
        {
            recents.RemoveRange(VenueSections.MaxRecents, recents.Count - VenueSections.MaxRecents);
        }

        recentsStamp++;
        configuration.Save();
    }

    private void ClearRecents()
    {
        configuration.VenueRecents.Clear();
        configuration.Save();
        recentsStamp++;
        UiFeedback.Play(UiSound.Tap);
    }

    private void ResetScrollState()
    {
        visibleCards = PageSize;
        ResetDiscover();
    }

    private bool FiltersActive => selectedTags.Count > 0 || configuration.VenueSourceFilter != VenueFilter.SourceAll;

    private int ActiveFilterCount =>
        selectedTags.Count + (configuration.VenueSourceFilter != VenueFilter.SourceAll ? 1 : 0);

    private bool IsFavorite(string id) => VenueFilter.Contains(configuration.VenueFavorites, id);

    private void ToggleFavorite(string id)
    {
        var removed = configuration.VenueFavorites.Remove(id);
        if (!removed)
        {
            configuration.VenueFavorites.Add(id);
        }

        UiFeedback.Play(removed ? UiSound.ToggleOff : UiSound.ToggleOn);
        favoritesStamp++;
        configuration.Save();
    }

    private bool IsTagSelected(string tag) => VenueFilter.Contains(selectedTags, tag);

    private void ToggleTag(string tag)
    {
        tagsStamp++;
        visibleCards = PageSize;
        UiFeedback.Play(UiSound.Tap);
        for (var index = 0; index < selectedTags.Count; index++)
        {
            if (string.Equals(selectedTags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                selectedTags.RemoveAt(index);
                return;
            }
        }

        selectedTags.Add(tag);
    }

    private void ResetFilters()
    {
        selectedTags.Clear();
        tagsStamp++;
        configuration.VenueSourceFilter = VenueFilter.SourceAll;
        configuration.VenueHideAdult = false;
        configuration.Save();
        visibleCards = PageSize;
        UiFeedback.Play(UiSound.Refresh);
    }

    private void HandleCardAction(VenueCardAction action, VenueEvent venue, string backTitle)
    {
        switch (action)
        {
            case VenueCardAction.Open:
                OpenDetail(venue, backTitle);
                break;
            case VenueCardAction.ToggleFavorite:
                ToggleFavorite(venue.Id);
                break;
            case VenueCardAction.Teleport:
                Teleport(venue);
                break;
            case VenueCardAction.Twitch:
                UrlActions.AskThenOpen(venue.TwitchUrl!);
                break;
        }
    }

    private void Teleport(VenueEvent venue)
    {
        if (!venue.CanTeleport)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        TeleportActions.AskThenTravel(confirm, VenueDisplayText.Clean(venue.Title), venue.PlaceLine,
            venue.TeleportCode!);
    }

    public void Dispose()
    {
    }
}
