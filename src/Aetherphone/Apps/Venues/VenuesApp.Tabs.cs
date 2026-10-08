using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Venues;

internal readonly record struct VenueAgendaEntry(int Index, string Heading, string Start, string End, bool Live,
    int Count, string Title);

internal sealed partial class VenuesApp
{
    private const float CountGap = 4f;

    private readonly List<VenueAgendaEntry> agenda = new();
    private readonly PullToRefresh liveRefresh = new();
    private readonly PullToRefresh eventsRefresh = new();
    private readonly NavBarButton[] listButtons = Array.Empty<NavBarButton>();
    private int savedOpenCount;
    private int savedUpcomingCount;
    private string listHeading = string.Empty;
    private string listTitle = string.Empty;

    private void RebuildAgenda()
    {
        agenda.Clear();
        var events = sections.Events;
        var nowUtc = DateTime.UtcNow;
        var currentGroup = string.Empty;
        var headerSlot = -1;
        for (var index = 0; index < events.Count; index++)
        {
            var venue = events[index];
            var live = venue.IsEventOn(nowUtc);
            var group = live
                ? Loc.T(L.Venues.LiveNowLabel)
                : TimeText.FutureDayLabel(new DateTimeOffset(venue.EventStartUtc!.Value).ToUnixTimeSeconds());
            if (!string.Equals(group, currentGroup, StringComparison.Ordinal))
            {
                headerSlot = agenda.Count;
                agenda.Add(new VenueAgendaEntry(-1, group, string.Empty, string.Empty, false, 0, string.Empty));
                currentGroup = group;
            }

            var start = venue.EventStartUtc is { } startUtc ? TimeText.Clock(startUtc.ToLocalTime()) : string.Empty;
            var end = venue.EventEndUtc is { } endUtc ? TimeText.Clock(endUtc.ToLocalTime()) : string.Empty;
            var title = venue.EventName.Length > 0 ? VenueDisplayText.Clean(venue.EventName) : string.Empty;
            agenda.Add(new VenueAgendaEntry(index, string.Empty, start, end, live, 0, title));
            agenda[headerSlot] = agenda[headerSlot] with { Count = agenda[headerSlot].Count + 1 };
        }
    }

    private void RebuildSavedGroups(DateTime nowUtc)
    {
        savedOpenCount = 0;
        savedUpcomingCount = 0;
        var saved = sections.Saved;
        for (var index = 0; index < saved.Count; index++)
        {
            var venue = saved[index];
            if (venue.IsLive(nowUtc))
            {
                savedOpenCount++;
            }
            else if (venue.StartUtc is { } start && start > nowUtc)
            {
                savedUpcomingCount++;
            }
        }
    }

    private void DrawLiveTab(in PhoneContext context)
    {
        var title = Loc.T(L.Venues.LiveNowLabel);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("venues.live"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            liveRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, venues.Busy, ui.MutedInk, refreshAction);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawScopeRow(drawList, origin, width, title, scale);
            if (DrawLoadState(drawList, new Vector2(origin.X, cursorY), width, navBar.Body, scale, out var stateBottom))
            {
                cursorY = stateBottom;
            }
            else if (sections.Live.Count == 0)
            {
                var action = sections.LaterToday.Count > 0 ? Loc.T(L.Venues.LaterToday) : string.Empty;
                cursorY = DrawEmptyState(drawList, new Vector2(origin.X, cursorY), width,
                    VenuesArt.StateHeight(navBar.Body, scale), PhoneIcons.Flame, Loc.T(L.Venues.NoLive),
                    Loc.T(L.Venues.NoLiveHint), action, out var showLater, scale);
                if (showLater)
                {
                    OpenList(VenueListKind.LaterToday, -1, title);
                }
            }
            else
            {
                cursorY = DrawFeedCards(drawList, new Vector2(origin.X, cursorY + VenuesArt.SectionGap * scale), width,
                    sections.Live, liveText, true, title, scale);
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        EndRootTitle(in navBar, context, "venues.nav.live", title);
    }

    private float DrawFeedCards(ImDrawListPtr drawList, Vector2 origin, float width, IReadOnlyList<VenueEvent> feed,
        VenueTextList text, bool actions, string backTitle, float scale)
    {
        var count = Math.Min(feed.Count, visibleCards);
        var cursorY = origin.Y;
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var venue = feed[index];
            var height = VenueCard.FeedHeight(venue, text[index], width, scale, actions);
            var card = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + height));
            if (index == 0)
            {
                UiAnchors.Report("venues.card.first", card);
            }

            if (ImGui.IsRectVisible(card.Min, card.Max))
            {
                var action = VenueCard.DrawFeed(drawList, ui, card, venue, text[index], IsFavorite(venue.Id), art,
                    actions, scale);
                if (!casinoPill.Draw(drawList, card, VenueCard.HeroHeight(width, scale), venue.Address, navigation,
                        scale))
                {
                    HandleCardAction(action, venue, backTitle);
                }
            }

            cursorY = card.Max.Y + VenuesArt.CardGap * scale;
        }

        return DrawMoreSpinner(feed.Count, count, origin.X, cursorY, width, scale);
    }

    private void DrawEventsTab(in PhoneContext context)
    {
        var title = Loc.T(L.Venues.Events);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("venues.events"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            eventsRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, venues.Busy, ui.MutedInk, refreshAction);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawScopeRow(drawList, origin, width, title, scale);
            if (DrawLoadState(drawList, new Vector2(origin.X, cursorY), width, navBar.Body, scale, out var stateBottom))
            {
                cursorY = stateBottom;
            }
            else if (agenda.Count == 0)
            {
                cursorY = DrawEmptyState(drawList, new Vector2(origin.X, cursorY), width,
                    VenuesArt.StateHeight(navBar.Body, scale), PhoneIcons.Calendar, Loc.T(L.Venues.NoEvents),
                    Loc.T(L.Venues.NoEventsHint), string.Empty, out _, scale);
            }
            else
            {
                cursorY = DrawAgenda(drawList, new Vector2(origin.X, cursorY), width, title, scale);
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        EndRootTitle(in navBar, context, "venues.nav.events", title);
    }

    private float DrawAgenda(ImDrawListPtr drawList, Vector2 origin, float width, string backTitle, float scale)
    {
        var events = sections.Events;
        var art = Art;
        var rowHeight = VenuesArt.RowHeight * scale;
        var cursorY = origin.Y;
        var rowInGroup = 0;
        var groupCount = 0;
        for (var index = 0; index < agenda.Count; index++)
        {
            var entry = agenda[index];
            if (entry.Index < 0)
            {
                cursorY = Section(drawList, origin.X, cursorY, width, entry.Heading, string.Empty, out _, scale);
                ui.Card(drawList, new Vector2(origin.X, cursorY),
                    new Vector2(origin.X + width, cursorY + entry.Count * rowHeight), Metrics.Radius.Grouped * scale);
                rowInGroup = 0;
                groupCount = entry.Count;
                continue;
            }

            var row = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + rowHeight));
            cursorY = row.Max.Y;
            rowInGroup++;
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var venue = events[entry.Index];
            var text = eventsText[entry.Index];
            HandleCardAction(VenueCard.DrawRow(drawList, ui, row, venue, text, IsFavorite(venue.Id), art, scale,
                entry.Start, entry.End, entry.Live, entry.Title), venue, backTitle);
            if (rowInGroup < groupCount)
            {
                var inset = VenuesArt.RowPad + VenueCard.LeadWidth + VenuesArt.RowThumb + VenuesArt.TextGap;
                var left = row.Min.X + inset * scale;
                FeedCell.Hairline(drawList, left, row.Max.X, row.Max.Y, ui.Hairline);
            }
        }

        return cursorY;
    }

    private void DrawSavedTab(in PhoneContext context)
    {
        var title = Loc.T(L.Venues.Favorites);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("venues.saved"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            var saved = sections.Saved;
            if (saved.Count == 0)
            {
                cursorY = DrawEmptyState(drawList, origin, width, VenuesArt.StateHeight(navBar.Body, scale),
                    PhoneIcons.Star, Loc.T(L.Venues.NoSaved), Loc.T(L.Venues.NoSavedHint), Loc.T(L.Venues.Discover),
                    out var discover, scale);
                if (discover)
                {
                    SelectTab(VenueTab.Discover);
                }
            }
            else
            {
                var upcomingEnd = savedOpenCount + savedUpcomingCount;
                cursorY = DrawSavedGroup(drawList, origin.X, cursorY, width, Loc.T(L.Venues.OpenNow), 0,
                    savedOpenCount, title, scale);
                cursorY = DrawSavedGroup(drawList, origin.X, cursorY, width, Loc.T(L.Venues.ComingUp),
                    savedOpenCount, upcomingEnd, title, scale);
                cursorY = DrawSavedGroup(drawList, origin.X, cursorY, width, Loc.T(L.Venues.NoHoursPosted),
                    upcomingEnd, saved.Count, title, scale);
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        EndRootTitle(in navBar, context, "venues.nav.saved", title, false);
    }

    private float DrawSavedGroup(ImDrawListPtr drawList, float left, float top, float width, string heading,
        int start, int end, string backTitle, float scale)
    {
        if (end <= start)
        {
            return top;
        }

        var cursorY = Section(drawList, left, top, width, heading, string.Empty, out _, scale);
        return DrawRowCard(drawList, new Vector2(left, cursorY), width, sections.Saved, savedText, start, end,
            backTitle, scale);
    }

    private void DrawList(in PhoneContext context, VenueRoute route)
    {
        RefreshList(route.List, route.Category);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("venues.list"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawScopeRow(drawList, origin, width, listTitle, scale);
            cursorY += VenuesArt.HeaderGap * scale;
            if (listQuery.Feed.Count == 0)
            {
                cursorY = DrawEmptyState(drawList, new Vector2(origin.X, cursorY), width,
                    VenuesArt.StateHeight(navBar.Body, scale), PhoneIcons.MapPin, Loc.T(L.Venues.NoVenues),
                    Loc.T(L.Venues.EmptyHint), string.Empty, out _, scale);
            }
            else
            {
                Typography.Draw(drawList, new Vector2(origin.X, cursorY + CountGap * scale), listHeading, ui.MutedInk,
                    TextStyles.Footnote);
                cursorY += Typography.LineHeight(TextStyles.Footnote) + (CountGap + VenuesArt.HeaderGap) * scale;
                if (route.List == VenueListKind.Live)
                {
                    cursorY = DrawFeedCards(drawList, new Vector2(origin.X, cursorY), width, listQuery.Feed, listText,
                        true, listTitle, scale);
                }
                else
                {
                    var end = Math.Min(listQuery.Feed.Count, visibleCards);
                    cursorY = DrawRowCard(drawList, new Vector2(origin.X, cursorY), width, listQuery.Feed, listText, 0,
                        end, listTitle, scale);
                    cursorY = DrawMoreSpinner(listQuery.Feed.Count, end, origin.X, cursorY, width, scale);
                }
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "venues.nav.list", listTitle, NavBarStyle.From(ui), listButtons,
            route.BackTitle, back);
    }

    private void RefreshList(VenueListKind kind, int category)
    {
        CheckLanguage();
        var nowUtc = DateTime.UtcNow;
        var time = kind switch
        {
            VenueListKind.Live => VenueTimeFilter.LiveNow,
            VenueListKind.LaterToday => VenueTimeFilter.Today,
            _ => VenueTimeFilter.All,
        };
        var scope = ResolveScope();
        var world = kind == VenueListKind.NearYou ? CurrentWorld() : scope.World;
        var key = new VenueQueryKey(venues.Version, time, configuration.VenueSourceFilter, scope.DataCenters, false,
            favoritesStamp, tagsStamp, string.Empty, CurrentMinute(nowUtc),
            kind == VenueListKind.Category ? category : -1, world, configuration.VenueHideAdult);
        if (!listQuery.Update(key, venues.Events, configuration.VenueFavorites, selectedTags, nowUtc))
        {
            return;
        }

        listText.Fill(listQuery.Feed, nowUtc);
        listHeading = Loc.T(L.Venues.VenueCount, listQuery.Feed.Count.ToString("N0", Loc.Culture));
        listTitle = kind switch
        {
            VenueListKind.Live => Loc.T(L.Venues.LiveNowLabel),
            VenueListKind.LaterToday => Loc.T(L.Venues.LaterToday),
            VenueListKind.NearYou => Loc.T(L.Venues.NearWorld, world),
            VenueListKind.Category when category >= 0 && category < CategoryLabels.Length =>
                Loc.T(CategoryLabels[category]),
            _ => Loc.T(L.Venues.Directory),
        };
    }
}
