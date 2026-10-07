using System.Text;

namespace Aetherphone.Core.Social;

internal static class PostText
{
    public const int MaxLines = 5;
    public const int LineBreakCost = 2;

    private const int MaxConsecutiveBreaks = 2;
    private const char LineBreak = '\n';

    public static int Weight(string text)
    {
        return text.Length + CountLineBreaks(text) * (LineBreakCost - 1);
    }

    public static int CharacterBudget(string text, int maxWeight)
    {
        return maxWeight - CountLineBreaks(text) * (LineBreakCost - 1);
    }

    public static PostBreak CanBreak(string text, int cursor, int maxWeight)
    {
        if (Weight(text) + LineBreakCost > maxWeight)
        {
            return PostBreak.NoRoom;
        }

        var position = Math.Clamp(cursor, 0, text.Length);
        if (BreaksBefore(text, position) + BreaksFrom(text, position) + 1 > MaxConsecutiveBreaks)
        {
            return PostBreak.BlankRun;
        }

        var candidate = string.Concat(text.AsSpan(0, position), "\n", text.AsSpan(position));
        var lines = CountTextLines(candidate);
        if (IsBlankLineAt(candidate, position + 1))
        {
            lines++;
        }

        return lines <= MaxLines ? PostBreak.Allowed : PostBreak.LineCap;
    }

    public static string Fit(string text, int maxWeight, ref int cursor)
    {
        if (Weight(text) <= maxWeight && CountTextLines(text) <= MaxLines && LongestBreakRun(text) <= MaxConsecutiveBreaks)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var weight = 0;
        var textLines = 0;
        var lineHasText = false;
        var breakRun = 0;
        var removedBeforeCursor = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == LineBreak)
            {
                if (breakRun >= MaxConsecutiveBreaks)
                {
                    removedBeforeCursor += index < cursor ? 1 : 0;
                    index++;
                    continue;
                }

                var joinsLine = textLines >= MaxLines && HasTextAfter(text, index);
                var cost = joinsLine ? 1 : LineBreakCost;
                if (weight + cost > maxWeight)
                {
                    break;
                }

                builder.Append(joinsLine ? ' ' : LineBreak);
                weight += cost;
                breakRun = joinsLine ? 0 : breakRun + 1;
                lineHasText = joinsLine && lineHasText;
                index++;
                continue;
            }

            var runeLength = char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                                                                && char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            if (weight + runeLength > maxWeight)
            {
                break;
            }

            if (!lineHasText && !char.IsWhiteSpace(text[index]))
            {
                lineHasText = true;
                textLines++;
            }

            builder.Append(text, index, runeLength);
            weight += runeLength;
            breakRun = 0;
            index += runeLength;
        }

        cursor = Math.Clamp(cursor - removedBeforeCursor, 0, builder.Length);
        return builder.ToString();
    }

    private static int CountLineBreaks(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == LineBreak)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountTextLines(string text)
    {
        var lines = 0;
        var lineHasText = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == LineBreak)
            {
                lineHasText = false;
                continue;
            }

            if (lineHasText || char.IsWhiteSpace(character))
            {
                continue;
            }

            lineHasText = true;
            lines++;
        }

        return lines;
    }

    private static int LongestBreakRun(string text)
    {
        var longest = 0;
        var run = 0;
        for (var index = 0; index < text.Length; index++)
        {
            run = text[index] == LineBreak ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static int BreaksBefore(string text, int position)
    {
        var count = 0;
        while (position - count - 1 >= 0 && text[position - count - 1] == LineBreak)
        {
            count++;
        }

        return count;
    }

    private static int BreaksFrom(string text, int position)
    {
        var count = 0;
        while (position + count < text.Length && text[position + count] == LineBreak)
        {
            count++;
        }

        return count;
    }

    private static bool IsBlankLineAt(string text, int lineStart)
    {
        for (var index = lineStart; index < text.Length && text[index] != LineBreak; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasTextAfter(string text, int position)
    {
        for (var index = position + 1; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return true;
            }
        }

        return false;
    }
}
