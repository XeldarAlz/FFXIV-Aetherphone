using Aetherphone.Core.Clock;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Clock.Widgets;

internal enum ClockSlotKind : byte
{
    Local,
    Eorzea,
    Server,
    City,
}

internal readonly struct ClockSlot
{
    public readonly ClockSlotKind Kind;
    public readonly string City;
    public readonly TimeZoneInfo Zone;

    public ClockSlot(ClockSlotKind kind, string city, TimeZoneInfo zone)
    {
        Kind = kind;
        City = city;
        Zone = zone;
    }

    public string Name => Kind switch
    {
        ClockSlotKind.Eorzea => Loc.T(L.Home.Eorzea),
        ClockSlotKind.Server => Loc.T(L.Clock.Server),
        ClockSlotKind.City => City,
        _ => Loc.T(L.Clock.Local),
    };
}

internal readonly struct ClockReading
{
    public readonly DateTime Moment;
    public readonly float Seconds;
    public readonly int DayOffset;
    public readonly int OffsetMinutes;

    public ClockReading(DateTime moment, float seconds, int dayOffset, int offsetMinutes)
    {
        Moment = moment;
        Seconds = seconds;
        DayOffset = dayOffset;
        OffsetMinutes = offsetMinutes;
    }

    public bool IsDaytime => Moment.Hour is >= 6 and < 18;
}

internal static class ClockZones
{
    public const string LocalValue = "local";
    public const string EorzeaValue = "eorzea";
    public const string ServerValue = "server";
    private const long EorzeaSecondsPerDay = 86400;
    private const int MinutesPerHour = 60;
    private static readonly DateTime EorzeaEpoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly Dictionary<string, TimeZoneInfo> Resolved = new(StringComparer.Ordinal);

    public static ClockSlot Local => new(ClockSlotKind.Local, string.Empty, TimeZoneInfo.Local);
    public static ClockSlot Eorzea => new(ClockSlotKind.Eorzea, string.Empty, TimeZoneInfo.Utc);
    public static ClockSlot Server => new(ClockSlotKind.Server, string.Empty, TimeZoneInfo.Utc);

    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        if (Resolved.TryGetValue(timeZoneId, out var zone))
        {
            return zone;
        }

        WorldClockCatalog.TryResolve(timeZoneId, out zone);
        Resolved[timeZoneId] = zone;
        return zone;
    }

    public static ClockSlot FromChoice(string value, List<WorldClockEntry> cities)
    {
        if (string.Equals(value, EorzeaValue, StringComparison.Ordinal))
        {
            return Eorzea;
        }

        if (string.Equals(value, ServerValue, StringComparison.Ordinal))
        {
            return Server;
        }

        if (value.Length == 0 || string.Equals(value, LocalValue, StringComparison.Ordinal))
        {
            return Local;
        }

        for (var index = 0; index < cities.Count; index++)
        {
            if (string.Equals(cities[index].City, value, StringComparison.Ordinal))
            {
                return new ClockSlot(ClockSlotKind.City, cities[index].City, Resolve(cities[index].TimeZoneId));
            }
        }

        var catalog = WorldClockCatalog.All;
        for (var index = 0; index < catalog.Count; index++)
        {
            if (string.Equals(catalog[index].City, value, StringComparison.Ordinal))
            {
                return new ClockSlot(ClockSlotKind.City, catalog[index].City, Resolve(catalog[index].TimeZoneId));
            }
        }

        return Local;
    }

    public static void Choices(List<WidgetChoice> target, List<WorldClockEntry> cities)
    {
        target.Add(new WidgetChoice(LocalValue, L.Clock.Local));
        target.Add(new WidgetChoice(EorzeaValue, L.Home.Eorzea));
        target.Add(new WidgetChoice(ServerValue, L.Clock.Server));
        for (var index = 0; index < cities.Count; index++)
        {
            target.Add(new WidgetChoice(cities[index].City, cities[index].City));
        }
    }

    public static int Fill(ClockSlot[] target, List<WorldClockEntry> cities, bool includeLocal)
    {
        var count = 0;
        if (includeLocal)
        {
            target[count++] = Local;
        }

        target[count++] = Eorzea;
        target[count++] = Server;
        for (var index = 0; index < cities.Count && count < target.Length; index++)
        {
            target[count++] = new ClockSlot(ClockSlotKind.City, cities[index].City,
                Resolve(cities[index].TimeZoneId));
        }

        return count;
    }

    public static int FillFaces(ClockSlot[] target, List<WorldClockEntry> cities)
    {
        var count = 0;
        target[count++] = Local;
        target[count++] = Eorzea;
        for (var index = 0; index < cities.Count && count < target.Length; index++)
        {
            target[count++] = new ClockSlot(ClockSlotKind.City, cities[index].City,
                Resolve(cities[index].TimeZoneId));
        }

        if (count < target.Length)
        {
            target[count++] = Server;
        }

        return count;
    }

    public static ClockReading Read(in ClockSlot slot, DateTime utcNow)
    {
        switch (slot.Kind)
        {
            case ClockSlotKind.Eorzea:
                var eorzeaSeconds = EorzeaTime.CurrentSeconds() % EorzeaSecondsPerDay;
                return new ClockReading(EorzeaEpoch.AddSeconds(eorzeaSeconds), eorzeaSeconds % 60, 0, 0);
            case ClockSlotKind.Local:
                var local = utcNow.ToLocalTime();
                return new ClockReading(local, local.Second + local.Millisecond / 1000f, 0, 0);
        }

        var moment = TimeZoneInfo.ConvertTimeFromUtc(utcNow, slot.Zone);
        var offset = slot.Zone.GetUtcOffset(utcNow) - TimeZoneInfo.Local.GetUtcOffset(utcNow);
        var dayOffset = (moment.Date - utcNow.ToLocalTime().Date).Days;
        return new ClockReading(moment, moment.Second + moment.Millisecond / 1000f, dayOffset,
            (int)offset.TotalMinutes);
    }

    public static string Detail(ref CachedText cache, in ClockSlot slot, in ClockReading reading)
    {
        switch (slot.Kind)
        {
            case ClockSlotKind.Eorzea:
                return Loc.T(L.Clock.InGame);
            case ClockSlotKind.Server:
                return "UTC";
            case ClockSlotKind.Local:
                var localOffset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
                return cache.IsCurrent(localOffset) ? cache.Value : cache.Store(localOffset, UtcLabel(localOffset));
        }

        var key = (reading.DayOffset + 2) * 100_000L + reading.OffsetMinutes + 50_000L;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CityLabel(reading));
    }

    public static string OffsetText(int offsetMinutes)
    {
        var sign = offsetMinutes < 0 ? "-" : "+";
        var magnitude = Math.Abs(offsetMinutes);
        var hours = magnitude / MinutesPerHour;
        var minutes = magnitude % MinutesPerHour;
        return minutes == 0
            ? string.Concat(sign, hours.ToString(Loc.Culture))
            : string.Concat(sign, hours.ToString(Loc.Culture), ":", minutes.ToString("D2", Loc.Culture));
    }

    public static string UtcLabel(int offsetMinutes) =>
        offsetMinutes == 0 ? "UTC" : string.Concat("UTC", OffsetText(offsetMinutes));

    private static string CityLabel(in ClockReading reading)
    {
        var day = reading.DayOffset switch
        {
            0 => Loc.T(L.Clock.DayToday),
            1 => Loc.T(L.Clock.DayTomorrow),
            -1 => Loc.T(L.Clock.DayYesterday),
            _ => reading.Moment.ToString("ddd", Loc.Culture),
        };
        if (reading.OffsetMinutes == 0)
        {
            return day;
        }

        return Loc.T(L.WidgetsTime.DayOffset, day, Loc.T(L.Time.HoursShort, OffsetText(reading.OffsetMinutes)));
    }
}
