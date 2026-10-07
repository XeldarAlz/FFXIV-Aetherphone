using System.Globalization;

namespace Aetherphone.Core.MoogleClicker;

internal static class KupoFormat
{
    public const int MaxGroup = 100;
    private const int SmallCount = 1000;
    private const int CacheLimit = 8192;
    private const int NamedGroups = 5;
    private const int Letters = 26;
    private const int KeyStride = 3000;
    private const int DecimalStride = 1000;
    private const double Thousand = 1000d;
    private const double Slack = 1e-7;
    private static readonly double Overflow = Math.Pow(Thousand, MaxGroup + 1);
    private static readonly string[] NamedSuffixes = { "", "K", "M", "B", "T" };
    private static readonly string[] Whole = new string[SmallCount];
    private static readonly string[] Tenths = new string[SmallCount];
    private static readonly Dictionary<int, string> Compact = new();
    private static string? overflowLabel;

    public static string Amount(double value)
    {
        if (!(value >= 1d))
        {
            return WholeLabel(0);
        }

        return value < Thousand ? WholeLabel((int)value) : CompactLabel(value);
    }

    public static string Rate(double value)
    {
        if (!(value > 0d))
        {
            return WholeLabel(0);
        }

        if (value < 100d)
        {
            return TenthsLabel((int)(value * 10d + Slack));
        }

        return value < Thousand ? WholeLabel((int)value) : CompactLabel(value);
    }

    public static string Suffix(int group)
    {
        if (group < NamedGroups)
        {
            return NamedSuffixes[Math.Max(0, group)];
        }

        var index = group - NamedGroups;
        Span<char> letters = stackalloc char[2];
        letters[0] = (char)('a' + index / Letters % Letters);
        letters[1] = (char)('a' + index % Letters);
        return new string(letters);
    }

    private static string WholeLabel(int value)
    {
        var clamped = Math.Clamp(value, 0, SmallCount - 1);
        return Whole[clamped] ??= clamped.ToString(CultureInfo.InvariantCulture);
    }

    private static string TenthsLabel(int tenths)
    {
        var clamped = Math.Clamp(tenths, 0, SmallCount - 1);
        if (clamped % 10 == 0)
        {
            return WholeLabel(clamped / 10);
        }

        return Tenths[clamped] ??= string.Concat((clamped / 10).ToString(CultureInfo.InvariantCulture), ".",
            (clamped % 10).ToString(CultureInfo.InvariantCulture));
    }

    private static string CompactLabel(double value)
    {
        if (!(value < Overflow))
        {
            return overflowLabel ??= Build(DecimalStride - 1, 0, MaxGroup);
        }

        var group = Math.Clamp((int)Math.Floor(Math.Log10(value) / 3d), 1, MaxGroup);
        var scaled = value / Math.Pow(Thousand, group);
        if (scaled >= Thousand && group < MaxGroup)
        {
            group++;
            scaled /= Thousand;
        }
        else if (scaled < 1d && group > 1)
        {
            group--;
            scaled *= Thousand;
        }

        int decimals;
        int digits;
        if (scaled < 10d)
        {
            decimals = 2;
            digits = (int)(scaled * 100d + Slack);
        }
        else if (scaled < 100d)
        {
            decimals = 1;
            digits = (int)(scaled * 10d + Slack);
        }
        else
        {
            decimals = 0;
            digits = (int)(scaled + Slack);
        }

        digits = Math.Clamp(digits, 0, DecimalStride - 1);
        var key = group * KeyStride + decimals * DecimalStride + digits;
        if (Compact.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Compact.Count >= CacheLimit)
        {
            Compact.Clear();
        }

        var label = Build(digits, decimals, group);
        Compact[key] = label;
        return label;
    }

    private static string Build(int digits, int decimals, int group)
    {
        var divisor = decimals == 2 ? 100 : decimals == 1 ? 10 : 1;
        var whole = digits / divisor;
        var fraction = digits % divisor;
        var fractionDigits = decimals;
        while (fractionDigits > 0 && fraction % 10 == 0)
        {
            fraction /= 10;
            fractionDigits--;
        }

        var wholeText = whole.ToString(CultureInfo.InvariantCulture);
        if (fractionDigits == 0)
        {
            return string.Concat(wholeText, Suffix(group));
        }

        var fractionText = fraction.ToString(fractionDigits == 2 ? "00" : "0", CultureInfo.InvariantCulture);
        return string.Concat(wholeText, ".", fractionText, Suffix(group));
    }
}
