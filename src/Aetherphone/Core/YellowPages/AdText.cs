using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.YellowPages;

internal readonly record struct AdOpenState(bool IsOpen, long ClosesAtUnix, long NextOpeningUnix);

/// <summary>Formatting and schedule math for ads. Schedules are stored as UTC weekly slots; every
/// user-facing time renders through TimeText so it lands in the viewer's clock.</summary>
internal static class AdText
{
    private const int MinutesPerWeek = 7 * 1440;

    public static SharedLocation Location(AdDto ad) =>
        new((uint)ad.TerritoryId, (uint)ad.MapId, ad.MapX, ad.MapY, (uint)ad.WorldId,
            (short)ad.Ward, (short)ad.Plot, 0);

    public static string PlaceLine(AdDto ad)
    {
        var zone = LocationShare.ZoneName((uint)ad.TerritoryId);
        var world = LocationShare.WorldName((uint)ad.WorldId);
        if (zone.Length > 0 && world.Length > 0)
        {
            return $"{zone} · {world}";
        }

        return zone.Length > 0 ? zone : world;
    }

    public static string Gil(long value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    public static string PriceLine(AdDto ad)
    {
        if (ad.Archetype != AdArchetypes.Service)
        {
            return string.Empty;
        }

        return ad.PriceMode switch
        {
            AdPriceModes.Fixed => Loc.T(L.YellowPages.PriceGil, Gil(ad.PriceGil)),
            AdPriceModes.From => Loc.T(L.YellowPages.PriceFrom, Gil(ad.PriceGil)),
            _ => Loc.T(L.YellowPages.PriceAsk),
        };
    }

    public static string Identity(AdDto ad)
    {
        var name = SocialIdentity.Name(ad.OwnerName, ad.OwnerHandle);
        return ad.OwnerHandle.Length > 0 ? $"{name} · @{ad.OwnerHandle}" : name;
    }

    public static string ExpiresLine(AdDto ad, long nowUnix)
    {
        var remaining = ad.ExpiresAtUnix - nowUnix;
        if (remaining <= 0)
        {
            return Loc.T(L.YellowPages.Expired);
        }

        var days = (int)(remaining / 86400);
        if (days >= 1)
        {
            return Loc.T(L.YellowPages.ExpiresDays, days);
        }

        var hours = Math.Max(1, (int)(remaining / 3600));
        return Loc.T(L.YellowPages.ExpiresHours, hours);
    }

    public static AdOpenState OpenState(AdDto ad, long nowUnix)
    {
        if (ad.OpenUntilUnix > nowUnix)
        {
            return new AdOpenState(true, ad.OpenUntilUnix, 0L);
        }

        if (ad.Archetype != AdArchetypes.Place || ad.Schedule.Length == 0)
        {
            return new AdOpenState(false, 0L, 0L);
        }

        var nowMinuteOfWeek = MinuteOfWeek(nowUnix);
        var bestDelta = int.MaxValue;
        for (var index = 0; index < ad.Schedule.Length; index++)
        {
            var slot = ad.Schedule[index];
            var start = slot.Day * 1440 + slot.StartMinute;
            var sinceStart = Modulo(nowMinuteOfWeek - start, MinutesPerWeek);
            if (sinceStart < slot.DurationMinutes)
            {
                var closesAt = nowUnix + (slot.DurationMinutes - sinceStart) * 60L;
                return new AdOpenState(true, closesAt, 0L);
            }

            var untilStart = Modulo(start - nowMinuteOfWeek, MinutesPerWeek);
            if (untilStart < bestDelta)
            {
                bestDelta = untilStart;
            }
        }

        var nextOpening = bestDelta == int.MaxValue ? 0L : nowUnix + bestDelta * 60L;
        return new AdOpenState(false, 0L, nextOpening);
    }

    public static string OpenLine(AdDto ad, long nowUnix)
    {
        var state = OpenState(ad, nowUnix);
        if (state.IsOpen)
        {
            return state.ClosesAtUnix > 0
                ? Loc.T(L.YellowPages.OpenClosesAt, TimeText.Clock(state.ClosesAtUnix))
                : Loc.T(L.YellowPages.OpenNow);
        }

        if (state.NextOpeningUnix <= 0)
        {
            return string.Empty;
        }

        return Loc.T(L.YellowPages.OpensAt,
            $"{TimeText.DayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}");
    }

    public static string ScheduleSlotLine(AdScheduleSlot slot, long nowUnix)
    {
        var startUnix = NextOccurrenceUnix(slot, nowUnix);
        var endUnix = startUnix + slot.DurationMinutes * 60L;
        return $"{TimeText.DayLabel(startUnix)} {TimeText.Clock(startUnix)} - {TimeText.Clock(endUnix)}";
    }

    public static long NextOccurrenceUnix(AdScheduleSlot slot, long nowUnix)
    {
        var nowMinuteOfWeek = MinuteOfWeek(nowUnix);
        var start = slot.Day * 1440 + slot.StartMinute;
        var untilStart = Modulo(start - nowMinuteOfWeek, MinutesPerWeek);
        return nowUnix - nowUnix % 60 + untilStart * 60L;
    }

    /// <summary>Converts a slot the user picked in their local clock into the UTC weekly slot the wire
    /// stores. Anchored on the next occurrence, so DST shifts only drift future weeks by the usual hour.</summary>
    public static AdScheduleSlot ToUtcSlot(int localDay, int localStartMinute, int durationMinutes)
    {
        var nowLocal = DateTime.Now;
        var daysAhead = Modulo(localDay - (int)nowLocal.DayOfWeek, 7);
        var localStart = nowLocal.Date.AddDays(daysAhead).AddMinutes(localStartMinute);
        var utc = localStart.ToUniversalTime();
        return new AdScheduleSlot((int)utc.DayOfWeek, utc.Hour * 60 + utc.Minute, durationMinutes);
    }

    private static int MinuteOfWeek(long unixSeconds)
    {
        var moment = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        return (int)moment.DayOfWeek * 1440 + moment.Hour * 60 + moment.Minute;
    }

    private static int Modulo(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}
