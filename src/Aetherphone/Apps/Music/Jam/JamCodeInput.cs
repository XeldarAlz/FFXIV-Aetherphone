namespace Aetherphone.Apps.Music.Jam;

internal static class JamCodeInput
{
    public const int Length = 6;
    public const int GroupLength = 3;

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[Length];
        if (TryTrailingCode(input, buffer))
        {
            return new string(buffer);
        }

        var count = 0;
        for (var index = 0; index < input.Length && count < Length; index++)
        {
            var character = input[index];
            if (char.IsAsciiLetterOrDigit(character))
            {
                buffer[count] = char.ToUpperInvariant(character);
                count++;
            }
        }

        return count == 0 ? string.Empty : new string(buffer[..count]);
    }

    private static bool TryTrailingCode(string input, Span<char> buffer)
    {
        if (CountAlphanumeric(input) <= Length)
        {
            return false;
        }

        var end = input.Length;
        var lastStart = TokenStart(input, ref end, out var lastLength);
        if (lastLength == Length)
        {
            Copy(input, lastStart, Length, buffer, 0);
            return true;
        }

        if (lastLength != GroupLength)
        {
            return false;
        }

        var firstStart = TokenStart(input, ref end, out var firstLength);
        if (firstLength != GroupLength)
        {
            return false;
        }

        Copy(input, firstStart, GroupLength, buffer, 0);
        Copy(input, lastStart, GroupLength, buffer, GroupLength);
        return true;
    }

    private static int TokenStart(string input, ref int end, out int length)
    {
        while (end > 0 && !char.IsAsciiLetterOrDigit(input[end - 1]))
        {
            end--;
        }

        var start = end;
        while (start > 0 && char.IsAsciiLetterOrDigit(input[start - 1]))
        {
            start--;
        }

        length = end - start;
        end = start;
        return start;
    }

    private static int CountAlphanumeric(string input)
    {
        var count = 0;
        for (var index = 0; index < input.Length; index++)
        {
            if (char.IsAsciiLetterOrDigit(input[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static void Copy(string input, int start, int length, Span<char> buffer, int offset)
    {
        for (var index = 0; index < length; index++)
        {
            buffer[offset + index] = char.ToUpperInvariant(input[start + index]);
        }
    }
}
