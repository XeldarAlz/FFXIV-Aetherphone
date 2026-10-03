using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueMergerTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc);

    private static VenueEvent Listing(string id, string title, int ward = 25, int plot = 28,
        DateTime? start = null, DateTime? end = null, string? banner = null) =>
        new()
        {
            Id = id,
            Sources = VenueSources.FfxivVenues,
            Title = title,
            Host = string.Empty,
            Description = string.Empty,
            DataCenter = "Light",
            World = "Shiva",
            LocationLine = $"Lavender Beds, W{ward}, P{plot}",
            PlaceLine = $"Shiva · Lavender Beds, W{ward}, P{plot}",
            TeleportCode = null,
            BannerUrl = banner,
            LogoUrl = null,
            StartUtc = start,
            EndUtc = end,
            Tags = Array.Empty<string>(),
            WebsiteUrl = null,
            DiscordUrl = null,
            ListingUrl = null,
            AttendeeCount = 0,
            Address = VenueAddress.Of("Shiva", "Lavender Beds", ward, plot),
        };

    private static DirectoryVenueEntry Directory(string id, string name, string district = "The Lavender Beds",
        int ward = 25, int plot = 28) =>
        new()
        {
            Id = id,
            Name = name,
            Slug = id,
            Server = "shiva",
            Datacenter = "Light",
            District = district,
            Ward = ward,
            Plot = plot,
            LogoUrl = "https://example.com/logo.png",
            BannerUrl = "https://example.com/banner.png",
            Lifestream = "/li Shiva Lavender Beds 25 28",
            Amenities = ["dj", "lgbtqia", "nsfw"],
        };

    private static OpenVenueEntry Open(string name, int ward = 25, int plot = 28, string? dj = "DJ Nyx") =>
        new()
        {
            Name = name,
            Slug = "open-slug",
            Server = "Shiva",
            Datacenter = "Light",
            District = "Lavender Beds",
            Ward = ward,
            Plot = plot,
            DjName = dj,
            DjTwitch = "https://www.twitch.tv/nyx",
        };

    [Fact]
    public void Merge_JoinsDirectoryEntryOntoListingByAddress()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise")], [Directory("r1", "Paradise Nightclub")], [],
            [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.Equal("ffxiv:a", venue.Id);
        Assert.Equal(VenueSources.FfxivVenues | VenueSources.Rolladeck, venue.Sources);
        Assert.Equal("https://example.com/banner.png", venue.BannerUrl);
        Assert.Equal("https://example.com/logo.png", venue.LogoUrl);
        Assert.Equal("Shiva Lavender Beds 25 28", venue.TeleportCode);
        Assert.Equal("https://xivrolladeck.com/venue/r1", venue.RolladeckUrl);
    }

    [Fact]
    public void Merge_KeepsListingBannerOverDirectoryBanner()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise", banner: "https://own/banner.png")],
            [Directory("r1", "Paradise")], [], [], Now, Now);

        Assert.Equal("https://own/banner.png", Assert.Single(merged).BannerUrl);
    }

    [Fact]
    public void Merge_AddsDirectoryOnlyVenuesWithAmenityTags()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise")], [Directory("r2", "Voidsent", ward: 3, plot: 7)],
            [], [], Now, Now);

        Assert.Equal(2, merged.Length);
        var added = merged[1];
        Assert.Equal("rolladeck:r2", added.Id);
        Assert.Equal(VenueSources.Rolladeck, added.Sources);
        Assert.False(added.HasOpening);
        Assert.Equal(["18+", "DJ", "LGBTQIA+"], added.Tags);
    }

    [Fact]
    public void Merge_OpenVenueConfirmsLiveUntilConfirmationLapses()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise")], [], [Open("Paradise")], [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.True(venue.IsConfirmedLive(Now));
        Assert.Equal(VenueLiveState.Confirmed, venue.LiveState(Now));
        Assert.Equal("DJ Nyx", venue.LiveHeadline);
        Assert.Equal("https://www.twitch.tv/nyx", venue.TwitchUrl);
        Assert.False(venue.IsConfirmedLive(Now + VenueMerger.ConfirmationLifetime + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Merge_OpenVenueWithoutListingCreatesVenue()
    {
        var merged = MergeEvents([], [], [Open("Club Nova", ward: 9, plot: 12)], [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.Equal("Club Nova", venue.Title);
        Assert.True(venue.IsConfirmedLive(Now));
    }

    [Fact]
    public void Merge_LiveDjAtAddressAddsViewers()
    {
        var dj = new LiveDjEntry
        {
            DjName = "DJ Yams",
            Server = "Shiva",
            District = "Lavender Beds",
            Ward = 25,
            Plot = 28,
            ViewerCount = 56,
            TwitchUrl = "https://www.twitch.tv/yams",
        };

        var venue = Assert.Single(MergeEvents([Listing("ffxiv:a", "Paradise")], [], [], [dj], Now, Now));

        Assert.Equal(56, venue.LiveViewers);
        Assert.Equal("DJ Yams", venue.LiveHeadline);
        Assert.True(venue.IsConfirmedLive(Now));
    }

    [Fact]
    public void Merge_EntriesWithoutFullAddressNeverJoin()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise")], [Directory("r1", "Other", plot: 0)], [], [],
            Now, Now);

        Assert.Equal(2, merged.Length);
    }

    [Fact]
    public void ScheduledOpen_WithoutEnd_ExpiresAfterFourHours()
    {
        var venue = Listing("ffxiv:a", "Paradise", start: Now.AddHours(-1));

        Assert.True(venue.IsScheduledOpen(Now));
        Assert.False(venue.IsScheduledOpen(Now.AddHours(4)));
    }

    [Fact]
    public void Mapper_KeepsFfxivVenueWithoutScheduledOpening()
    {
        var dto = new FfxivVenueDto
        {
            Id = "abc",
            Name = "Quiet Lounge",
            Location = new FfxivLocationDto { World = "Shiva", DataCenter = "Light", District = "Mist", Ward = 4, Plot = 9 },
        };

        var venue = VenueMapper.FromFfxiv(dto, Now);

        Assert.NotNull(venue);
        Assert.False(venue.HasOpening);
        Assert.Equal("Shiva · Mist, W4, P9", venue.PlaceLine);
        Assert.Equal("Shiva Mist W4 P9", venue.TeleportCode);
        Assert.True(venue.Address.IsKnown);
    }

    [Theory]
    [InlineData("/li Shiva Lavender Beds 25 28", "Shiva Lavender Beds 25 28")]
    [InlineData("Shiva Lavender Beds 25 28", "Shiva Lavender Beds 25 28")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void TeleportDestination_StripsCommandPrefix(string? input, string? expected) =>
        Assert.Equal(expected, RolladeckText.TeleportDestination(input));

    [Fact]
    public void Query_DirectoryPutsLiveFirstThenSortsByTitle()
    {
        var scheduled = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2, start: Now.AddMinutes(-30), end: Now.AddHours(2));
        var confirmedSource = Listing("ffxiv:c", "Charlie", ward: 3, plot: 3);
        var idle = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1);
        var idleToo = Listing("ffxiv:d", "Delta", ward: 4, plot: 4);
        var merged = MergeEvents([scheduled, confirmedSource, idleToo, idle], [],
            [Open("Charlie", ward: 3, plot: 3)], [], Now, Now);
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, null, false, 0, 0,
            string.Empty, 0);

        query.Update(key, merged, [], [], Now);

        Assert.Equal(["Charlie", "Bravo", "Alpha", "Delta"], Titles(query.Feed));
    }

    [Fact]
    public void Query_CategoryAndWorldNarrowTheFeed()
    {
        var bar = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1) with { Tags = ["Bar"] };
        var club = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2) with { Tags = ["Nightclub"] };
        var elsewhere = Listing("ffxiv:c", "Charlie", ward: 3, plot: 3) with { Tags = ["Bar"], World = "Odin" };
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, null, false, 0, 0,
            string.Empty, 0, VenueCategories.Bars, "Shiva");

        query.Update(key, [bar, club, elsewhere], [], [], Now);

        Assert.Equal(["Alpha"], Titles(query.Feed));
    }

    [Fact]
    public void Sections_SplitLiveLaterTodayEventsAndFavorites()
    {
        var localNow = Now.ToLocalTime();
        var laterToday = localNow.Date.AddHours(23).AddMinutes(30).ToUniversalTime();
        var live = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1, start: Now.AddMinutes(-10), end: Now.AddHours(2));
        var later = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2, start: laterToday, end: laterToday.AddHours(2),
            banner: "https://banner") with { Tags = ["Bath house"] };
        var party = Listing("partake:1", "Beach Rave", ward: 3, plot: 3, start: Now.AddDays(3)) with
        {
            EventStartUtc = Now.AddDays(3),
        };
        var sections = new VenueSections();
        var key = new VenueSectionsKey(1, VenueFilter.SourceAll, null, string.Empty, "Shiva", 0, 0, 0);

        sections.Update(key, [live, later, party], ["ffxiv:b"], [], Now);

        Assert.Equal(["Alpha"], Titles(sections.Live));
        Assert.True(sections.FeaturedIsLive);
        Assert.Equal(["Alpha"], Titles(sections.Featured));
        if (laterToday > Now)
        {
            Assert.Equal(["Bravo"], Titles(sections.LaterToday));
            Assert.Equal(["Bravo"], Titles(sections.LaterRail));
        }

        Assert.Equal(["Beach Rave"], Titles(sections.Events));
        Assert.Equal(["Bravo"], Titles(sections.Saved));
        Assert.Equal(3, sections.NearYou.Count);
        Assert.Equal(1, sections.CategoryCount(VenueCategories.BathHouses));
    }

    [Fact]
    public void TagIndex_CountsTagsSkipsRatingsAndHonorsHideAdult()
    {
        var adultBar = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1) with { Tags = ["18+", "Bar", "DJ"] };
        var safeBar = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2) with { Tags = ["SFW", "Bar"] };
        var index = new VenueTagIndex();

        index.Update(new VenueTagIndexKey(1, VenueFilter.SourceAll, null, string.Empty, false, 0), [adultBar, safeBar],
            ["DJ"]);

        Assert.Equal([new VenueTagCount("Bar", 2), new VenueTagCount("DJ", 1)], index.Entries);
        Assert.Equal(1, index.MatchCount);

        index.Update(new VenueTagIndexKey(1, VenueFilter.SourceAll, null, string.Empty, true, 0), [adultBar, safeBar], []);

        Assert.Equal([new VenueTagCount("Bar", 1)], index.Entries);
        Assert.Equal(1, index.MatchCount);
    }

    [Fact]
    public void Merge_CarriesStreamTitleAndGenresOntoVenueAndDj()
    {
        var dj = new LiveDjEntry
        {
            DjName = "DJ Nyx", Server = "Shiva", District = "Lavender Beds", Ward = 25, Plot = 28, ViewerCount = 56,
            StreamTitle = "70s 80s 90s night", Genres = ["80s", "Disco", "80s"],
        };
        dj.InitNormalized();

        var snapshot = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [], [], [dj], Now, Now);

        var venue = Assert.Single(snapshot.Events);
        Assert.Equal("70s 80s 90s night", venue.LiveTitle);
        Assert.Equal(["80s", "Disco"], venue.LiveGenres);
        Assert.True(venue.HasLiveDj(Now));
        Assert.False(venue.HasLiveDj(Now + VenueMerger.ConfirmationLifetime));
        Assert.Equal(["80s", "Disco"], snapshot.Djs[0].Genres);
    }

    [Fact]
    public void Merge_ListsOnlyLiveDjsLinkedToAVenue()
    {
        var atVenue = new LiveDjEntry
        {
            DjName = "DJ Yams", Server = "Shiva", Datacenter = "Light", District = "Lavender Beds", Ward = 25, Plot = 28,
            ViewerCount = 12,
        };
        var roaming = new LiveDjEntry { DjName = "DJ Solo", Datacenter = "Aether", ViewerCount = 90 };

        var djs = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [], [], [atVenue, roaming], Now, Now).Djs;

        var linked = Assert.Single(djs);
        Assert.Equal("DJ Yams", linked.Name);
        Assert.Equal("ffxiv:a", linked.VenueId);
    }

    [Fact]
    public void Scope_RegionSetAndWorldNarrowVenues()
    {
        var light = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1);
        var chaos = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2) with { DataCenter = "Chaos", World = "Omega" };
        var aether = Listing("ffxiv:c", "Charlie", ward: 3, plot: 3) with { DataCenter = "Aether", World = "Gilgamesh" };
        var europe = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Light", "Chaos" };
        var query = new VenueQuery();
        query.Update(new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, europe, false, 0, 0,
            string.Empty, 0, World: "Omega"), [light, chaos, aether], [], [], Now);

        Assert.Equal(["Bravo"], Titles(query.Feed));
    }

    [Fact]
    public void Mapper_MarksRolladeckDiscordEventsAsEvents()
    {
        var entry = Directory("r9", "Club Glo");
        entry.UpcomingDiscordEvents = [new DirectoryEventEntry
        {
            Name = "Deimos @ Club Glo",
            StartTime = "2026-10-02T01:00:00+00:00",
            EndTime = "2026-10-02T02:00:00+00:00",
        }];

        var venue = VenueMapper.FromRolladeck(entry, Now);

        Assert.NotNull(venue);
        Assert.True(venue.IsEvent);
        Assert.Equal("Deimos @ Club Glo", venue.EventName);
        Assert.Equal(new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc), venue.StartUtc);
    }

    [Fact]
    public void Merge_KeepsRolladeckDiscordEventWhenListingHasItsOwnSchedule()
    {
        var opening = Now.AddHours(20);
        var listing = Listing("ffxiv:a", "Club Glo", start: opening, end: opening.AddHours(4));
        var entry = Directory("r9", "Club Glo");
        entry.UpcomingDiscordEvents = [new DirectoryEventEntry
        {
            Name = "Deimos @ Club Glo",
            StartTime = "2026-10-02T01:00:00+00:00",
            EndTime = "2026-10-02T02:00:00+00:00",
        }];

        var venue = Assert.Single(MergeEvents([listing], [entry], [], [], Now, Now));

        Assert.Equal(opening, venue.StartUtc);
        Assert.True(venue.IsEvent);
        Assert.Equal("Deimos @ Club Glo", venue.EventName);
        Assert.Equal(new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc), venue.EventStartUtc);
        Assert.Equal(new DateTime(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc), venue.EventEndUtc);

        var sections = new VenueSections();
        sections.Update(new VenueSectionsKey(1, VenueFilter.SourceAll, null, string.Empty, string.Empty, 0, 0, 0),
            [venue], [], [], Now);

        Assert.Equal(["Club Glo"], Titles(sections.Events));
    }

    [Fact]
    public void Query_LiveFilterKeepsOnlyLiveAndSkipsRebuildForSameKey()
    {
        var live = Listing("ffxiv:b", "Bravo", start: Now.AddMinutes(-5), end: Now.AddHours(1));
        var later = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1, start: Now.AddHours(5), end: Now.AddHours(8));
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.LiveNow, VenueFilter.SourceAll, null, false, 0, 0,
            string.Empty, 0);

        Assert.True(query.Update(key, [live, later], [], [], Now));
        Assert.False(query.Update(key, [live, later], [], [], Now));
        Assert.Equal(["Bravo"], Titles(query.Feed));
    }

    [Fact]
    public void Query_SearchMatchesLiveHeadline()
    {
        var merged = MergeEvents([Listing("ffxiv:a", "Paradise")], [], [Open("Paradise")], [], Now, Now);
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, null, false, 0, 0, "nyx", 0);

        query.Update(key, merged, [], [], Now);

        Assert.Equal(["Paradise"], Titles(query.Feed));
    }

    private static VenueEvent[] MergeEvents(IReadOnlyList<VenueEvent> listings,
        IReadOnlyList<DirectoryVenueEntry> directory, IReadOnlyList<OpenVenueEntry> openVenues,
        IReadOnlyList<LiveDjEntry> liveDjs, DateTime liveFetchedUtc, DateTime nowUtc) =>
        VenueMerger.Merge(listings, directory, openVenues, liveDjs, liveFetchedUtc, nowUtc).Events;

    private static string[] Titles(IReadOnlyList<VenueEvent> venues)
    {
        var titles = new string[venues.Count];
        for (var index = 0; index < venues.Count; index++)
        {
            titles[index] = venues[index].Title;
        }

        return titles;
    }
}
