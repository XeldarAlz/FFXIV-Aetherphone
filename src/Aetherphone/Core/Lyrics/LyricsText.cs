using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Lyrics;

internal static class LyricsText
{
    private const double ContainmentFloor = 0.85;

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        for (var index = 0; index < decomposed.Length; index++)
        {
            var character = decomposed[index];
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            {
                continue;
            }

            if (character is '\'' or '’' or '‘' or '`')
            {
                continue;
            }

            if (character == '&')
            {
                AppendWord(builder, "and");
                pendingSpace = true;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && builder.Length > 0)
            {
                builder.Append(' ');
            }

            pendingSpace = false;
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static double Similarity(string normalizedLeft, string normalizedRight)
    {
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1;
        }

        var leftIsLonger = normalizedLeft.Length >= normalizedRight.Length;
        var longer = leftIsLonger ? normalizedLeft : normalizedRight;
        var shorter = leftIsLonger ? normalizedRight : normalizedLeft;
        if (ContainsWords(longer, shorter))
        {
            return ContainmentFloor + (1 - ContainmentFloor) * shorter.Length / longer.Length;
        }

        return Dice(normalizedLeft, normalizedRight);
    }

    private static void AppendWord(StringBuilder builder, string word)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(word);
    }

    private static bool ContainsWords(string longer, string shorter)
    {
        var searchFrom = 0;
        while (searchFrom <= longer.Length - shorter.Length)
        {
            var found = longer.IndexOf(shorter, searchFrom, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            var end = found + shorter.Length;
            var startsOnBoundary = found == 0 || longer[found - 1] == ' ' || IsIdeographic(longer[found]);
            var endsOnBoundary = end == longer.Length || longer[end] == ' ' || IsIdeographic(longer[end - 1]);
            if (startsOnBoundary && endsOnBoundary)
            {
                return true;
            }

            searchFrom = found + 1;
        }

        return false;
    }

    private static bool IsIdeographic(char character)
    {
        return character >= '⺀';
    }

    private static double Dice(string left, string right)
    {
        var leftBigrams = Bigrams(left);
        var rightBigrams = Bigrams(right);
        if (leftBigrams.Length == 0 || rightBigrams.Length == 0)
        {
            return 0;
        }

        Array.Sort(leftBigrams);
        Array.Sort(rightBigrams);
        var shared = 0;
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < leftBigrams.Length && rightIndex < rightBigrams.Length)
        {
            var comparison = leftBigrams[leftIndex].CompareTo(rightBigrams[rightIndex]);
            if (comparison == 0)
            {
                shared++;
                leftIndex++;
                rightIndex++;
            }
            else if (comparison < 0)
            {
                leftIndex++;
            }
            else
            {
                rightIndex++;
            }
        }

        return 2d * shared / (leftBigrams.Length + rightBigrams.Length);
    }

    private static int[] Bigrams(string value)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        var length = compact.Length;
        if (length == 1)
        {
            return new[] { compact[0] << 16 };
        }

        var bigrams = new int[Math.Max(0, length - 1)];
        for (var index = 0; index < bigrams.Length; index++)
        {
            bigrams[index] = (compact[index] << 16) | compact[index + 1];
        }

        return bigrams;
    }
}
