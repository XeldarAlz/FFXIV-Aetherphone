using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Lyrics;

internal readonly struct LrcWord
{
    public readonly double Start;
    public readonly int CharStart;
    public readonly int CharLength;

    public LrcWord(double start, int charStart, int charLength)
    {
        Start = start;
        CharStart = charStart;
        CharLength = charLength;
    }
}

internal sealed class LrcDocument
{
    public static readonly LrcDocument Empty = new(false, Array.Empty<double>(), Array.Empty<string>(),
        Array.Empty<int>(), Array.Empty<int>(), Array.Empty<LrcWord>());

    private readonly double[] starts;
    private readonly string[] texts;
    private readonly int[] wordOffsets;
    private readonly int[] wordCounts;
    private readonly LrcWord[] words;

    private LrcDocument(bool isSynced, double[] starts, string[] texts, int[] wordOffsets, int[] wordCounts,
        LrcWord[] words)
    {
        IsSynced = isSynced;
        this.starts = starts;
        this.texts = texts;
        this.wordOffsets = wordOffsets;
        this.wordCounts = wordCounts;
        this.words = words;
    }

    public bool IsSynced { get; }

    public int Count => texts.Length;

    public bool IsEmpty => texts.Length == 0;

    public bool HasWordTiming => words.Length > 0;

    public ReadOnlySpan<double> Starts => starts;

    public ReadOnlySpan<string> Texts => texts;

    public double StartOf(int lineIndex) => IsSynced ? starts[lineIndex] : 0;

    public string TextOf(int lineIndex) => texts[lineIndex];

    public ReadOnlySpan<LrcWord> WordsOf(int lineIndex)
    {
        if (words.Length == 0)
        {
            return ReadOnlySpan<LrcWord>.Empty;
        }

        return new ReadOnlySpan<LrcWord>(words, wordOffsets[lineIndex], wordCounts[lineIndex]);
    }

    public int LineAt(double seconds)
    {
        if (!IsSynced || starts.Length == 0 || seconds < starts[0])
        {
            return -1;
        }

        var low = 0;
        var high = starts.Length - 1;
        while (low < high)
        {
            var middle = low + ((high - low + 1) >> 1);
            if (starts[middle] <= seconds)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    public static LrcDocument Plain(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var lines = SplitLines(text);
        var first = 0;
        var last = lines.Length - 1;
        while (first <= last && lines[first].Length == 0)
        {
            first++;
        }

        while (last >= first && lines[last].Length == 0)
        {
            last--;
        }

        var texts = new string[last - first + 1];
        Array.Copy(lines, first, texts, 0, texts.Length);
        return new LrcDocument(false, Array.Empty<double>(), texts, Array.Empty<int>(), Array.Empty<int>(),
            Array.Empty<LrcWord>());
    }

    public static LrcDocument Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var lines = SplitLines(text);
        var parsed = new List<ParsedLine>(lines.Length);
        var parsedWords = new List<LrcWord>();
        var lineTimes = new List<double>(4);
        var offsetSeconds = 0d;
        var sawTimestamp = false;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            lineTimes.Clear();
            var cursor = 0;
            while (cursor < line.Length && line[cursor] == '[')
            {
                var close = line.IndexOf(']', cursor + 1);
                if (close < 0)
                {
                    break;
                }

                var tag = line.AsSpan(cursor + 1, close - cursor - 1);
                if (TryParseTime(tag, out var seconds))
                {
                    lineTimes.Add(seconds);
                }
                else if (TryParseOffset(tag, out var offset))
                {
                    offsetSeconds = offset;
                }

                cursor = close + 1;
            }

            if (lineTimes.Count == 0)
            {
                continue;
            }

            sawTimestamp = true;
            var body = line.Substring(cursor);
            var wordStart = parsedWords.Count;
            var lyric = ExtractWords(body, parsedWords);
            var wordCount = parsedWords.Count - wordStart;
            for (var timeIndex = 0; timeIndex < lineTimes.Count; timeIndex++)
            {
                parsed.Add(new ParsedLine(lineTimes[timeIndex], lyric, wordStart, wordCount, parsed.Count));
            }
        }

        if (!sawTimestamp)
        {
            return Plain(text);
        }

        parsed.Sort(static (left, right) =>
        {
            var byTime = left.Start.CompareTo(right.Start);
            return byTime != 0 ? byTime : left.Order.CompareTo(right.Order);
        });

        var count = parsed.Count;
        var totalWords = 0;
        for (var index = 0; index < count; index++)
        {
            totalWords += parsed[index].WordCount;
        }

        var starts = new double[count];
        var texts = new string[count];
        var hasWords = totalWords > 0;
        var wordOffsets = hasWords ? new int[count] : Array.Empty<int>();
        var wordCounts = hasWords ? new int[count] : Array.Empty<int>();
        var words = hasWords ? new LrcWord[totalWords] : Array.Empty<LrcWord>();
        var wordCursor = 0;
        for (var index = 0; index < count; index++)
        {
            var entry = parsed[index];
            starts[index] = Math.Max(0, entry.Start - offsetSeconds);
            texts[index] = entry.Text;
            if (!hasWords)
            {
                continue;
            }

            wordOffsets[index] = wordCursor;
            wordCounts[index] = entry.WordCount;
            for (var wordIndex = 0; wordIndex < entry.WordCount; wordIndex++)
            {
                var word = parsedWords[entry.WordStart + wordIndex];
                words[wordCursor++] = new LrcWord(Math.Max(0, word.Start - offsetSeconds), word.CharStart,
                    word.CharLength);
            }
        }

        return new LrcDocument(true, starts, texts, wordOffsets, wordCounts, words);
    }

    private static string[] SplitLines(string text)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd('\r').Trim();
        }

        return lines;
    }

    private static string ExtractWords(string body, List<LrcWord> sink)
    {
        if (body.IndexOf('<') < 0)
        {
            return body.Trim();
        }

        var firstWord = sink.Count;
        var builder = new StringBuilder(body.Length);
        var pendingStart = -1d;
        var pendingCharStart = 0;
        var cursor = 0;
        while (cursor < body.Length)
        {
            var character = body[cursor];
            if (character == '<')
            {
                var close = body.IndexOf('>', cursor + 1);
                if (close > cursor && TryParseTime(body.AsSpan(cursor + 1, close - cursor - 1), out var seconds))
                {
                    FlushWord(sink, builder.Length, pendingStart, pendingCharStart);
                    pendingStart = seconds;
                    pendingCharStart = builder.Length;
                    cursor = close + 1;
                    continue;
                }
            }

            builder.Append(character);
            cursor++;
        }

        FlushWord(sink, builder.Length, pendingStart, pendingCharStart);
        var raw = builder.ToString();
        var trimmed = raw.Trim();
        var leading = trimmed.Length == 0 ? 0 : raw.IndexOf(trimmed, StringComparison.Ordinal);
        for (var index = firstWord; index < sink.Count; index++)
        {
            var word = sink[index];
            var start = Math.Clamp(word.CharStart - leading, 0, trimmed.Length);
            var end = Math.Clamp(word.CharStart + word.CharLength - leading, 0, trimmed.Length);
            sink[index] = new LrcWord(word.Start, start, end - start);
        }

        return trimmed;
    }

    private static void FlushWord(List<LrcWord> sink, int textLength, double start, int charStart)
    {
        if (start < 0 || textLength <= charStart)
        {
            return;
        }

        sink.Add(new LrcWord(start, charStart, textLength - charStart));
    }

    internal static bool TryParseTime(ReadOnlySpan<char> tag, out double seconds)
    {
        seconds = 0;
        var colon = tag.IndexOf(':');
        if (colon <= 0 || colon == tag.Length - 1)
        {
            return false;
        }

        var minutesSpan = tag[..colon];
        var rest = tag[(colon + 1)..];
        if (!IsDigits(minutesSpan))
        {
            return false;
        }

        var separator = rest.IndexOfAny('.', ':');
        var secondsSpan = separator < 0 ? rest : rest[..separator];
        var fractionSpan = separator < 0 ? ReadOnlySpan<char>.Empty : rest[(separator + 1)..];
        if (secondsSpan.Length == 0 || !IsDigits(secondsSpan) || (separator >= 0 && !IsDigits(fractionSpan)))
        {
            return false;
        }

        if (fractionSpan.Length > 3)
        {
            fractionSpan = fractionSpan[..3];
        }

        if (!int.TryParse(minutesSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(secondsSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var wholeSeconds))
        {
            return false;
        }

        var fraction = 0d;
        if (fractionSpan.Length > 0)
        {
            fraction = int.Parse(fractionSpan, NumberStyles.None, CultureInfo.InvariantCulture)
                / Math.Pow(10, fractionSpan.Length);
        }

        seconds = minutes * 60d + wholeSeconds + fraction;
        return true;
    }

    private static bool TryParseOffset(ReadOnlySpan<char> tag, out double seconds)
    {
        seconds = 0;
        const string prefix = "offset:";
        if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = tag[prefix.Length..].Trim();
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return false;
        }

        seconds = milliseconds / 1000d;
        return true;
    }

    private static bool IsDigits(ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < span.Length; index++)
        {
            if (span[index] < '0' || span[index] > '9')
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct ParsedLine(double Start, string Text, int WordStart, int WordCount, int Order);
}
