using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Housing;

internal static class HousingFormat
{
    public static TimeSpan? Remaining(DateTime? phaseEndsUtc, DateTime nowUtc)
    {
        if (phaseEndsUtc is not { } target || target == default)
        {
            return null;
        }

        var remaining = target - nowUtc;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    public static bool HasExpired(DateTime? phaseEndsUtc, DateTime nowUtc) =>
        phaseEndsUtc is { } target && target != default && target <= nowUtc;

    public static string Countdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Loc.T(L.Housing.CountdownEnded);
        }

        if (remaining.TotalDays >= 1d)
        {
            return Loc.T(L.Housing.CountdownDays, (int)remaining.TotalDays, remaining.Hours, remaining.Minutes);
        }

        if (remaining.TotalHours >= 1d)
        {
            return Loc.T(L.Housing.CountdownHours, (int)remaining.TotalHours, remaining.Minutes);
        }

        if (remaining.TotalMinutes >= 1d)
        {
            return Loc.T(L.Housing.CountdownMinutes, (int)remaining.TotalMinutes, remaining.Seconds);
        }

        return Loc.T(L.Housing.CountdownUnderMinute);
    }

    public static string PhaseCountdown(HousingPlot plot, DateTime nowUtc)
    {
        if (plot.PhaseEndsUtc is null)
        {
            return Loc.T(L.Housing.TimeUnknown);
        }

        if (HasExpired(plot.PhaseEndsUtc, nowUtc))
        {
            return Loc.T(L.Housing.PhaseExpired);
        }

        return Countdown(Remaining(plot.PhaseEndsUtc, nowUtc) ?? TimeSpan.Zero);
    }

    public static string ScanAge(DateTime scannedUtc, DateTime nowUtc)
    {
        if (scannedUtc == default)
        {
            return Loc.T(L.Housing.ScannedUnknown);
        }

        var age = nowUtc - scannedUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1d)
        {
            return Loc.T(L.Housing.ScannedJustNow);
        }

        if (age.TotalMinutes < 60d)
        {
            return Loc.T(L.Housing.ScannedMinutes, (int)age.TotalMinutes);
        }

        if (age.TotalHours < 24d)
        {
            return Loc.T(L.Housing.ScannedHours, (int)age.TotalHours);
        }

        return Loc.T(L.Housing.ScannedDays, (int)age.TotalDays);
    }

    public static string AgeRelative(DateTime momentUtc, DateTime nowUtc)
    {
        if (momentUtc == default)
        {
            return Loc.T(L.Housing.TimeUnknown);
        }

        var age = nowUtc - momentUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1d)
        {
            return Loc.T(L.Housing.AgeJustNow);
        }

        if (age.TotalMinutes < 60d)
        {
            return Loc.T(L.Housing.AgeMinutesAgo, (int)age.TotalMinutes);
        }

        return age.TotalHours < 24d
            ? Loc.T(L.Housing.AgeHoursAgo, (int)age.TotalHours)
            : Loc.T(L.Housing.AgeDaysAgo, (int)age.TotalDays);
    }

    public static string ScanAgeShort(DateTime scannedUtc, DateTime nowUtc)
    {
        if (scannedUtc == default)
        {
            return "-";
        }

        var age = nowUtc - scannedUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1d)
        {
            return Loc.T(L.Housing.AgeNow);
        }

        if (age.TotalMinutes < 60d)
        {
            return Loc.T(L.Housing.AgeMinutes, (int)age.TotalMinutes);
        }

        if (age.TotalHours < 24d)
        {
            return Loc.T(L.Housing.AgeHours, (int)age.TotalHours);
        }

        return Loc.T(L.Housing.AgeDays, (int)age.TotalDays);
    }

    public static string ExactLocalTime(DateTime utcMoment)
    {
        if (utcMoment == default)
        {
            return Loc.T(L.Housing.TimeUnknown);
        }

        var local = DateTime.SpecifyKind(utcMoment, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("d MMM yyyy", Loc.Culture) + " " + TimeText.Clock(local);
    }

    public static string Price(long gil) =>
        gil <= 0L ? Loc.T(L.Housing.NotReported) : Loc.T(L.Housing.PriceGil, gil.ToString("N0", Loc.Culture));

    public static string Entries(int? entries) =>
        entries is { } count ? count.ToString(Loc.Culture) : Loc.T(L.Housing.NotReported);

    public static string? Odds(int? entries)
    {
        if (entries is not { } count || count <= 0)
        {
            return null;
        }

        return Loc.T(L.Housing.OddsApproximate, count);
    }

    public static string SizeLabel(HousingPlotSize size) => size switch
    {
        HousingPlotSize.Small => Loc.T(L.Housing.SizeSmall),
        HousingPlotSize.Medium => Loc.T(L.Housing.SizeMedium),
        HousingPlotSize.Large => Loc.T(L.Housing.SizeLarge),
        _ => Loc.T(L.Housing.SizeUnknown),
    };

    public static string PhaseLabel(HousingLotteryPhase phase) => phase switch
    {
        HousingLotteryPhase.Entry => Loc.T(L.Housing.PhaseEntry),
        HousingLotteryPhase.Results => Loc.T(L.Housing.PhaseResults),
        HousingLotteryPhase.Unavailable => Loc.T(L.Housing.PhaseUnavailable),
        HousingLotteryPhase.Expired => Loc.T(L.Housing.PhaseExpired),
        _ => Loc.T(L.Housing.PhaseUnknown),
    };

    public static string EligibilityLabel(HousingPurchaseEligibility eligibility) => eligibility switch
    {
        HousingPurchaseEligibility.Private => Loc.T(L.Housing.EligibilityPrivate),
        HousingPurchaseEligibility.FreeCompany => Loc.T(L.Housing.EligibilityFreeCompany),
        HousingPurchaseEligibility.Both => Loc.T(L.Housing.EligibilityBoth),
        _ => Loc.T(L.Housing.NotReported),
    };

    public static string ModeLabel(HousingPurchaseMode mode) => mode switch
    {
        HousingPurchaseMode.Lottery => Loc.T(L.Housing.ModeLottery),
        HousingPurchaseMode.FirstComeFirstServed => Loc.T(L.Housing.ModeFcfs),
        _ => Loc.T(L.Housing.NotReported),
    };

    public static string FreshnessLabel(HousingDataFreshness freshness) => freshness switch
    {
        HousingDataFreshness.Live => Loc.T(L.Housing.FreshnessLive),
        HousingDataFreshness.Recent => Loc.T(L.Housing.FreshnessRecent),
        HousingDataFreshness.Stale => Loc.T(L.Housing.FreshnessStale),
        HousingDataFreshness.Cached => Loc.T(L.Housing.FreshnessCached),
        _ => Loc.T(L.Housing.FreshnessUnknown),
    };

    public static string DivisionLabel(bool isSubdivision) =>
        Loc.T(isSubdivision ? L.Housing.Subdivision : L.Housing.MainDivision);

    public static string WardLabel(int ward) => Loc.T(L.Housing.WardNumber, ward);

    public static string PlotLabel(int plot) => Loc.T(L.Housing.PlotNumber, plot);

    public static string PlotTitle(HousingPlot plot) =>
        Loc.T(L.Housing.PlotTitle, plot.Key.Plot, SizeLabel(plot.Size));

    public static string Place(string districtName, int ward) =>
        Loc.T(L.Housing.PlaceLine, districtName, ward);

    public static string LeadTime(int minutes) =>
        minutes >= 60 && minutes % 60 == 0
            ? Loc.Plural(L.Housing.LeadHours, minutes / 60)
            : Loc.Plural(L.Housing.LeadMinutes, minutes);
}
