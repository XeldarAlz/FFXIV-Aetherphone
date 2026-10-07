using System.Text;

namespace Aetherphone.Core.Social;

internal static class PostText
{
    public const int MaxLines = 5;
    public const int LineBreakCost = 2;

    private const int MaxLineBreaks = MaxLines - 1;
    private const char LineBreak = '\n';

    public static int Weight(string text)
    {
        return text.Length + CountLineBreaks(text) * (LineBreakCost - 1);
    }

    public static int CharacterBudget(string text, int maxWeight)
    {
        return maxWeight - CountLineBreaks(text) * (LineBreakCost - 1);
    }

    public static bool CanBreak(string text, int maxWeight)
    {
        var lineBreaks = CountLineBreaks(text);
        return lineBreaks < MaxLineBreaks && text.Length + lineBreaks * (LineBreakCost - 1) + LineBreakCost <= maxWeight;
    }

    public static string Fit(string text, int maxWeight, ref int cursor)
    {
        var lineBreaks = CountLineBreaks(text);
        if (lineBreaks <= MaxLineBreaks && text.Length + lineBreaks * (LineBreakCost - 1) <= maxWeight)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var weight = 0;
        var keptBreaks = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == LineBreak)
            {
                var keepsBreak = keptBreaks < MaxLineBreaks;
                var cost = keepsBreak ? LineBreakCost : 1;
                if (weight + cost > maxWeight)
                {
                    break;
                }

                builder.Append(keepsBreak ? LineBreak : ' ');
                keptBreaks += keepsBreak ? 1 : 0;
                weight += cost;
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

            builder.Append(text, index, runeLength);
            weight += runeLength;
            index += runeLength;
        }

        cursor = Math.Clamp(cursor, 0, builder.Length);
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
}
