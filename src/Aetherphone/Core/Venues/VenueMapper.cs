using System.Globalization;
using System.Text;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Rolladeck;

namespace Aetherphone.Core.Venues;

internal static class VenueMapper
{
    public const string AdultTag = "18+";
    public const string SafeTag = "SFW";

    private static readonly Dictionary<string, string> AmenityLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dj"] = "DJ",
        ["bar"] = "Bar",
        ["lgbtqia"] = "LGBTQIA+",
        ["rp"] = "RP",
        ["vip"] = "VIP",
        ["courts"] = "Courtesans",
        ["bard"] = "Bards",
    };

    public static VenueEvent? FromFfxiv(FfxivVenueDto dto, DateTime nowUtc)
    {
        if (dto.Id is null || string.IsNullOrEmpty(dto.Name))
        {
            return null;
        }

        TryResolveOpening(dto, nowUtc, out var startUtc, out var endUtc);
        var location = dto.Location;
        var world = location?.World ?? string.Empty;
        var locationLine = BuildFfxivLocationLine(location, world);
        var description = dto.Description is { Length: > 0 } ? string.Join("\n\n", dto.Description) : string.Empty;
        return new VenueEvent
        {
            Id = $"ffxiv:{dto.Id}",
            Sources = VenueSources.FfxivVenues,
            Title = dto.Name,
            Host = string.Empty,
            Description = description,
            DataCenter = location?.DataCenter ?? string.Empty,
            World = world,
            LocationLine = locationLine,
            PlaceLine = BuildPlaceLine(world, locationLine),
            TeleportCode = BuildFfxivTeleport(location, world),
            BannerUrl = NullIfEmpty(dto.BannerUri),
            LogoUrl = null,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Tags = CollectFfxivTags(dto),
            WebsiteUrl = NullIfEmpty(dto.Website),
            DiscordUrl = NullIfEmpty(dto.Discord),
            ListingUrl = $"https://ffxivvenues.com/venue/{dto.Id}",
            AttendeeCount = 0,
            Address = location is null
                ? default
                : VenueAddress.Of(world, location.District, location.Ward, location.Plot),
        };
    }

    public static VenueEvent? FromPartake(PartakeEventDto dto)
    {
        if (dto.StartsAt is not { } starts || string.IsNullOrEmpty(dto.Title))
        {
            return null;
        }

        var world = dto.LocationData?.Server?.Name ?? string.Empty;
        var freeText = dto.Location ?? string.Empty;
        var locationLine = freeText.Length > 0 ? freeText : world;
        var startUtc = starts.UtcDateTime;
        var endUtc = dto.EndsAt?.UtcDateTime;
        return new VenueEvent
        {
            Id = $"partake:{dto.Id}",
            Sources = VenueSources.Partake,
            Title = dto.Title,
            Host = dto.Team?.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            DataCenter = dto.LocationData?.DataCenter?.Name ?? string.Empty,
            World = world,
            LocationLine = locationLine,
            PlaceLine = BuildPlaceLine(world, locationLine),
            TeleportCode = null,
            BannerUrl = null,
            LogoUrl = NullIfEmpty(dto.Team?.IconUrl),
            StartUtc = startUtc,
            EndUtc = endUtc,
            Tags = CollectPartakeTags(dto),
            WebsiteUrl = NullIfEmpty(dto.Team?.WebsiteUrl),
            DiscordUrl = NullIfEmpty(dto.Team?.DiscordUrl),
            ListingUrl = $"https://www.partake.gg/events/{dto.Id}",
            AttendeeCount = dto.AttendeeCount,
            EventStartUtc = startUtc,
            EventEndUtc = endUtc,
        };
    }

    public static VenueEvent? FromRolladeck(DirectoryVenueEntry entry, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Name))
        {
            return null;
        }

        var world = entry.Server ?? string.Empty;
        var locationLine = BuildHousingLine(entry.District, entry.Ward, entry.Plot);
        var eventName = TryNextDiscordEvent(entry.UpcomingDiscordEvents, nowUtc, out var startUtc, out var endUtc);
        return new VenueEvent
        {
            Id = $"rolladeck:{entry.Id}",
            Sources = VenueSources.Rolladeck,
            Title = RolladeckText.Normalize(entry.Name),
            Host = string.Empty,
            Description = string.Empty,
            DataCenter = DataCenterOf(entry.Datacenter, world),
            World = world,
            LocationLine = locationLine,
            PlaceLine = BuildPlaceLine(world, locationLine),
            TeleportCode = RolladeckText.TeleportDestination(entry.Lifestream),
            BannerUrl = NullIfEmpty(entry.BannerUrl),
            LogoUrl = NullIfEmpty(entry.LogoUrl),
            StartUtc = startUtc,
            EndUtc = endUtc,
            Tags = CollectAmenityTags(entry.Amenities, entry.Sfw),
            WebsiteUrl = NullIfEmpty(entry.WebsiteOrCarrd),
            DiscordUrl = NullIfEmpty(entry.DiscordServer),
            ListingUrl = null,
            AttendeeCount = 0,
            Address = VenueAddress.Of(world, entry.District, entry.Ward, entry.Plot),
            RolladeckUrl = RolladeckVenueUrl(entry.Slug),
            EventStartUtc = startUtc,
            EventEndUtc = endUtc,
            EventName = eventName,
        };
    }

    public static string? RolladeckVenueUrl(string? slug) =>
        string.IsNullOrEmpty(slug) ? null : $"https://xivrolladeck.com/venue/{slug}";

    private static string DataCenterOf(string? dataCenter, string world)
    {
        if (!string.IsNullOrEmpty(dataCenter))
        {
            return dataCenter;
        }

        return WorldGeography.DataCenterOfWorld(world)?.Name ?? string.Empty;
    }

    public static string BuildPlaceLine(string world, string locationLine)
    {
        if (locationLine.Length == 0 || string.Equals(locationLine, world, StringComparison.Ordinal))
        {
            return world;
        }

        return world.Length > 0 ? $"{world} · {locationLine}" : locationLine;
    }

    public static bool TryParseUtc(string? value, out DateTime utc)
    {
        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed))
        {
            utc = parsed.UtcDateTime;
            return true;
        }

        utc = default;
        return false;
    }

    private static string TryNextDiscordEvent(List<DirectoryEventEntry> events, DateTime nowUtc,
        out DateTime? startUtc, out DateTime? endUtc)
    {
        startUtc = null;
        endUtc = null;
        var name = string.Empty;
        for (var index = 0; index < events.Count; index++)
        {
            var entry = events[index];
            if (!TryParseUtc(entry.StartTime, out var start))
            {
                continue;
            }

            DateTime? end = TryParseUtc(entry.EndTime, out var parsedEnd) ? parsedEnd : null;
            if (!IsRelevant(start, end, false, nowUtc) || (startUtc is { } best && start >= best))
            {
                continue;
            }

            startUtc = start;
            endUtc = end;
            name = RolladeckText.Normalize(entry.Name);
        }

        return name;
    }

    private static void TryResolveOpening(FfxivVenueDto dto, DateTime nowUtc, out DateTime? startUtc,
        out DateTime? endUtc)
    {
        startUtc = null;
        endUtc = null;
        if (dto.Schedule is { } schedule)
        {
            for (var index = 0; index < schedule.Length; index++)
            {
                var resolution = schedule[index].Resolution;
                if (resolution?.Start is not { } start)
                {
                    continue;
                }

                Consider(start.UtcDateTime, resolution.End?.UtcDateTime, resolution.IsNow, nowUtc, ref startUtc,
                    ref endUtc);
            }
        }

        if (dto.ScheduleOverrides is not { } overrides)
        {
            return;
        }

        for (var index = 0; index < overrides.Length; index++)
        {
            var entry = overrides[index];
            if (!entry.Open || entry.Start is not { } start)
            {
                continue;
            }

            Consider(start.UtcDateTime, entry.End?.UtcDateTime, false, nowUtc, ref startUtc, ref endUtc);
        }
    }

    private static void Consider(DateTime start, DateTime? end, bool isNow, DateTime nowUtc, ref DateTime? bestStart,
        ref DateTime? bestEnd)
    {
        if (!IsRelevant(start, end, isNow, nowUtc))
        {
            return;
        }

        if (bestStart is { } best && start >= best)
        {
            return;
        }

        bestStart = start;
        bestEnd = end;
    }

    private static bool IsRelevant(DateTime startUtc, DateTime? endUtc, bool isNow, DateTime nowUtc)
    {
        if (isNow)
        {
            return true;
        }

        if (endUtc is { } end)
        {
            return end > nowUtc;
        }

        return startUtc > nowUtc;
    }

    private static string BuildFfxivLocationLine(FfxivLocationDto? location, string world)
    {
        if (location is null)
        {
            return world;
        }

        if (!string.IsNullOrEmpty(location.Override))
        {
            return location.Override;
        }

        var district = location.District ?? string.Empty;
        if (location.Ward <= 0)
        {
            return district.Length > 0 ? district : world;
        }

        var builder = new StringBuilder();
        if (district.Length > 0)
        {
            builder.Append(district).Append(", ");
        }

        builder.Append('W').Append(location.Ward);
        if (location.Plot > 0)
        {
            builder.Append(", P").Append(location.Plot);
        }
        else if (location.Apartment > 0)
        {
            builder.Append(", Apt ").Append(location.Apartment);
        }

        return builder.ToString();
    }

    private static string BuildHousingLine(string? district, int? ward, int? plot)
    {
        var name = district ?? string.Empty;
        if (ward is not > 0)
        {
            return name;
        }

        var prefix = name.Length > 0 ? $"{name}, " : string.Empty;
        return plot is > 0 ? $"{prefix}W{ward}, P{plot}" : $"{prefix}W{ward}";
    }

    private static string? BuildFfxivTeleport(FfxivLocationDto? location, string world)
    {
        if (location is null || string.IsNullOrEmpty(world) || location.Ward <= 0 ||
            string.IsNullOrEmpty(location.District))
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(world).Append(' ').Append(location.District).Append(" W").Append(location.Ward);
        if (location.Plot > 0)
        {
            builder.Append(" P").Append(location.Plot);
        }
        else if (location.Apartment > 0)
        {
            builder.Append(" A").Append(location.Apartment);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> CollectFfxivTags(FfxivVenueDto dto)
    {
        var tags = new List<string>();
        AddTag(tags, dto.Sfw ? SafeTag : AdultTag);
        if (dto.Tags is { } source)
        {
            for (var index = 0; index < source.Length; index++)
            {
                AddTag(tags, source[index]);
            }
        }

        return tags;
    }

    private static IReadOnlyList<string> CollectPartakeTags(PartakeEventDto dto)
    {
        var tags = new List<string>();
        if (IsAdult(dto.AgeRating))
        {
            AddTag(tags, AdultTag);
        }

        if (dto.Tags is { } source)
        {
            for (var index = 0; index < source.Length; index++)
            {
                AddTag(tags, source[index]);
            }
        }

        return tags;
    }

    private static IReadOnlyList<string> CollectAmenityTags(List<string> amenities, bool? sfw)
    {
        var tags = new List<string>();
        var adult = sfw == false || Contains(amenities, "nsfw");
        if (adult)
        {
            AddTag(tags, AdultTag);
        }
        else if (sfw == true || Contains(amenities, "sfw"))
        {
            AddTag(tags, SafeTag);
        }

        for (var index = 0; index < amenities.Count; index++)
        {
            var amenity = amenities[index];
            if (string.Equals(amenity, "nsfw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(amenity, "sfw", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddTag(tags, AmenityLabel(amenity));
        }

        return tags;
    }

    private static string AmenityLabel(string amenity)
    {
        if (AmenityLabels.TryGetValue(amenity, out var label))
        {
            return label;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(amenity.ToLowerInvariant());
    }

    private static bool Contains(List<string> values, string target)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAdult(string? ageRating)
    {
        if (string.IsNullOrEmpty(ageRating))
        {
            return false;
        }

        return ageRating.Contains("ADULT", StringComparison.OrdinalIgnoreCase) ||
               ageRating.Contains("MATURE", StringComparison.OrdinalIgnoreCase) ||
               ageRating.Contains("18", StringComparison.Ordinal);
    }

    public static void AddTag(List<string> tags, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var trimmed = tag.Trim();
        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        tags.Add(trimmed);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
