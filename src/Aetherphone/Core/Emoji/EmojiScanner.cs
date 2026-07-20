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
    public static bool MightContain(ReadOnlySpan<char> text)
    {
        if (!EmojiCatalog.Ready)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            var value = text[index];
            if (value >= 0x0080 && EmojiCatalog.IsStarter(value))
            {
                return true;
            }
        }

        return false;
    }

    public static void Collect(string text, List<EmojiSpan> target)
    {
        var position = 0;
        var length = text.Length;
        while (position < length)
        {
            if (EmojiCatalog.TryMatch(text, position, out var matched, out var file))
            {
                target.Add(new EmojiSpan(position, matched, file));
                position += matched;
                continue;
            }

            position++;
        }
    }
}
