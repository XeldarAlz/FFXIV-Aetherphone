using System.Text;

namespace Aetherphone.Core.Venues;

internal static class VenueMapper
{
    public static VenueEvent? FromFfxiv(FfxivVenueDto dto, DateTime nowUtc)
    {
        if (dto.Id is null || string.IsNullOrEmpty(dto.Name))
        {
            return null;
        }

        if (!TryResolveOpening(dto, nowUtc, out var startUtc, out var endUtc))
        {
            return null;
        }

        var location = dto.Location;
        var dataCenter = location?.DataCenter ?? string.Empty;
        var world = location?.World ?? string.Empty;
        var locationLine = BuildFfxivLocationLine(location, world);
        var teleport = BuildFfxivTeleport(location, world);
        var tags = CollectFfxivTags(dto);
        var description = dto.Description is { Length: > 0 } ? string.Join("\n\n", dto.Description) : string.Empty;
        var website = !string.IsNullOrEmpty(dto.Website) ? dto.Website : $"https://ffxivvenues.com/{dto.Id}";
        return new VenueEvent
        {
            Id = $"ffxiv:{dto.Id}",
            Source = VenueSource.FfxivVenues,
            Title = dto.Name,
            Host = string.Empty,
            Description = description,
            DataCenter = dataCenter,
            World = world,
            LocationLine = locationLine,
            TeleportCode = teleport,
            BannerUrl = dto.BannerUri,
            IconUrl = null,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Recurring = true,
            Tags = tags,
            Url = website,
            DiscordUrl = string.IsNullOrEmpty(dto.Discord) ? null : dto.Discord,
            AttendeeCount = 0,
        };
    }

    public static VenueEvent? FromPartake(PartakeEventDto dto)
    {
        if (dto.StartsAt is not { } starts || string.IsNullOrEmpty(dto.Title))
        {
            return null;
        }

        var dataCenter = dto.LocationData?.DataCenter?.Name ?? string.Empty;
        var world = dto.LocationData?.Server?.Name ?? string.Empty;
        var freeText = dto.Location ?? string.Empty;
        var locationLine = BuildPartakeLocationLine(world, freeText);
        return new VenueEvent
        {
            Id = $"partake:{dto.Id}",
            Source = VenueSource.Partake,
            Title = dto.Title,
            Host = dto.Team?.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            DataCenter = dataCenter,
            World = world,
            LocationLine = locationLine,
            TeleportCode = null,
            BannerUrl = null,
            IconUrl = dto.Team?.IconUrl,
            StartUtc = starts.UtcDateTime,
            EndUtc = dto.EndsAt?.UtcDateTime,
            Recurring = false,
            Tags = CollectPartakeTags(dto),
            Url = $"https://www.partake.gg/events/{dto.Id}",
            DiscordUrl = string.IsNullOrEmpty(dto.Team?.DiscordUrl) ? null : dto.Team!.DiscordUrl,
            AttendeeCount = dto.AttendeeCount,
        };
    }

    private static bool TryResolveOpening(FfxivVenueDto dto, DateTime nowUtc, out DateTime startUtc,
        out DateTime? endUtc)
    {
        startUtc = default;
        endUtc = null;
        var found = false;
        var bestStart = DateTime.MaxValue;
        DateTime? bestEnd = null;
        if (dto.Schedule is { } schedule)
        {
            for (var index = 0; index < schedule.Length; index++)
            {
                var resolution = schedule[index].Resolution;
                if (resolution?.Start is not { } start)
                {
                    continue;
                }

                var startValue = start.UtcDateTime;
                DateTime? endValue = resolution.End?.UtcDateTime;
                if (!IsRelevant(startValue, endValue, resolution.IsNow, nowUtc))
                {
                    continue;
                }

                if (startValue < bestStart)
                {
                    bestStart = startValue;
                    bestEnd = endValue;
                    found = true;
                }
            }
        }

        if (dto.ScheduleOverrides is { } overrides)
        {
            for (var index = 0; index < overrides.Length; index++)
            {
                var entry = overrides[index];
                if (!entry.Open || entry.Start is not { } start)
                {
                    continue;
                }

                var startValue = start.UtcDateTime;
                DateTime? endValue = entry.End?.UtcDateTime;
                if (!IsRelevant(startValue, endValue, false, nowUtc))
                {
                    continue;
                }

                if (startValue < bestStart)
                {
                    bestStart = startValue;
                    bestEnd = endValue;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        startUtc = bestStart;
        endUtc = bestEnd;
        return true;
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

    private static string BuildPartakeLocationLine(string world, string freeText)
    {
        if (world.Length > 0 && freeText.Length > 0)
        {
            return $"{freeText}";
        }

        return freeText.Length > 0 ? freeText : world;
    }

    private static IReadOnlyList<string> CollectFfxivTags(FfxivVenueDto dto)
    {
        var tags = new List<string>();
        if (dto.Tags is { } source)
        {
            for (var index = 0; index < source.Length; index++)
            {
                AddTag(tags, source[index]);
            }
        }

        AddTag(tags, dto.Sfw ? "SFW" : "18+");
        return tags;
    }

    private static IReadOnlyList<string> CollectPartakeTags(PartakeEventDto dto)
    {
        var tags = new List<string>();
        if (dto.Tags is { } source)
        {
            for (var index = 0; index < source.Length; index++)
            {
                AddTag(tags, source[index]);
            }
        }

        if (IsAdult(dto.AgeRating))
        {
            AddTag(tags, "18+");
        }

        return tags;
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

    private static void AddTag(List<string> tags, string? tag)
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
}
