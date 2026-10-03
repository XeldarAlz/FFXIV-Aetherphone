using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal readonly record struct VenueAgendaEntry(int Index, string Heading, string Start, string End, bool Live);

internal sealed partial class VenuesApp
{
    private readonly List<VenueAgendaEntry> agenda = new();
    private string listHeading = string.Empty;
    private string listTitle = string.Empty;

    private void RebuildAgenda()
    {
        agenda.Clear();
        var events = sections.Events;
        var nowUtc = DateTime.UtcNow;
        var currentGroup = string.Empty;
        for (var index = 0; index < events.Count; index++)
        {
            var venue = events[index];
            var live = venue.IsEventOn(nowUtc);
            var group = live
                ? Loc.Upper(Loc.T(L.Venues.LiveNowLabel))
                : Loc.Culture.TextInfo.ToUpper(
                    TimeText.FutureDayLabel(new DateTimeOffset(venue.EventStartUtc!.Value).ToUnixTimeSeconds()));
            if (!string.Equals(group, currentGroup, StringComparison.Ordinal))
            {
                agenda.Add(new VenueAgendaEntry(-1, group, string.Empty, string.Empty, false));
                currentGroup = group;
            }

            var start = venue.EventStartUtc is { } startUtc ? TimeText.Clock(startUtc.ToLocalTime()) : string.Empty;
            var end = venue.EventEndUtc is { } endUtc ? TimeText.Clock(endUtc.ToLocalTime()) : string.Empty;
            agenda.Add(new VenueAgendaEntry(index, string.Empty, start, end, live));
        }
    }

    private void DrawLiveTab(Rect body)
    {
        EnsureActions();
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (!DrawLoadingOrFailure(body))
            {
                if (sections.Live.Count == 0)
                {
                    DrawEmptyState(PhoneIcons.Flame, Loc.T(L.Venues.NoLive), Loc.T(L.Venues.NoLiveHint),
                        sections.LaterToday.Count > 0 ? Loc.T(L.Venues.LaterToday) : string.Empty, seeLaterAction);
                }
                else
                {
                    DrawSectionHeading(liveHeading, scale);
                    DrawFeedList(sections.Live, liveText, true);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawEventsTab(Rect body)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (!DrawLoadingOrFailure(body))
            {
                if (agenda.Count == 0)
                {
                    DrawEmptyState(PhoneIcons.Calendar, Loc.T(L.Venues.NoEvents), Loc.T(L.Venues.NoEventsHint));
                }
                else
                {
                    DrawAgenda(scale);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawAgenda(float scale)
    {
        var events = sections.Events;
        var art = Art;
        for (var index = 0; index < agenda.Count; index++)
        {
            var entry = agenda[index];
            if (entry.Index < 0)
            {
                DrawSectionHeading(entry.Heading, scale);
                continue;
            }

            var venue = events[entry.Index];
            var action = VenueCard.DrawRow(venue, eventsText[entry.Index], IsFavorite(venue.Id), art, Ink, entry.Start,
                entry.End, entry.Live, venue.EventName);
            HandleCardAction(action, venue);
        }
    }

    private void DrawSavedTab(Rect body)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (sections.Saved.Count == 0)
            {
                DrawEmptyState(PhoneIcons.Star, Loc.T(L.Venues.NoSaved), Loc.T(L.Venues.NoSavedHint));
            }
            else
            {
                var saved = sections.Saved;
                var art = Art;
                for (var index = 0; index < saved.Count; index++)
                {
                    var venue = saved[index];
                    HandleCardAction(VenueCard.DrawRow(venue, savedText[index], true, art, Ink), venue);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawList(Rect area, VenueListKind kind, int category)
    {
        var scale = UiScale.Current;
        RefreshList(kind, category);
        SocialChrome.DrawScreenHeader(area, listTitle, Ink, back, ScreenTitleStyle, 0f, string.Empty, true, true);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (listQuery.Feed.Count == 0)
            {
                DrawEmptyState(PhoneIcons.MapPin, Loc.T(L.Venues.NoVenues), Loc.T(L.Venues.EmptyHint));
            }
            else
            {
                DrawSectionHeading(listHeading, scale);
                DrawFeedList(listQuery.Feed, listText, kind == VenueListKind.Live);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
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
        listHeading = Loc.Culture.TextInfo.ToUpper(
            Loc.T(L.Venues.VenueCount, listQuery.Feed.Count.ToString("N0", Loc.Culture)));
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
