using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.YellowPages;

internal readonly record struct AdOpenState(bool IsOpen, long ClosesAtUnix, long NextOpeningUnix);

internal static class AdText
{
    public const int MaxEveryWeeks = 8;

    private const int MinutesPerWeek = 7 * 1440;
    private const long SecondsPerWeek = 7L * 86400L;

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

    public static string WorldLine(AdDto ad) => ad.WorldId > 0 ? LocationShare.WorldName((uint)ad.WorldId) : string.Empty;

    public static string Gil(long value)
    {
        return NumberText.Group(value);
    }

    public static string PriceLine(AdDto ad)
    {
        if (ad.Archetype != AdArchetypes.Service || AdCategories.IsLinkOnly(ad.Category))
        {
            return string.Empty;
        }

        if (ad.Wanted)
        {
            return ad.PriceMode switch
            {
                AdPriceModes.Fixed => Loc.T(L.YellowPages.BudgetGil, Gil(ad.PriceGil)),
                AdPriceModes.From => Loc.T(L.YellowPages.BudgetUpTo, Gil(ad.PriceGil)),
                _ => Loc.T(L.YellowPages.BudgetOpen),
            };
        }

        return ad.PriceMode switch
        {
            AdPriceModes.Fixed => Loc.T(L.YellowPages.PriceGil, Gil(ad.PriceGil)),
            AdPriceModes.From => Loc.T(L.YellowPages.PriceFrom, Gil(ad.PriceGil)),
            _ => Loc.T(L.YellowPages.PriceAsk),
        };
    }

    public static string Headline(AdDto ad, long nowUnix)
    {
        if (ad.Archetype == AdArchetypes.Place)
        {
            return OpenLine(ad, nowUnix);
        }

        if (ad.Archetype == AdArchetypes.Service)
        {
            return AdCategories.IsLinkOnly(ad.Category) ? Loc.T(L.YellowPages.ModBadge) : PriceLine(ad);
        }

        return ad.SlotsLine;
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

    public static string RemainingShort(AdDto ad, long nowUnix)
    {
        var remaining = ad.ExpiresAtUnix - nowUnix;
        if (remaining <= 0)
        {
            return Loc.T(L.YellowPages.Expired);
        }

        var days = (int)(remaining / 86400);
        if (days >= 1)
        {
            return Loc.T(L.YellowPages.DaysLeft, days);
        }

        return Loc.T(L.YellowPages.HoursLeft, Math.Max(1, (int)(remaining / 3600)));
    }

    public static bool ExpiresSoon(AdDto ad, long nowUnix) => ad.ExpiresAtUnix - nowUnix < 86400L;

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

        var nextOpening = long.MaxValue;
        for (var index = 0; index < ad.Schedule.Length; index++)
        {
            var slot = ad.Schedule[index];
            var start = UpcomingStartUnix(slot, nowUnix);
            if (start <= nowUnix)
            {
                return new AdOpenState(true, start + slot.DurationMinutes * 60L, 0L);
            }

            if (start < nextOpening)
            {
                nextOpening = start;
            }
        }

        return new AdOpenState(false, 0L, nextOpening);
    }

    public static bool Repeats(AdScheduleSlot slot) => slot.EveryWeeks > 1 && slot.FirstUnix > 0;

    public static int EveryWeeks(AdDto ad) =>
        ad.Schedule.Length > 0 && Repeats(ad.Schedule[0]) ? ad.Schedule[0].EveryWeeks : 1;

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
            $"{TimeText.FutureDayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}");
    }

    public static string ScheduleSlotLine(AdScheduleSlot slot, long nowUnix)
    {
        var startUnix = UpcomingStartUnix(slot, nowUnix);
        var endUnix = startUnix + slot.DurationMinutes * 60L;
        return $"{TimeText.FutureDayLabel(startUnix)} {TimeText.Clock(startUnix)} - {TimeText.Clock(endUnix)}";
    }

    public static long UpcomingStartUnix(AdScheduleSlot slot, long nowUnix)
    {
        if (Repeats(slot))
        {
            if (nowUnix < slot.FirstUnix)
            {
                return slot.FirstUnix;
            }

            var periodSeconds = slot.EveryWeeks * SecondsPerWeek;
            var sinceStart = (nowUnix - slot.FirstUnix) % periodSeconds;
            var latestStart = nowUnix - sinceStart;
            return sinceStart < slot.DurationMinutes * 60L ? latestStart : latestStart + periodSeconds;
        }

        var start = slot.Day * 1440 + slot.StartMinute;
        var sinceStartMinutes = Modulo(MinuteOfWeek(nowUnix) - start, MinutesPerWeek);
        var latestWeeklyStart = nowUnix - nowUnix % 60 - sinceStartMinutes * 60L;
        return sinceStartMinutes < slot.DurationMinutes ? latestWeeklyStart : latestWeeklyStart + SecondsPerWeek;
    }

    public static int FirstWeekOffset(AdScheduleSlot slot, long nowUnix)
    {
        if (!Repeats(slot))
        {
            return 0;
        }

        var weekly = new AdScheduleSlot(slot.Day, slot.StartMinute, slot.DurationMinutes);
        var weeksApart = (UpcomingStartUnix(slot, nowUnix) - UpcomingStartUnix(weekly, nowUnix) + SecondsPerWeek / 2)
            / SecondsPerWeek;
        return Modulo((int)weeksApart, slot.EveryWeeks);
    }

    public static AdScheduleSlot ToUtcSlot(int localDay, int localStartMinute, int durationMinutes)
    {
        var nowLocal = DateTime.Now;
        var daysAhead = Modulo(localDay - (int)nowLocal.DayOfWeek, 7);
        var localStart = nowLocal.Date.AddDays(daysAhead).AddMinutes(localStartMinute);
        var utc = localStart.ToUniversalTime();
        return new AdScheduleSlot((int)utc.DayOfWeek, utc.Hour * 60 + utc.Minute, durationMinutes);
    }

    public static AdScheduleSlot ToRepeatingSlot(int localDay, int localStartMinute, int durationMinutes,
        int everyWeeks, int firstWeekOffset, DateTime nowLocal)
    {
        var firstLocal = FirstLocalStart(localDay, localStartMinute, durationMinutes, nowLocal)
            .AddDays(7 * firstWeekOffset);
        var utc = firstLocal.ToUniversalTime();
        var firstUnix = new DateTimeOffset(utc).ToUnixTimeSeconds();
        return new AdScheduleSlot((int)utc.DayOfWeek, utc.Hour * 60 + utc.Minute, durationMinutes, everyWeeks,
            firstUnix);
    }

    public static DateTime FirstLocalStart(int localDay, int localStartMinute, int durationMinutes, DateTime nowLocal)
    {
        var daysAhead = Modulo(localDay - (int)nowLocal.DayOfWeek, 7);
        var start = nowLocal.Date.AddDays(daysAhead).AddMinutes(localStartMinute);
        return start.AddMinutes(durationMinutes) > nowLocal ? start : start.AddDays(7);
    }

    public static void ToLocalSlot(AdScheduleSlot slot, out int localDay, out int localStartMinute)
    {
        var nowUtc = DateTime.UtcNow;
        var daysAhead = Modulo(slot.Day - (int)nowUtc.DayOfWeek, 7);
        var utcStart = DateTime.SpecifyKind(nowUtc.Date.AddDays(daysAhead).AddMinutes(slot.StartMinute),
            DateTimeKind.Utc);
        var local = utcStart.ToLocalTime();
        localDay = (int)local.DayOfWeek;
        localStartMinute = local.Hour * 60 + local.Minute;
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
