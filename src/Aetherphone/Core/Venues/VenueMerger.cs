using Aetherphone.Core.Rolladeck;

namespace Aetherphone.Core.Venues;

internal static class VenueMerger
{
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(12);

    public static VenueSnapshot Merge(IReadOnlyList<VenueEvent> listings, IReadOnlyList<DirectoryVenueEntry> directory,
        IReadOnlyList<OpenVenueEntry> openVenues, IReadOnlyList<LiveDjEntry> liveDjs, DateTime liveFetchedUtc,
        DateTime nowUtc)
    {
        var merged = new List<VenueEvent>(listings.Count + directory.Count);
        var byAddress = new Dictionary<VenueAddress, int>(listings.Count);
        for (var index = 0; index < listings.Count; index++)
        {
            var venue = listings[index];
            if (venue.Address.IsKnown && !byAddress.ContainsKey(venue.Address))
            {
                byAddress[venue.Address] = merged.Count;
            }

            merged.Add(venue);
        }

        for (var index = 0; index < directory.Count; index++)
        {
            var mapped = VenueMapper.FromRolladeck(directory[index], nowUtc);
            if (mapped is null)
            {
                continue;
            }

            if (mapped.Address.IsKnown && byAddress.TryGetValue(mapped.Address, out var existing))
            {
                merged[existing] = Enrich(merged[existing], mapped);
                continue;
            }

            if (mapped.Address.IsKnown)
            {
                byAddress[mapped.Address] = merged.Count;
            }

            merged.Add(mapped);
        }

        var confirmedUntil = liveFetchedUtc + ConfirmationLifetime;
        for (var index = 0; index < openVenues.Count; index++)
        {
            var open = openVenues[index];
            var address = VenueAddress.Of(open.Server, open.District, open.Ward, open.Plot);
            if (!address.IsKnown)
            {
                continue;
            }

            if (!byAddress.TryGetValue(address, out var slot))
            {
                var created = FromOpenVenue(open, address, nowUtc);
                if (created is null)
                {
                    continue;
                }

                slot = merged.Count;
                byAddress[address] = slot;
                merged.Add(created);
            }

            merged[slot] = Confirm(merged[slot], open, confirmedUntil, nowUtc);
        }

        var djs = new List<VenueDj>(liveDjs.Count);
        for (var index = 0; index < liveDjs.Count; index++)
        {
            var dj = liveDjs[index];
            var address = VenueAddress.Of(dj.Server, dj.District, dj.Ward, dj.Plot);
            if (!address.IsKnown || !byAddress.TryGetValue(address, out var slot))
            {
                continue;
            }

            merged[slot] = AttachDj(merged[slot], dj, confirmedUntil);
            djs.Add(new VenueDj(dj.NormalizedName, NullIfEmpty(dj.AvatarUrl), dj.ViewerCount,
                NullIfEmpty(dj.TwitchUrl), merged[slot].Id)
            {
                Genres = GenresOf(dj),
            });
        }

        djs.Sort(static (left, right) => right.Viewers.CompareTo(left.Viewers));
        return new VenueSnapshot(merged.ToArray(), djs.ToArray());
    }

    private static VenueEvent Enrich(VenueEvent listing, VenueEvent rolladeck)
    {
        var adoptOpening = !listing.HasOpening && rolladeck.HasOpening;
        var adoptEvent = !listing.IsEvent && rolladeck.IsEvent;
        return listing with
        {
            Sources = listing.Sources | VenueSources.Rolladeck,
            BannerUrl = listing.BannerUrl ?? rolladeck.BannerUrl,
            LogoUrl = listing.LogoUrl ?? rolladeck.LogoUrl,
            TeleportCode = listing.TeleportCode ?? rolladeck.TeleportCode,
            WebsiteUrl = listing.WebsiteUrl ?? rolladeck.WebsiteUrl,
            DiscordUrl = listing.DiscordUrl ?? rolladeck.DiscordUrl,
            RolladeckUrl = rolladeck.RolladeckUrl,
            StartUtc = adoptOpening ? rolladeck.StartUtc : listing.StartUtc,
            EndUtc = adoptOpening ? rolladeck.EndUtc : listing.EndUtc,
            EventStartUtc = adoptEvent ? rolladeck.EventStartUtc : listing.EventStartUtc,
            EventEndUtc = adoptEvent ? rolladeck.EventEndUtc : listing.EventEndUtc,
            EventName = adoptEvent ? rolladeck.EventName : listing.EventName,
        };
    }

    private static VenueEvent Confirm(VenueEvent venue, OpenVenueEntry open, DateTime confirmedUntil, DateTime nowUtc)
    {
        var headline = RolladeckText.Normalize(open.EventName ?? open.DjName ?? open.DiscordEventName);
        var startUtc = venue.StartUtc;
        var endUtc = venue.EndUtc;
        if (!venue.IsScheduledOpen(nowUtc) && VenueMapper.TryParseUtc(open.EventEnd, out var eventEnd) &&
            eventEnd > nowUtc)
        {
            startUtc = VenueMapper.TryParseUtc(open.EventStart, out var eventStart) ? eventStart : nowUtc;
            endUtc = eventEnd;
        }

        return venue with
        {
            Sources = venue.Sources | VenueSources.Rolladeck,
            LiveConfirmedUntilUtc = confirmedUntil,
            LiveHeadline = headline.Length > 0 ? headline : venue.LiveHeadline,
            TwitchUrl = string.IsNullOrEmpty(open.DjTwitch) ? venue.TwitchUrl : open.DjTwitch,
            Description = venue.Description.Length > 0
                ? venue.Description
                : RolladeckText.Normalize(open.Description),
            BannerUrl = venue.BannerUrl ?? NullIfEmpty(open.BannerUrl),
            LogoUrl = venue.LogoUrl ?? NullIfEmpty(open.LogoUrl),
            RolladeckUrl = venue.RolladeckUrl ?? VenueMapper.RolladeckVenueUrl(open.Slug),
            StartUtc = startUtc,
            EndUtc = endUtc,
        };
    }

    private static VenueEvent AttachDj(VenueEvent venue, LiveDjEntry dj, DateTime confirmedUntil)
    {
        var busiest = dj.ViewerCount >= venue.LiveViewers || venue.LiveTitle.Length == 0;
        return venue with
        {
            Sources = venue.Sources | VenueSources.Rolladeck,
            LiveConfirmedUntilUtc = confirmedUntil,
            DjLiveUntilUtc = confirmedUntil,
            LiveViewers = Math.Max(venue.LiveViewers, dj.ViewerCount),
            LiveHeadline = venue.LiveHeadline.Length > 0 ? venue.LiveHeadline : dj.NormalizedName,
            TwitchUrl = venue.TwitchUrl ?? NullIfEmpty(dj.TwitchUrl),
            LiveTitle = busiest && !string.IsNullOrEmpty(dj.NormalizedTitle) ? dj.NormalizedTitle : venue.LiveTitle,
            LiveGenres = busiest && dj.Genres.Count > 0 ? GenresOf(dj) : venue.LiveGenres,
        };
    }

    private static string[] GenresOf(LiveDjEntry dj)
    {
        var genres = new List<string>(dj.Genres.Count);
        for (var index = 0; index < dj.Genres.Count; index++)
        {
            VenueMapper.AddTag(genres, RolladeckText.Normalize(dj.Genres[index]));
        }

        return genres.ToArray();
    }

    private static VenueEvent? FromOpenVenue(OpenVenueEntry open, VenueAddress address, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(open.Name))
        {
            return null;
        }

        var entry = new DirectoryVenueEntry
        {
            Id = open.Id ?? open.Slug ?? open.Name,
            Name = open.Name,
            Slug = open.Slug,
            Server = open.Server,
            Datacenter = open.Datacenter,
            District = open.District,
            Ward = open.Ward,
            Plot = open.Plot,
            LogoUrl = open.LogoUrl,
            BannerUrl = open.BannerUrl,
            WebsiteOrCarrd = open.WebsiteOrCarrd,
            DiscordServer = open.DiscordServer,
            Lifestream = open.Lifestream,
            Amenities = open.Amenities,
        };
        var venue = VenueMapper.FromRolladeck(entry, nowUtc);
        return venue is null ? null : venue with { Address = address };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
