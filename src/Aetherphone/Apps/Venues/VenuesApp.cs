using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Venues;

internal sealed class VenuesApp : IPhoneApp
{
    private const float SearchHeight = 46f;
    private const float SegmentHeight = 34f;
    private const float ChipRowHeight = 36f;
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
    private readonly ViewRouter<VenueEvent?> router;
    private readonly RouterDraw<VenueEvent?> drawView;
    private readonly Action backToList;
    private readonly List<VenueEvent> filtered = new();
    private readonly List<string> selectedTags = new();
    private readonly SortedSet<string> tagSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> tagList = new();
    private string search = string.Empty;
    private bool favoritesOnly;
    private bool showTagSheet;
    private bool lifestreamAvailable;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public VenuesApp(VenuesService venues, MediaCache media, HttpService http, ITextureProvider textures,
        GameData gameData, Configuration configuration)
    {
        this.venues = venues;
        this.media = media;
        this.http = http;
        this.gameData = gameData;
        this.configuration = configuration;
        artwork = new ArtworkCache(textures);
        router = new ViewRouter<VenueEvent?>(null, Id);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        showTagSheet = false;
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        venues.EnsureFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
        showTagSheet = false;
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        venues.EnsureFresh(false);
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(VenueEvent? view, Rect area, int depth)
    {
        if (view is { } venue)
        {
            DrawDetail(area, venue);
        }
        else
        {
            DrawRoot(area);
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
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);
        DrawReloadButton(area);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, top),
            new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        DrawSearch(searchBar);
        var segmentBar = new Rect(new Vector2(area.Min.X + pad, searchBar.Max.Y),
            new Vector2(area.Max.X - pad, searchBar.Max.Y + SegmentHeight * scale));
        DrawTimeSegments(segmentBar);
        var chipBar = new Rect(new Vector2(area.Min.X + pad, segmentBar.Max.Y + 2f * scale),
            new Vector2(area.Max.X - pad, segmentBar.Max.Y + 2f * scale + ChipRowHeight * scale));
        DrawFilterChips(chipBar);
        var body = new Rect(new Vector2(area.Min.X, chipBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            if (showTagSheet)
            {
                DrawTagSheet();
            }
            else
            {
                DrawList(body);
            }
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
            var action = VenueCard.Draw(card, venue, IsFavorite(venue.Id), media, http, artwork, frameTheme, nowUtc);
            if (action == VenueCardAction.Open)
            {
                router.Push(venue);
            }
            else if (action == VenueCardAction.ToggleFavorite)
            {
                ToggleFavorite(venue.Id);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, VenueCard.Height * scale + 10f * scale));
        }

        if (filtered.Count > MaxCards)
        {
            var more = Loc.T(L.Venues.MoreCount, filtered.Count - MaxCards);
            Typography.Draw(
                ImGui.GetCursorScreenPos() + new Vector2(4f * ImGuiHelpers.GlobalScale, 4f * ImGuiHelpers.GlobalScale),
                more, frameTheme.TextMuted, 0.8f);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 26f * ImGuiHelpers.GlobalScale));
        }
    }

    private void DrawSummary(string dataCenter)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dcLabel = dataCenter.Length > 0 ? dataCenter : Loc.T(L.Venues.AllDataCenters);
        var summary =
            $"{dcLabel}  ·  {TimeFilterLabel(configuration.VenueTimeFilter)}  ·  {Loc.T(L.Venues.EventsCount, filtered.Count)}";
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + 4f * scale, origin.Y + 8f * scale), summary, frameTheme.TextMuted,
            0.78f);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 30f * scale));
    }

    private void DrawEmptyState(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (venues.State == VenueState.Loading && venues.Events.Count == 0)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 80f * scale), 13f * scale, frameTheme.Accent,
                frameTheme.TextMuted, Loc.T(L.Common.Loading));
            return;
        }

        var message = venues.State == VenueState.Failed ? Loc.T(L.Venues.Failed) : Loc.T(L.Venues.NoVenues);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 90f * scale), message, frameTheme.TextMuted);
    }

    private void DrawTagSheet()
    {
        VenueFilter.CollectTags(venues.Events, configuration.VenueSourceFilter, CurrentDataCenter(), tagSet);
        tagList.Clear();
        foreach (var tag in tagSet)
        {
            tagList.Add(tag);
        }

        SettingsSection.Header(Loc.T(L.Venues.Tags), frameTheme);
        if (selectedTags.Count > 0)
        {
            var clearCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Link(clearCard.NextRow(), "✕", frameTheme.Danger, Loc.T(L.Venues.ClearTags), string.Empty,
                    frameTheme))
            {
                selectedTags.Clear();
            }

            clearCard.End();
        }

        if (tagList.Count == 0)
        {
            Typography.Draw(
                ImGui.GetCursorScreenPos() + new Vector2(4f * ImGuiHelpers.GlobalScale, 10f * ImGuiHelpers.GlobalScale),
                Loc.T(L.Venues.NoVenues), frameTheme.TextMuted, 0.82f);
            return;
        }

        var card = GroupCard.Begin(frameTheme, tagList.Count);
        for (var index = 0; index < tagList.Count; index++)
        {
            var tag = tagList[index];
            if (SettingsRow.Selectable(card.NextRow(), tag, IsTagSelected(tag), frameTheme))
            {
                ToggleTag(tag);
            }
        }

        card.End();
    }

    private void DrawDetail(Rect area, VenueEvent venue)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, VenueText.Fit(venue.Title, area.Width * 0.6f, 1.15f, FontWeight.SemiBold), backToList);
        DrawDetailStar(area, venue);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawHero(venue);
            DrawActions(venue);
            DrawInfo(venue);
            DrawTagsSection(venue);
            DrawAbout(venue);
        }
    }

    private void DrawHero(VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Min(width * 0.5f, 180f * scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var drawList = ImGui.GetWindowDrawList();
        VenueImage.Draw(drawList, rect, venue, media, http, artwork, 16f * scale);
        if (venue.IsLive(DateTime.UtcNow))
        {
            var badgeMin = new Vector2(rect.Min.X + 10f * scale, rect.Min.Y + 10f * scale);
            DrawPill(drawList, badgeMin, Loc.T(L.Common.Live), frameTheme.ToggleOn, new Vector4(1f, 1f, 1f, 0.98f),
                scale);
        }

        var sourceLabel = SourceLabel(venue.Source);
        var sourceSize = Typography.Measure(sourceLabel, 0.74f, FontWeight.SemiBold);
        var sourceMin = new Vector2(rect.Max.X - sourceSize.X - 24f * scale, rect.Min.Y + 10f * scale);
        DrawPill(drawList, sourceMin, sourceLabel, new Vector4(0.04f, 0.04f, 0.06f, 0.78f),
            new Vector4(1f, 1f, 1f, 0.96f), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 6f * scale));
    }

    private void DrawActions(VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 40f * scale;
        var gap = 8f * scale;
        var hasTeleport = venue.CanTeleport;
        var hasDiscord = !string.IsNullOrEmpty(venue.DiscordUrl);
        var slots = 1 + (hasTeleport ? 1 : 0) + (hasDiscord ? 1 : 0);
        var slotWidth = (width - gap * (slots - 1)) / slots;
        var cursor = origin.X;
        if (hasTeleport)
        {
            var rect = new Rect(new Vector2(cursor, origin.Y), new Vector2(cursor + slotWidth, origin.Y + height));
            if (DrawButton(rect, Loc.T(L.Venues.Teleport), true, lifestreamAvailable))
            {
                if (lifestreamAvailable)
                {
                    LifestreamBridge.Travel(venue.TeleportCode!);
                }
                else
                {
                    ImGui.SetClipboardText($"/li {venue.TeleportCode}");
                }
            }

            if (!lifestreamAvailable && ImGui.IsMouseHoveringRect(rect.Min, rect.Max))
            {
                ImGui.SetTooltip(Loc.T(L.Venues.NeedsLifestream));
            }

            cursor += slotWidth + gap;
        }

        var openRect = new Rect(new Vector2(cursor, origin.Y), new Vector2(cursor + slotWidth, origin.Y + height));
        if (DrawButton(openRect, Loc.T(L.Venues.Open), !hasTeleport, true))
        {
            UrlActions.OpenInBrowser(venue.Url);
        }

        cursor += slotWidth + gap;
        if (hasDiscord)
        {
            var discordRect = new Rect(new Vector2(cursor, origin.Y),
                new Vector2(cursor + slotWidth, origin.Y + height));
            if (DrawButton(discordRect, Loc.T(L.Venues.Discord), false, true))
            {
                UrlActions.OpenInBrowser(venue.DiscordUrl!);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    private void DrawInfo(VenueEvent venue)
    {
        var rows = 1;
        if (venue.DataCenter.Length > 0)
        {
            rows++;
        }

        if (venue.World.Length > 0)
        {
            rows++;
        }

        if (venue.LocationLine.Length > 0)
        {
            rows++;
        }

        if (venue.Host.Length > 0)
        {
            rows++;
        }

        if (venue.AttendeeCount > 0)
        {
            rows++;
        }

        SettingsSection.Header(Loc.T(L.Venues.Details), frameTheme);
        var card = GroupCard.Begin(frameTheme, rows);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.When), VenueFormat.Range(venue), frameTheme);
        if (venue.DataCenter.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.DataCenter), venue.DataCenter, frameTheme);
        }

        if (venue.World.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.World), venue.World, frameTheme);
        }

        if (venue.LocationLine.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.Location), venue.LocationLine, frameTheme);
        }

        if (venue.Host.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.Host), venue.Host, frameTheme);
        }

        if (venue.AttendeeCount > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Venues.Attendees), venue.AttendeeCount.ToString(), frameTheme);
        }

        card.End();
    }

    private void DrawTagsSection(VenueEvent venue)
    {
        if (venue.Tags.Count == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Venues.Tags), frameTheme);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = 6f * scale;
        var lineHeight = VenueChips.Height(scale) + 7f * scale;
        var cursorX = origin.X;
        var cursorY = origin.Y;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var chipWidth = VenueChips.Measure(tag, scale);
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                cursorY += lineHeight;
            }

            VenueChips.Draw(drawList, new Vector2(cursorX, cursorY), tag, scale);
            cursorX += chipWidth + gap;
        }

        var totalHeight = cursorY - origin.Y + lineHeight;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
    }

    private void DrawAbout(VenueEvent venue)
    {
        if (venue.Description.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Venues.About), frameTheme);
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.9f))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(venue.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
    }

    private void DrawSearch(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Min.Y + 6f * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Max.Y - 6f * scale);
        drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(frameTheme.GroupedCard),
            (pillMax.Y - pillMin.Y) * 0.5f);
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 28f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputTextWithHint("##venueSearch", Loc.T(L.Venues.Search), ref search, 80, ImGuiInputTextFlags.None);
        }
    }

    private void DrawTimeSegments(Rect bar)
    {
        var labels = new[]
        {
            TimeFilterLabel(VenueTimeFilter.LiveNow), TimeFilterLabel(VenueTimeFilter.Today),
            TimeFilterLabel(VenueTimeFilter.Upcoming), TimeFilterLabel(VenueTimeFilter.All),
        };
        var selected = SegmentStrip.Draw("venues.timeFilter", bar, labels, (int)configuration.VenueTimeFilter,
            frameTheme);
        if (selected != (int)configuration.VenueTimeFilter)
        {
            configuration.VenueTimeFilter = (VenueTimeFilter)selected;
            configuration.Save();
        }
    }

    private void DrawFilterChips(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 8f * scale;
        var cursor = bar.Min.X;
        var centerY = bar.Center.Y;
        var dataCenter = CurrentDataCenter();
        var dcLabel = dataCenter.Length > 0 ? dataCenter : Loc.T(L.Venues.AllDataCenters);
        if (DrawChip(ref cursor, centerY, gap, dcLabel, !configuration.VenueAllDataCenters && dataCenter.Length > 0))
        {
            configuration.VenueAllDataCenters = !configuration.VenueAllDataCenters;
            configuration.Save();
        }

        if (DrawChip(ref cursor, centerY, gap, SourceFilterLabel(configuration.VenueSourceFilter),
                configuration.VenueSourceFilter != VenueFilter.SourceAll))
        {
            configuration.VenueSourceFilter = (configuration.VenueSourceFilter + 1) % 3;
            configuration.Save();
        }

        var tagsLabel = selectedTags.Count > 0
            ? $"{Loc.T(L.Venues.Tags)} · {selectedTags.Count}"
            : Loc.T(L.Venues.Tags);
        if (DrawChip(ref cursor, centerY, gap, tagsLabel, showTagSheet || selectedTags.Count > 0))
        {
            showTagSheet = !showTagSheet;
        }

        if (DrawChip(ref cursor, centerY, gap, Loc.T(L.Venues.Favorites), favoritesOnly))
        {
            favoritesOnly = !favoritesOnly;
        }
    }

    private bool DrawChip(ref float cursorX, float centerY, float gap, string label, bool active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(label, 0.8f, FontWeight.Medium);
        var height = 28f * scale;
        var width = textSize.X + 22f * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + width, centerY + height * 0.5f);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = active ? Palette.WithAlpha(frameTheme.Accent, 0.92f) : frameTheme.GroupedCard;
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        var ink = active ? frameTheme.TextStrong :
            hovered ? frameTheme.TextStrong : frameTheme.TextMuted;
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, centerY - textSize.Y * 0.5f), label, ink, 0.8f,
            FontWeight.Medium);
        cursorX = max.X + gap;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool DrawButton(Rect rect, string label, bool primary, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var rounding = rect.Height * 0.32f;
        Vector4 fill;
        Vector4 ink;
        if (primary)
        {
            fill = hovered ? Palette.Mix(frameTheme.Accent, frameTheme.TextStrong, 0.12f) : frameTheme.Accent;
            ink = new Vector4(1f, 1f, 1f, 0.98f);
        }
        else
        {
            fill = hovered ? Palette.Mix(frameTheme.GroupedCard, frameTheme.TextStrong, 0.08f) : frameTheme.GroupedCard;
            ink = frameTheme.TextStrong;
        }

        if (!enabled)
        {
            fill = Palette.WithAlpha(fill, 0.45f);
            ink = frameTheme.TextMuted;
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawReloadButton(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Max.X - 18f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var box = 14f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));
        var glyph = FontAwesomeIcon.Sync.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? frameTheme.TextStrong : frameTheme.TextMuted))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            venues.EnsureFresh(true);
        }
    }

    private void DrawDetailStar(Rect area, VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Max.X - 18f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var box = 14f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));
        var favorite = IsFavorite(venue.Id);
        var glyph = FontAwesomeIcon.Star.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, favorite ? frameTheme.Accent :
                       hovered ? frameTheme.TextStrong : frameTheme.TextMuted))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                ToggleFavorite(venue.Id);
            }
        }
    }

    private static void DrawPill(ImDrawListPtr drawList, Vector2 min, string label, Vector4 fill, Vector4 ink,
        float scale)
    {
        var textSize = Typography.Measure(label, 0.74f, FontWeight.SemiBold);
        var height = 20f * scale;
        var width = textSize.X + 16f * scale;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f), label,
            ink, 0.74f, FontWeight.SemiBold);
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

    private static string SourceLabel(VenueSource source) =>
        source switch
        {
            VenueSource.Partake => Loc.T(L.Venues.SourcePartake),
            _ => Loc.T(L.Venues.SourceFfxiv),
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
