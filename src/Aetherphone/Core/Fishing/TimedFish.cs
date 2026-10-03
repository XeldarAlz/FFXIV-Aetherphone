using System.Globalization;

namespace Aetherphone.Core.Fishing;

internal enum FishHookset : byte
{
    Unknown,
    Precision,
    Powerful,
}

internal enum FishTug : byte
{
    Unknown,
    Light,
    Medium,
    Heavy,
}

[Flags]
internal enum TimedFishFlags : byte
{
    None = 0,
    BigFish = 1,
    Folklore = 2,
    Collectable = 4,
    Snagging = 8,
    AmbitiousLure = 16,
    ModestLure = 32,
}

internal readonly record struct FishPredator(uint ItemId, byte Count);

internal sealed record TimedFish(
    uint ItemId,
    uint SpotId,
    FishWindowRule Rule,
    uint[] BaitChain,
    FishPredator[] Predators,
    ushort IntuitionSeconds,
    FishHookset Hookset,
    FishTug Tug,
    TimedFishFlags Flags,
    float Patch)
{
    public bool IsBigFish => (Flags & TimedFishFlags.BigFish) != 0;
}

internal static class TimedFishParser
{
    private const int FieldCount = 12;
    private static readonly byte[] NoWeather = Array.Empty<byte>();
    private static readonly uint[] NoBait = Array.Empty<uint>();
    private static readonly FishPredator[] NoPredators = Array.Empty<FishPredator>();

    public static List<TimedFish> Parse(string text)
    {
        var fish = new List<TimedFish>();
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var line = text.AsSpan(lineStart, lineEnd - lineStart).TrimEnd('\r');
            lineStart = lineEnd + 1;
            if (TryParseLine(line, out var parsed))
            {
                fish.Add(parsed);
            }
        }

        return fish;
    }

    public static bool TryParseLine(ReadOnlySpan<char> line, out TimedFish fish)
    {
        fish = null!;
        if (line.IsEmpty)
        {
            return false;
        }

        Span<Range> fields = stackalloc Range[FieldCount + 1];
        if (line.Split(fields, '|') != FieldCount)
        {
            return false;
        }

        if (!TryUInt(line[fields[0]], out var itemId) || itemId == 0 || !TryUInt(line[fields[1]], out var spotId) ||
            !TryUInt(line[fields[2]], out var startMinute) || !TryUInt(line[fields[3]], out var endMinute) ||
            startMinute > FishWindowRule.MinutesPerDay || endMinute > FishWindowRule.MinutesPerDay)
        {
            return false;
        }

        var weather = ParseBytes(line[fields[4]]);
        var previousWeather = ParseBytes(line[fields[5]]);
        var bait = ParseIds(line[fields[6]]);
        var predators = ParsePredators(line[fields[7]]);
        TryUInt(line[fields[8]], out var intuition);
        var hookAndTug = line[fields[9]];
        var flags = ParseFlags(line[fields[10]]);
        float.TryParse(line[fields[11]], NumberStyles.Float, CultureInfo.InvariantCulture, out var patch);
        var rule = new FishWindowRule((ushort)startMinute, (ushort)endMinute, weather, previousWeather);
        fish = new TimedFish(itemId, spotId, rule, bait, predators, (ushort)Math.Min(intuition, ushort.MaxValue),
            ParseHookset(hookAndTug), ParseTug(hookAndTug), flags, patch);
        return true;
    }

    private static bool TryUInt(ReadOnlySpan<char> field, out uint value)
    {
        if (field.IsEmpty)
        {
            value = 0;
            return true;
        }

        return uint.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static int CountEntries(ReadOnlySpan<char> field)
    {
        if (field.IsEmpty)
        {
            return 0;
        }

        var count = 1;
        for (var index = 0; index < field.Length; index++)
        {
            if (field[index] == ',')
            {
                count++;
            }
        }

        return count;
    }

    private static byte[] ParseBytes(ReadOnlySpan<char> field)
    {
        var count = CountEntries(field);
        if (count == 0)
        {
            return NoWeather;
        }

        var values = new byte[count];
        var written = 0;
        foreach (var range in field.Split(','))
        {
            if (byte.TryParse(field[range], NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                value != 0)
            {
                values[written++] = value;
            }
        }

        return written == count ? values : values.AsSpan(0, written).ToArray();
    }

    private static uint[] ParseIds(ReadOnlySpan<char> field)
    {
        var count = CountEntries(field);
        if (count == 0)
        {
            return NoBait;
        }

        var values = new uint[count];
        var written = 0;
        foreach (var range in field.Split(','))
        {
            if (uint.TryParse(field[range], NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                value != 0)
            {
                values[written++] = value;
            }
        }

        return written == count ? values : values.AsSpan(0, written).ToArray();
    }

    private static FishPredator[] ParsePredators(ReadOnlySpan<char> field)
    {
        var count = CountEntries(field);
        if (count == 0)
        {
            return NoPredators;
        }

        var values = new FishPredator[count];
        var written = 0;
        foreach (var range in field.Split(','))
        {
            var entry = field[range];
            var separator = entry.IndexOf('x');
            if (separator <= 0 ||
                !uint.TryParse(entry[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) ||
                !byte.TryParse(entry[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var amount))
            {
                continue;
            }

            values[written++] = new FishPredator(itemId, amount);
        }

        return written == count ? values : values.AsSpan(0, written).ToArray();
    }

    private static FishHookset ParseHookset(ReadOnlySpan<char> field)
    {
        if (field.Contains('P'))
        {
            return FishHookset.Precision;
        }

        return field.Contains('W') ? FishHookset.Powerful : FishHookset.Unknown;
    }

    private static FishTug ParseTug(ReadOnlySpan<char> field)
    {
        if (field.Contains('L'))
        {
            return FishTug.Light;
        }

        if (field.Contains('M'))
        {
            return FishTug.Medium;
        }

        return field.Contains('H') ? FishTug.Heavy : FishTug.Unknown;
    }

    private static TimedFishFlags ParseFlags(ReadOnlySpan<char> field)
    {
        var flags = TimedFishFlags.None;
        for (var index = 0; index < field.Length; index++)
        {
            flags |= field[index] switch
            {
                'B' => TimedFishFlags.BigFish,
                'L' => TimedFishFlags.Folklore,
                'C' => TimedFishFlags.Collectable,
                'N' => TimedFishFlags.Snagging,
                'A' => TimedFishFlags.AmbitiousLure,
                'M' => TimedFishFlags.ModestLure,
                _ => TimedFishFlags.None,
            };
        }

        return flags;
    }
}
