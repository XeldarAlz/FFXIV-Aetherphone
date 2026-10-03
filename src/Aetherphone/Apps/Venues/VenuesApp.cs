using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp : IPhoneApp, ISpotlightVenues
{
    private const float CellPadX = SocialChrome.CellPadX;
    private const float HeaderIconSize = 21f;
    private const float ScopePillHeight = 28f;
    private const float SectionRowHeight = 36f;
    private const float EmptyStateTop = 70f;
    private const int PageSize = 30;
    private const int TabCount = 4;

    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ScopePillStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle ScreenTitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle SeeAllStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle EmptyTitleStyle = TextStyles.Headline;
    private static readonly TextStyle EmptyBodyStyle = TextStyles.Subheadline;
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
    private readonly ViewRouter<VenueRoute> router;
    private readonly RouterDraw<VenueRoute> drawView;
    private readonly Action back;
    private readonly VenueSections sections = new();
    private readonly VenueQuery listQuery = new();
    private readonly VenueQuery searchQuery = new();
    private readonly VenueLabelCache labels = new();
    private readonly VenueTextList featuredText = new("venues.featured.");
    private readonly VenueTextList liveText = new("venues.live.");
    private readonly VenueTextList laterText = new("venues.later.");
    private readonly VenueTextList nearText = new("venues.near.");
    private readonly VenueTextList eventsText = new("venues.events.", true);
    private readonly VenueTextList savedText = new("venues.saved.");
    private readonly VenueTextList listText = new("venues.list.");
    private readonly VenueTextList searchText = new("venues.search.");
    private readonly List<string> selectedTags = new();
    private ResolvedScope resolvedScope;
    private int resolvedScopeFrame = -1;
    private readonly Dictionary<string, string> venueLanguages = new(StringComparer.Ordinal);
    private VenueTab activeTab = VenueTab.Discover;
    private string search = string.Empty;
    private int favoritesStamp;
    private int tagsStamp;
    private int visibleCards = PageSize;
    private string pendingVenueId = string.Empty;
    private PhoneTheme theme = PhoneTheme.Default;
    private Rect screenRect;

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
        scopeScreen = new GeoScopeScreen(Ink, ui, ScreenTitleStyle);
    }

    private VenueArt Art => new(images, artwork);

    public void OnOpened()
    {
        router.Reset();
        activeTab = VenueTab.Discover;
        search = string.Empty;
        ResetScrollState();
        venues.EnsureFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
    }

    public void RequestVenue(string venueId) => pendingVenueId = venueId;

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        ui.Theme = theme;
        venues.EnsureFresh(false);
        ConsumePendingVenue();
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        screenRect = screen;
        ui.Backdrop(screen);
        router.Draw(SceneChrome.AppAreaFrom(context.Content, theme, scale), AppSkin.Transparent,
            ImGui.GetIO().DeltaTime, drawView);
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

        var wanted = pendingVenueId;
        pendingVenueId = string.Empty;
        var events = venues.Events;
        for (var index = 0; index < events.Count; index++)
        {
            if (string.Equals(events[index].Id, wanted, StringComparison.Ordinal))
            {
                OpenDetail(events[index], false);
                return;
            }
        }
    }

    private void DrawView(VenueRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case VenueScreen.Filters:
                DrawFilters(area);
                break;
            case VenueScreen.Detail:
                DrawDetail(area, route.Venue!);
                break;
            case VenueScreen.Scope:
                DrawScopeScreen(area);
                break;
            case VenueScreen.List:
                DrawList(area, route.List, route.Category);
                break;
            default:
                DrawHome(area);
                break;
        }
    }

    private void DrawHome(Rect area)
    {
        var scale = UiScale.Current;
        RefreshSections();
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case VenueTab.Live:
                    DrawLiveTab(body);
                    break;
                case VenueTab.Events:
                    DrawEventsTab(body);
                    break;
                case VenueTab.Saved:
                    DrawSavedTab(body);
                    break;
                default:
                    DrawDiscoverTab(body);
                    break;
            }
        }

        DrawTopBar(area, scale);
        DrawTabBar(area);
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
    }

    private void DrawTopBar(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        ui.PaintGradient(drawList, header, screenRect, 0f);
        var rowCenterY = header.Center.Y;
        var filtersCenter = SocialChrome.HeaderSlot(area, 1);
        var radius = SocialChrome.HeaderIconRadius * scale;
        var pill = DrawScopePill(new Vector2(filtersCenter.X - radius - 8f * scale, rowCenterY), scale);
        var titleLeft = area.Min.X + CellPadX * scale;
        var titleLimit = MathF.Max(1f, pill.Min.X - 10f * scale - titleLeft);
        var title = Typography.FitText(TabTitle(), titleLimit, WordmarkStyle);
        var titleSize = Typography.Measure(title, WordmarkStyle);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleSize.Y * 0.5f), title, Ink.TitleInk,
            WordmarkStyle);
        if (venues.Busy && titleLeft + titleSize.X + 22f * scale < pill.Min.X)
        {
            LoadingPulse.Spinner(new Vector2(titleLeft + titleSize.X + 13f * scale, rowCenterY), 6f * scale,
                Ink.Accent);
        }

        if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Refresh, Loc.T(L.Common.Refresh)))
        {
            venues.EnsureFresh(true);
        }

        if (DrawHeaderIcon(drawList, filtersCenter, PhoneIcons.AdjustmentsHorizontal, Loc.T(L.Venues.Filters),
                FiltersActive, ActiveFilterCount))
        {
            router.Push(VenueRoute.Filters);
        }

    }

    private Rect DrawScopePill(Vector2 rightCenter, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var label = ResolveScope().Label;
        var labelSize = Typography.Measure(label, ScopePillStyle);
        var width = labelSize.X + 30f * scale;
        var half = ScopePillHeight * scale * 0.5f;
        var rect = new Rect(new Vector2(rightCenter.X - width, rightCenter.Y - half),
            new Vector2(rightCenter.X, rightCenter.Y + half));
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, half, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.ChipFill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, half, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 11f * scale, rect.Center.Y - labelSize.Y * 0.5f), label,
            Ink.AccentLink, ScopePillStyle);
        PhoneIcon.Draw(drawList, new Vector2(rect.Max.X - 11f * scale, rect.Center.Y), PhoneIcons.ChevronDown,
            Palette.WithAlpha(Ink.AccentLink, 0.85f), 12f * scale);
        UiAnchors.Report("venues.scope", rect);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            router.Push(VenueRoute.Scope);
        }

        return rect;
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

    private long CurrentMinute(DateTime nowUtc) => nowUtc.Ticks / TimeSpan.TicksPerMinute;

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
        return true;
    }

    private void RefreshSections()
    {
        CheckLanguage();
        var nowUtc = DateTime.UtcNow;
        var scope = ResolveScope();
        var key = new VenueSectionsKey(venues.Version, configuration.VenueSourceFilter, scope.DataCenters, scope.World,
            scope.World.Length > 0 ? string.Empty : CurrentWorld(), favoritesStamp, tagsStamp, CurrentMinute(nowUtc),
            configuration.VenueHideAdult);
        if (!sections.Update(key, venues.Events, configuration.VenueFavorites, selectedTags, nowUtc))
        {
            return;
        }

        featuredText.Fill(sections.Featured, nowUtc);
        liveText.Fill(sections.Live, nowUtc);
        laterText.Fill(sections.LaterRail, nowUtc);
        nearText.Fill(sections.NearRail, nowUtc);
        eventsText.Fill(sections.Events, nowUtc);
        savedText.Fill(sections.Saved, nowUtc);
        RebuildSectionLabels();
        RebuildAgenda();
    }

    private void OpenDetail(VenueEvent venue, bool animate = true)
    {
        detailScrollY = 0f;
        router.Push(VenueRoute.Detail(venue), animate);
    }

    private void OpenList(VenueListKind kind, int category = -1)
    {
        visibleCards = PageSize;
        router.Push(VenueRoute.ListOf(kind, category));
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
        if (!configuration.VenueFavorites.Remove(id))
        {
            configuration.VenueFavorites.Add(id);
        }

        favoritesStamp++;
        configuration.Save();
    }

    private bool IsTagSelected(string tag) => VenueFilter.Contains(selectedTags, tag);

    private void ToggleTag(string tag)
    {
        tagsStamp++;
        visibleCards = PageSize;
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
        configuration.Save();
    }

    private void HandleCardAction(VenueCardAction action, VenueEvent venue)
    {
        switch (action)
        {
            case VenueCardAction.Open:
                OpenDetail(venue);
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

        TeleportActions.AskThenTravel(confirm, venue.Title, venue.PlaceLine, venue.TeleportCode!);
    }

    private static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            HeaderIconSize, tooltip, Ink, Ink.MutedInk, highlighted, badge);

    private void DrawSectionHeading(string label, float scale, string action = "", Action? onAction = null)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = SectionRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var labelHeight = Typography.LineHeight(SectionStyle);
        var textTop = origin.Y + height - labelHeight - 7f * scale;
        var right = origin.X + width - CellPadX * scale;
        var labelRight = right;
        if (action.Length > 0)
        {
            var size = Typography.Measure(action, SeeAllStyle);
            var min = new Vector2(right - size.X, textTop);
            var hitMin = min - new Vector2(8f * scale, 6f * scale);
            var hitMax = min + size + new Vector2(8f * scale, 6f * scale);
            var hovered = UiInteract.Hover(hitMin, hitMax);
            Typography.Draw(drawList, min, action, hovered ? Ink.TitleInk : Ink.AccentLink, SeeAllStyle);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(hitMin, hitMax, hovered))
            {
                onAction?.Invoke();
            }

            labelRight = min.X - 12f * scale;
        }

        var left = origin.X + CellPadX * scale;
        Typography.Draw(drawList, new Vector2(left, textTop),
            Typography.FitText(label, MathF.Max(1f, labelRight - left), SectionStyle), Ink.FaintInk, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawEmptyState(string glyph, string title, string body, string action = "", Action? onAction = null)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var centerX = origin.X + width * 0.5f;
        var iconCenter = new Vector2(centerX, origin.Y + EmptyStateTop * scale);
        drawList.AddCircleFilled(iconCenter, 32f * scale, ImGui.GetColorU32(Ink.AccentWash), 40);
        PhoneIcon.Draw(drawList, iconCenter, glyph, Ink.AccentLink, 28f * scale);
        var maxWidth = MathF.Max(1f, width - CellPadX * 2f * scale);
        var bottom = Typography.DrawWrappedCentered(drawList, title, EmptyTitleStyle, Ink.TitleInk,
            new Vector2(centerX, iconCenter.Y + 48f * scale), maxWidth);
        if (body.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, body, EmptyBodyStyle, Ink.MutedInk,
                new Vector2(centerX, bottom + 6f * scale), maxWidth);
        }

        if (action.Length > 0)
        {
            var buttonWidth = Typography.Measure(action, TextStyles.SubheadlineEmphasized).X + 44f * scale;
            var button = new Rect(new Vector2(centerX - buttonWidth * 0.5f, bottom + 16f * scale),
                new Vector2(centerX + buttonWidth * 0.5f, bottom + 54f * scale));
            if (SocialPill.Accent(drawList, button, action, Ink, TextStyles.SubheadlineEmphasized,
                    button.Height * 0.5f))
            {
                onAction?.Invoke();
            }

            bottom = button.Max.Y;
        }

        ImGui.Dummy(new Vector2(width, bottom - origin.Y + Metrics.Space.Lg * scale));
    }

    private bool DrawLoadingOrFailure(Rect body)
    {
        if (venues.Events.Count > 0)
        {
            return false;
        }

        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        if (venues.State is VenueState.Loading or VenueState.Idle)
        {
            Skeleton.Feed(ImGui.GetWindowDrawList(),
                new Rect(new Vector2(origin.X + CellPadX * scale, origin.Y + 8f * scale),
                    new Vector2(origin.X + width - CellPadX * scale, body.Max.Y - 12f * scale)), scale);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, body.Max.Y - origin.Y)));
            return true;
        }

        if (venues.State != VenueState.Failed)
        {
            return false;
        }

        DrawEmptyState(PhoneIcons.InfoCircle, Loc.T(L.Venues.Failed), string.Empty, Loc.T(L.Venues.Retry),
            retryAction);
        return true;
    }

    private void DrawFeedList(IReadOnlyList<VenueEvent> feed, VenueTextList text, bool actions = false)
    {
        var count = Math.Min(feed.Count, visibleCards);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var venue = feed[index];
            var cardTop = ImGui.GetCursorScreenPos();
            var action = VenueCard.DrawFeed(venue, text[index], IsFavorite(venue.Id), art, Ink, actions);
            if (index == 0 && UiAnchors.Recording)
            {
                UiAnchors.Report("venues.card.first", new Rect(cardTop,
                    new Vector2(cardTop.X + ScrollLayout.StableContentWidth(), ImGui.GetCursorScreenPos().Y)));
            }

            HandleCardAction(action, venue);
        }

        if (feed.Count <= count)
        {
            return;
        }

        if (InfiniteScroll.ReachedBottom())
        {
            visibleCards += PageSize;
        }

        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + 20f * scale), 7f * scale, Ink.Accent);
        ImGui.Dummy(new Vector2(width, 40f * scale));
    }

    public void Dispose()
    {
    }
}
