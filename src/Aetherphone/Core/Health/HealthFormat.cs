using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Health;

internal static class HealthFormat
{
    public const double YalmsPerMalm = 1760d;

    private const double MetresPerYalm = 1d;
    private const double FeetPerMetre = 3.28084d;
    private const double MetresPerMile = 1609.34d;
    private const double CmPerInch = 2.54d;
    private const double InchPerFulm = 12d;
    private const double LbPerKg = 2.2046226d;
    private const double MlPerFlOz = 29.5735d;

    private static CultureInfo Culture => Loc.Culture;

    public static string Distance(double yalms, HealthUnits units)
    {
        yalms = Sane(yalms);
        switch (units)
        {
            case HealthUnits.Metric:
                var metres = yalms * MetresPerYalm;
                return metres >= 1000d
                    ? (metres / 1000d).ToString("0.00", Culture) + Loc.T(L.Health.UnitKm)
                    : metres.ToString("0", Culture) + Loc.T(L.Health.UnitM);
            case HealthUnits.Imperial:
                var miMetres = yalms * MetresPerYalm;
                if (miMetres >= MetresPerMile)
                {
                    return (miMetres / MetresPerMile).ToString("0.00", Culture) + Loc.T(L.Health.UnitMi);
                }

                return (miMetres * FeetPerMetre).ToString("0", Culture) + Loc.T(L.Health.UnitFt);
            default:
                return yalms >= YalmsPerMalm
                    ? (yalms / YalmsPerMalm).ToString("0.00", Culture) + Loc.T(L.Health.UnitMalms)
                    : yalms.ToString("0", Culture) + Loc.T(L.Health.UnitYalms);
        }
    }

    public static string Height(double cm, HealthUnits units)
    {
        cm = Sane(cm);
        if (cm <= 0)
        {
            return "-";
        }

        switch (units)
        {
            case HealthUnits.Metric:
                return cm.ToString("0.0", Culture) + Loc.T(L.Health.UnitCm);
            case HealthUnits.Imperial:
                return FeetInches(cm, "'", "\"");
            default:
                return FeetInches(cm, Loc.T(L.Health.UnitFulm), Loc.T(L.Health.UnitIlm));
        }
    }

    private static string FeetInches(double cm, string bigUnit, string smallUnit)
    {
        var totalInches = cm / CmPerInch;
        var big = (int)(totalInches / InchPerFulm);
        var small = (int)Math.Round(totalInches - big * InchPerFulm);
        if (small >= 12)
        {
            big++;
            small = 0;
        }

        return Loc.T(L.Health.HeightImperial, big, bigUnit, small, smallUnit);
    }

    public static string Weight(double kg, HealthUnits units)
    {
        kg = Sane(kg);
        return units switch
        {
            HealthUnits.Metric => kg.ToString("0.0", Culture) + Loc.T(L.Health.UnitKg),
            _ => (kg * LbPerKg).ToString("0", Culture) +
                 Loc.T(units == HealthUnits.Eorzean ? L.Health.UnitPonz : L.Health.UnitLb),
        };
    }

    public static double WeightToKg(double value, HealthUnits units) =>
        units == HealthUnits.Metric ? value : value / LbPerKg;

    public static double WeightFromKg(double kg, HealthUnits units) =>
        units == HealthUnits.Metric ? kg : kg * LbPerKg;

    public static string Volume(double ml, HealthUnits units)
    {
        ml = Sane(ml);
        if (units == HealthUnits.Imperial)
        {
            return (ml / MlPerFlOz).ToString("0", Culture) + Loc.T(L.Health.UnitFlOz);
        }

        return ml >= 1000d
            ? (ml / 1000d).ToString("0.0", Culture) + Loc.T(L.Health.UnitLitre)
            : ml.ToString("0", Culture) + Loc.T(L.Health.UnitMl);
    }

    public static string DrinkKindName(HydrationEntry entry) => entry.KindKey switch
    {
        DrinkKeys.Water => Loc.T(L.Health.DrinkKindWater),
        DrinkKeys.Tea => Loc.T(L.Health.DrinkKindTea),
        DrinkKeys.Coffee => Loc.T(L.Health.DrinkKindCoffee),
        DrinkKeys.Juice => Loc.T(L.Health.DrinkKindJuice),
        _ => entry.Kind.Length > 0 ? entry.Kind : Loc.T(L.Health.DrinkFallback),
    };

    public static string GoalName(HealthGoal goal) => goal.NameKey switch
    {
        GoalKeys.Walk1000 => Loc.T(L.Health.DefaultGoalWalk1000),
        GoalKeys.Walk5000 => Loc.T(L.Health.DefaultGoalWalk5000),
        GoalKeys.Walk10000 => Loc.T(L.Health.DefaultGoalWalk10000),
        GoalKeys.WalkMalm => Loc.T(L.Health.DefaultGoalWalkMalm),
        GoalKeys.Swim500 => Loc.T(L.Health.DefaultGoalSwim500),
        GoalKeys.Drinks4 => Loc.T(L.Health.DefaultGoalDrinks),
        GoalKeys.Active30 => Loc.T(L.Health.DefaultGoalActive30),
        GoalKeys.New => Loc.T(L.Health.NewGoal),
        _ => goal.Name.Length > 0 ? goal.Name : Loc.T(L.Health.GoalFallback),
    };

    public static long Steps(double onFootYalms, double strideYalms)
    {
        var stride = strideYalms is > 0.05 and < 10 ? strideYalms : 0.75;
        return (long)Math.Floor(Sane(onFootYalms) / stride);
    }

    public static string Number(long value) => value.ToString("N0", Culture);

    public static string Duration(double seconds)
    {
        var total = (int)Math.Max(0, seconds);
        var minutes = total / 60;
        var hours = minutes / 60;
        return hours > 0 ? Loc.T(L.Health.DurationHm, hours, minutes % 60) : Loc.T(L.Health.DurationM, minutes);
    }

    public static double MetFor(MovementKind kind) => kind switch
    {
        MovementKind.Walking => 3.5,
        MovementKind.Running => 8.0,
        MovementKind.Swimming => 6.0,
        MovementKind.Diving => 7.0,
        _ => 0d,
    };

    public static double Calories(MovementKind kind, double seconds, double weightKg)
    {
        var met = MetFor(kind);
        if (met <= 0 || weightKg <= 0 || seconds <= 0)
        {
            return 0d;
        }

        return met * 3.5 * weightKg / 200d * (seconds / 60d);
    }

    private static (double FactorCm, double Min, double Max)? Rsp(uint tribe, bool female) => tribe switch
    {
        1 => (female ? 163.8788462 : 174.8692308, 0.960, 1.040),
        2 => (female ? 163.8566434 : 174.9580420, 1.056, 1.144),
        3 or 4 => (female ? 190.9278152 : 201.9287777, 0.961, 1.039),
        5 or 6 => (91.96966825, 0.945, 1.055),
        7 or 8 => female ? (155.8192308, 0.960, 1.040) : (174.9777778, 0.910, 0.990),
        9 or 10 => female ? (192.0327586, 1.000, 1.160) : (221.9441233, 0.962, 1.038),
        11 or 12 => female ? (156.9267327, 0.930, 1.010) : (174.9322581, 1.160, 1.240),
        13 or 14 => female ? (187.0450281, 0.986, 1.066) : (219.8884298, 0.892, 0.968),
        15 or 16 => female ? (160.8595458, 1.111, 1.189) : (174.8930582, 0.984, 1.066),
        _ => null,
    };

    public static double HeightCm(uint tribeId, bool female, byte heightSlider)
    {
        if (Rsp(tribeId, female) is not { } r)
        {
            return 0d;
        }

        var t = Math.Clamp(heightSlider / 100d, 0d, 1d);
        return r.FactorCm * (r.Min + (r.Max - r.Min) * t);
    }

    public static double SuggestStride(double heightCm) =>
        heightCm > 0 ? Math.Clamp(heightCm * 0.00415d, 0.30d, 1.50d) : 0.75d;

    private static double Sane(double value) => double.IsFinite(value) && value > 0 ? value : 0d;
}
