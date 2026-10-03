using System.Globalization;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;

namespace Aetherphone.Apps.Market;

internal static class MarketText
{
    private const int CacheLimit = 512;
    private const string Minus = "−";

    private static readonly Dictionary<(string Key, string Text, long Number), string> Formatted = new();
    private static readonly Dictionary<long, string> Changes = new();
    private static readonly Dictionary<long, string> Deltas = new();
    private static readonly Dictionary<string, string> HqCategories = new(StringComparer.Ordinal);
    private static readonly Dictionary<(long Threshold, bool Below, bool Hq, string Scope), string> Rules = new();
    private static CultureInfo? culture;

    public static string Price(long value) => value > 0 ? MarketFormat.Gil(value) : "-";

    public static string Price(double value) => value > 0 ? MarketFormat.Gil(value) : "-";

    public static string Change(double fraction)
    {
        Validate();
        var tenths = (long)Math.Round(fraction * 1000d);
        if (Changes.TryGetValue(tenths, out var cached))
        {
            return cached;
        }

        var percent = Math.Abs(tenths) / 10d;
        var number = percent.ToString(percent >= 100d ? "0" : "0.0", Loc.Culture);
        var sign = tenths > 0 ? "+" : tenths < 0 ? Minus : string.Empty;
        var text = string.Concat(sign, number, "%");
        Trim(Changes.Count);
        Changes[tenths] = text;
        return text;
    }

    public static string Delta(long difference)
    {
        Validate();
        if (Deltas.TryGetValue(difference, out var cached))
        {
            return cached;
        }

        var sign = difference > 0 ? "+" : difference < 0 ? Minus : string.Empty;
        var text = string.Concat(sign, MarketFormat.Gil(Math.Abs(difference)));
        Trim(Deltas.Count);
        Deltas[difference] = text;
        return text;
    }

    public static string HqCategory(string category)
    {
        Validate();
        if (HqCategories.TryGetValue(category, out var cached))
        {
            return cached;
        }

        var hq = Loc.T(L.Common.Hq);
        var text = category.Length > 0 ? string.Concat(hq, " · ", category) : hq;
        Trim(HqCategories.Count);
        HqCategories[category] = text;
        return text;
    }

    public static string Rule(MarketAlert alert)
    {
        Validate();
        var key = (alert.Threshold, alert.Below, alert.HqOnly, alert.ScopeName);
        if (Rules.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var arrow = alert.Below ? "≤ " : "≥ ";
        var text = string.Concat(arrow, MarketFormat.Gil(alert.Threshold), " · ", alert.ScopeName);
        if (alert.HqOnly)
        {
            text = string.Concat(text, " · ", Loc.T(L.Common.Hq));
        }

        Trim(Rules.Count);
        Rules[key] = text;
        return text;
    }

    public static string Format(LocString entry, long number) => Format(entry, string.Empty, number, false);

    public static string Format(LocString entry, string text) => Format(entry, text, 0, true);

    public static string Format(LocString entry, string text, long number)
    {
        Validate();
        var key = (entry.Key, text, number);
        if (Formatted.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = Loc.T(entry, text, MarketFormat.Gil(number));
        Trim(Formatted.Count);
        Formatted[key] = value;
        return value;
    }

    public static string Velocity(double perDay)
    {
        Validate();
        var key = (L.Market.PerDay.Key, string.Empty, (long)Math.Round(perDay * 10d));
        if (Formatted.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = MarketFormat.Velocity(perDay);
        Trim(Formatted.Count);
        Formatted[key] = value;
        return value;
    }

    private static string Format(LocString entry, string text, long number, bool textOnly)
    {
        Validate();
        var key = (entry.Key, text, number);
        if (Formatted.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = textOnly ? Loc.T(entry, text) : Loc.T(entry, MarketFormat.Gil(number));
        Trim(Formatted.Count);
        Formatted[key] = value;
        return value;
    }

    private static void Validate()
    {
        if (ReferenceEquals(culture, Loc.Culture))
        {
            return;
        }

        culture = Loc.Culture;
        Formatted.Clear();
        Changes.Clear();
        Deltas.Clear();
        HqCategories.Clear();
        Rules.Clear();
    }

    private static void Trim(int count)
    {
        if (count < CacheLimit)
        {
            return;
        }

        Formatted.Clear();
        Changes.Clear();
        Deltas.Clear();
        HqCategories.Clear();
        Rules.Clear();
    }
}
