using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;

namespace Aetherphone.Core.Muster;

internal static class MusterText
{
    private const int CacheCapacity = 512;

    private static readonly Dictionary<string, string> PlaceById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> IdentityById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> WorldLineById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> HousingById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> CoordinatesById = new(StringComparer.Ordinal);
    private static string cachedLanguageCode = string.Empty;

    public static SharedLocation Location(MusterDto muster) =>
        new((uint)muster.TerritoryId, (uint)muster.MapId, muster.MapX, muster.MapY, (uint)muster.WorldId,
            (short)muster.Ward, (short)muster.Plot, (short)muster.Room);

    public static string Place(MusterDto muster)
    {
        EnsureLanguage();
        if (PlaceById.TryGetValue(muster.Id, out var cached))
        {
            return cached;
        }

        var zone = LocationShare.ZoneName((uint)muster.TerritoryId);
        var world = LocationShare.WorldName((uint)muster.WorldId);
        string place;
        if (zone.Length > 0 && world.Length > 0)
        {
            place = $"{zone} · {world}";
        }
        else if (zone.Length > 0)
        {
            place = zone;
        }
        else if (world.Length > 0)
        {
            place = world;
        }
        else
        {
            place = muster.Spot;
        }

        Store(PlaceById, muster.Id, place);
        return place;
    }

    public static string Identity(MusterDto muster)
    {
        EnsureLanguage();
        if (IdentityById.TryGetValue(muster.Id, out var cached))
        {
            return cached;
        }

        var identity = muster.HostWorld.Length > 0
            ? $"{muster.HostCharacter} · {muster.HostWorld}"
            : muster.HostCharacter;
        Store(IdentityById, muster.Id, identity);
        return identity;
    }

    public static string WorldLine(MusterDto muster)
    {
        EnsureLanguage();
        if (WorldLineById.TryGetValue(muster.Id, out var cached))
        {
            return cached;
        }

        var location = Location(muster);
        var line = LocationShare.WorldLine(in location);
        Store(WorldLineById, muster.Id, line);
        return line;
    }

    public static string HousingLine(MusterDto muster)
    {
        EnsureLanguage();
        if (HousingById.TryGetValue(muster.Id, out var cached))
        {
            return cached;
        }

        var location = Location(muster);
        var line = muster.Ward > 0 ? LocationShare.HousingLine(in location) : string.Empty;
        Store(HousingById, muster.Id, line);
        return line;
    }

    public static string Coordinates(MusterDto muster)
    {
        EnsureLanguage();
        if (CoordinatesById.TryGetValue(muster.Id, out var cached))
        {
            return cached;
        }

        var location = Location(muster);
        var line = LocationShare.CoordinateText(in location);
        Store(CoordinatesById, muster.Id, line);
        return line;
    }

    public static string Span(long seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var minutes = (int)((seconds + 59) / 60);
        var hours = minutes / 60;
        minutes %= 60;
        if (hours > 0 && minutes > 0)
        {
            return Loc.T(L.Muster.DurationHoursMinutes, hours, minutes);
        }

        if (hours > 0)
        {
            return Loc.T(L.Muster.DurationHours, hours);
        }

        return Loc.T(L.Muster.DurationMinutes, minutes);
    }

    private static void EnsureLanguage()
    {
        var code = Loc.Current.Code;
        if (string.Equals(code, cachedLanguageCode, StringComparison.Ordinal))
        {
            return;
        }

        cachedLanguageCode = code;
        PlaceById.Clear();
        IdentityById.Clear();
        WorldLineById.Clear();
        HousingById.Clear();
        CoordinatesById.Clear();
    }

    private static void Store(Dictionary<string, string> cache, string key, string value)
    {
        if (cache.Count >= CacheCapacity)
        {
            cache.Clear();
        }

        cache[key] = value;
    }
}
