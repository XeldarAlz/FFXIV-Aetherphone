namespace Aetherphone.Core.Emoji;

internal readonly struct EmojiSpan
{
    public readonly int Start;
    public readonly int Length;
    public readonly string File;

    public EmojiSpan(int start, int length, string file)
    {
        Start = start;
        Length = length;
        File = file;
    }
}

internal static class EmojiScanner
{
    public static bool MightContain(ReadOnlySpan<char> text) =>
        EmojiCatalog.Ready && text.IndexOf(':') >= 0;

    public static void Collect(string text, List<EmojiSpan> target)
    {
        var length = text.Length;
        var index = 0;
        while (index < length)
        {
            if (text[index] != ':')
            {
                index++;
                continue;
            }

            var end = index + 1;
            while (end < length && IsShortcodeChar(text[end]))
            {
                end++;
            }

            if (end < length && end > index + 1 && text[end] == ':'
                && EmojiCatalog.TryResolve(text.Substring(index + 1, end - index - 1), out var file))
            {
                target.Add(new EmojiSpan(index, end - index + 1, file));
                index = end + 1;
                continue;
            }

            index++;
        }
    }

    private static bool IsShortcodeChar(char value) =>
        value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';
}
