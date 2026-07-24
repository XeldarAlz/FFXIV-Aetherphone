using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp : IPhoneApp
{
    private const float SearchHeight = 46f;
    private const float SegmentHeight = 44f;
    private const float SegmentTrackHeight = 38f;
    private const float ChipRowHeight = 44f;
    private const int MaxCards = 80;
    public string Id => "venues";
    public string DisplayName => Loc.T(L.Apps.Venues);
    public string Glyph => "V";
    public int BadgeCount => 0;
    private readonly VenuesService venues;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly ArtworkCache artwork;
    private readonly AppSkin ui = new(AppPalettes.Venues);
    private readonly ViewRouter<VenueRoute> router;
    private readonly RouterDraw<VenueRoute> drawView;
    private readonly Action back;
    private readonly List<VenueEvent> filtered = new();
    private readonly List<string> selectedTags = new();
    private readonly SortedSet<string> tagSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> tagList = new();
    private readonly string[] timeLabels = new string[4];
    private string search = string.Empty;
    private bool favoritesOnly;
    private bool lifestreamAvailable;
    private float detailScrollY;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public VenuesApp(VenuesService venues, MediaCache media, HttpService http, ITextureProvider textures,
        GameData gameData, Configuration configuration)
    {
        this.venues = venues;
        this.media = media;
        this.http = http;
        this.gameData = gameData;
        this.configuration = configuration;
        artwork = new ArtworkCache(textures);
        router = new ViewRouter<VenueRoute>(VenueRoute.List);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        venues.EnsureFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        venues.EnsureFresh(false);
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(VenueRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case VenueScreen.Tags:
                DrawTagPicker(area);
                break;
            case VenueScreen.Detail:
                DrawDetail(area, route.Venue!);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private string CurrentDataCenter()
    {
        if (configuration.VenueAllDataCenters)
        {
            return string.Empty;
        }

        return gameData.DataCenterName(gameData.LocalCurrentWorldId);
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawRootHeader(area, scale);
        var pad = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, top),
            new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        UiAnchors.Report("venues.search", searchBar);
        SearchField.Draw(searchBar, "##venueSearch", Loc.T(L.Venues.Search), ref search, AppPalettes.Venues, 80);
        var segmentBar = new Rect(new Vector2(area.Min.X + pad, searchBar.Max.Y),
            new Vector2(area.Max.X - pad, searchBar.Max.Y + SegmentHeight * scale));
        UiAnchors.Report("venues.time", segmentBar);
        DrawTimeSegments(segmentBar);
        var chipBar = new Rect(new Vector2(area.Min.X + pad, segmentBar.Max.Y + 2f * scale),
            new Vector2(area.Max.X - pad, segmentBar.Max.Y + 2f * scale + ChipRowHeight * scale));
        UiAnchors.Report("venues.chips", chipBar);
        DrawFilterChips(chipBar);
        var body = new Rect(new Vector2(area.Min.X, chipBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawList(body);
        }
    }

    private void DrawRootHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, AppPalettes.Venues.TitleInk, 1.3f,
            FontWeight.Bold);
        var actionCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (venues.State == VenueState.Loading)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
            return;
        }

        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(), AppPalettes.Venues.BodyInk,
                AppSkin.Transparent, 0.9f))
        {
            venues.EnsureFresh(true);
        }
    }

    private void DrawTimeSegments(Rect bar)
    {
        timeLabels[0] = TimeFilterLabel(VenueTimeFilter.LiveNow);
        timeLabels[1] = TimeFilterLabel(VenueTimeFilter.Today);
        timeLabels[2] = TimeFilterLabel(VenueTimeFilter.Upcoming);
        timeLabels[3] = TimeFilterLabel(VenueTimeFilter.All);
        var selected = SegmentStrip.Draw("venues.timeFilter", bar, timeLabels, (int)configuration.VenueTimeFilter,
            AppPalettes.Venues, SegmentTrackHeight, 0.9f);
        if (selected == (int)configuration.VenueTimeFilter)
        {
            return;
        }

        configuration.VenueTimeFilter = (VenueTimeFilter)selected;
        configuration.Save();
    }

    private void DrawFilterChips(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = Metrics.Space.Sm * scale;
        var cursor = bar.Min.X;
        var centerY = bar.Center.Y;
        var dataCenter = CurrentDataCenter();
        var dcLabel = dataCenter.Length > 0 ? dataCenter : Loc.T(L.Venues.AllDataCenters);
        if (ui.FlowChip(ref cursor, centerY, gap, dcLabel,
                !configuration.VenueAllDataCenters && dataCenter.Length > 0))
        {
            configuration.VenueAllDataCenters = !configuration.VenueAllDataCenters;
            configuration.Save();
        }

        if (ui.FlowChip(ref cursor, centerY, gap, SourceFilterLabel(configuration.VenueSourceFilter),
                configuration.VenueSourceFilter != VenueFilter.SourceAll))
        {
            configuration.VenueSourceFilter = (configuration.VenueSourceFilter + 1) % 3;
            configuration.Save();
        }

        var tagsLabel = selectedTags.Count > 0
            ? $"{Loc.T(L.Venues.Tags)} · {selectedTags.Count}"
            : Loc.T(L.Venues.Tags);
        if (ui.FlowChip(ref cursor, centerY, gap, tagsLabel, selectedTags.Count > 0))
        {
            router.Push(VenueRoute.Tags);
        }

        if (ui.FlowChip(ref cursor, centerY, gap, Loc.T(L.Venues.Favorites), favoritesOnly))
        {
            favoritesOnly = !favoritesOnly;
        }
    }

    private void DrawList(Rect body)
    {
        var dataCenter = CurrentDataCenter();
        VenueFilter.Apply(venues.Events, filtered, configuration.VenueTimeFilter, configuration.VenueSourceFilter,
            dataCenter, favoritesOnly, configuration.VenueFavorites, selectedTags, search, DateTime.UtcNow);
        DrawSummary(dataCenter);
        if (filtered.Count == 0)
        {
            DrawEmptyState(body);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var nowUtc = DateTime.UtcNow;
        var count = Math.Min(filtered.Count, MaxCards);
        for (var index = 0; index < count; index++)
        {
            var venue = filtered[index];
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + VenueCard.Height * scale));
            if (ImGui.IsRectVisible(card.Min, card.Max))
            {
                var action = VenueCard.Draw(card, venue, IsFavorite(venue.Id), media, http, artwork, ui, nowUtc);
                if (action == VenueCardAction.Open)
                {
                    router.Push(VenueRoute.Detail(venue));
                }
                else if (action == VenueCardAction.ToggleFavorite)
                {
                    ToggleFavorite(venue.Id);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, (VenueCard.Height + VenueCard.Gap) * scale));
        }

        if (filtered.Count > MaxCards)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 8f * scale),
                Loc.T(L.Venues.MoreCount, filtered.Count - MaxCards), AppPalettes.Venues.MutedInk,
                TextStyles.Footnote);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 30f * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawSummary(string dataCenter)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dcLabel = dataCenter.Length > 0 ? dataCenter : Loc.T(L.Venues.AllDataCenters);
        var summary =
            $"{dcLabel}  ·  {TimeFilterLabel(configuration.VenueTimeFilter)}  ·  {Loc.T(L.Venues.EventsCount, filtered.Count)}";
        var origin = ImGui.GetCursorScreenPos();
        var summaryMaxWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X - 8f * scale);
        var summaryFitted = Typography.FitText(summary, summaryMaxWidth, TextStyles.Footnote);
        Typography.Draw(new Vector2(origin.X + 4f * scale, origin.Y + 8f * scale), summaryFitted,
            AppPalettes.Venues.MutedInk, TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 30f * scale));
    }

    private void DrawEmptyState(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var centerX = body.Center.X;
        if (venues.State == VenueState.Loading && venues.Events.Count == 0)
        {
            LoadingPulse.Draw(new Vector2(centerX, body.Min.Y + 90f * scale), 13f * scale, ui.Accent,
                AppPalettes.Venues.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        var failed = venues.State == VenueState.Failed && venues.Events.Count == 0;
        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, body.Min.Y + 84f * scale);
        drawList.AddCircleFilled(iconCenter, 30f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)), 40);
        var icon = failed ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.MapMarkedAlt;
        AppSkin.Icon(drawList, iconCenter, icon.ToIconString(), Palette.WithAlpha(ui.Accent, 0.95f), 1.15f);
        var message = failed ? Loc.T(L.Venues.Failed) : Loc.T(L.Venues.NoVenues);
        Typography.DrawCentered(new Vector2(centerX, iconCenter.Y + 52f * scale), message,
            AppPalettes.Venues.TitleInk, TextStyles.Headline);
        if (failed)
        {
            var retryWidth = Typography.Measure(Loc.T(L.Venues.Retry), 0.9f, FontWeight.SemiBold).X + 44f * scale;
            var retryTop = iconCenter.Y + 78f * scale;
            var retry = new Rect(new Vector2(centerX - retryWidth * 0.5f, retryTop),
                new Vector2(centerX + retryWidth * 0.5f, retryTop + 34f * scale));
            if (ui.GhostButton(retry, Loc.T(L.Venues.Retry)))
            {
                venues.EnsureFresh(true);
            }

            return;
        }

        Typography.DrawCentered(new Vector2(centerX, iconCenter.Y + 76f * scale), Loc.T(L.Venues.EmptyHint),
            AppPalettes.Venues.MutedInk, TextStyles.Footnote);
    }

    private void DrawTagPicker(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Venues.Tags), back);
        if (selectedTags.Count > 0 && ui.HeaderAction(area, Loc.T(L.Venues.ClearTags), true))
        {
            selectedTags.Clear();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            VenueFilter.CollectTags(venues.Events, configuration.VenueSourceFilter, CurrentDataCenter(), tagSet);
            tagList.Clear();
            foreach (var tag in tagSet)
            {
                tagList.Add(tag);
            }

            if (tagList.Count == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 90f * scale),
                    Loc.T(L.Venues.NoVenues), AppPalettes.Venues.MutedInk, TextStyles.Subheadline);
                return;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawTagFlow(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawTagFlow(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var lineHeight = VenueChips.LargeHeight(scale) + gap;
        var cursorX = origin.X;
        var cursorY = origin.Y;
        for (var index = 0; index < tagList.Count; index++)
        {
            var tag = tagList[index];
            var chipWidth = VenueChips.MeasureLarge(tag, scale);
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                cursorY += lineHeight;
            }

            var min = new Vector2(cursorX, cursorY);
            var max = new Vector2(cursorX + chipWidth, cursorY + VenueChips.LargeHeight(scale));
            var hovered = UiInteract.Hover(min, max);
            VenueChips.DrawLarge(drawList, min, tag, IsTagSelected(tag), hovered, scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    ToggleTag(tag);
                }
            }

            cursorX += chipWidth + gap;
        }

        var totalHeight = cursorY - origin.Y + lineHeight;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
    }

    private string TimeFilterLabel(VenueTimeFilter filter) =>
        filter switch
        {
            VenueTimeFilter.LiveNow => Loc.T(L.Venues.LiveNow),
            VenueTimeFilter.Today => Loc.T(L.Venues.Today),
            VenueTimeFilter.Upcoming => Loc.T(L.Venues.Upcoming),
            _ => Loc.T(L.Venues.All),
        };

    private string SourceFilterLabel(int source) =>
        source switch
        {
            VenueFilter.SourceFfxiv => Loc.T(L.Venues.SourceFfxiv),
            VenueFilter.SourcePartake => Loc.T(L.Venues.SourcePartake),
            _ => Loc.T(L.Venues.AllSources),
        };

    private bool IsFavorite(string id) => configuration.VenueFavorites.Contains(id);

    private void ToggleFavorite(string id)
    {
        if (!configuration.VenueFavorites.Remove(id))
        {
            configuration.VenueFavorites.Add(id);
        }

        configuration.Save();
    }

    private bool IsTagSelected(string tag)
    {
        for (var index = 0; index < selectedTags.Count; index++)
        {
            if (string.Equals(selectedTags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleTag(string tag)
    {
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

    public void Dispose() => artwork.Dispose();
}
