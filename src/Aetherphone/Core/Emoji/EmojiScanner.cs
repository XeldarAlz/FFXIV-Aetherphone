using System.Buffers;
using System.Globalization;
using System.Text;

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
    public const string MissingFile = "missing";

    private const int SequenceKeyCapacity = 64;
    private const char AsciiLimit = '\u0080';
    private const char SequenceSeparator = '-';

    public static bool MightContain(ReadOnlySpan<char> text) => MightContain(text, true);

    public static bool MightContain(ReadOnlySpan<char> text, bool shortcodes) =>
        EmojiCatalog.Ready && ((shortcodes && text.Contains(':')) || EmojiCatalog.MightHaveSequence(text));

    public static bool IsMissing(string file) => string.Equals(file, MissingFile, StringComparison.Ordinal);

    public static void Collect(string text, List<EmojiSpan> target) => Collect(text, target, true);

    public static void Collect(string text, List<EmojiSpan> target, bool shortcodes)
    {
        var sequences = EmojiCatalog.MightHaveSequence(text);
        if (!shortcodes && !sequences)
        {
            return;
        }

        var length = text.Length;
        var index = 0;
        while (index < length)
        {
            if (shortcodes && text[index] == ':' && TryShortcodeAt(text, index, out var shortcodeLength,
                    out var shortcodeFile))
            {
                target.Add(new EmojiSpan(index, shortcodeLength, shortcodeFile));
                index += shortcodeLength;
                continue;
            }

            if (!sequences)
            {
                index++;
                continue;
            }

            var clusterLength = ClusterLength(text.AsSpan(index));
            if (TryResolveCluster(text.AsSpan(index, clusterLength), out var clusterFile))
            {
                target.Add(new EmojiSpan(index, clusterLength, clusterFile));
            }

            index += clusterLength;
        }
    }

    public static bool IsShortcodeChar(char value) =>
        value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';

    private static bool TryShortcodeAt(string text, int start, out int length, out string file)
    {
        var end = start + 1;
        while (end < text.Length && IsShortcodeChar(text[end]))
        {
            end++;
        }

        length = end - start + 1;
        file = string.Empty;
        return end < text.Length && end > start + 1 && text[end] == ':'
               && EmojiCatalog.TryResolve(text.AsSpan(start + 1, end - start - 1), out file);
    }

    private static int ClusterLength(ReadOnlySpan<char> remaining)
    {
        if (remaining.Length == 1 || (remaining[0] < AsciiLimit && remaining[1] < AsciiLimit))
        {
            return 1;
        }

        return StringInfo.GetNextTextElementLength(remaining);
    }

    private static bool TryResolveCluster(ReadOnlySpan<char> cluster, out string file)
    {
        file = MissingFile;
        if (!EmojiCatalog.MightHaveSequence(cluster))
        {
            return false;
        }

        var unrenderable = char.IsSurrogate(cluster[0]);
        Span<char> key = stackalloc char[SequenceKeyCapacity];
        if (!TryWriteKey(cluster, key, out var keyLength, out var lead, out var qualified))
        {
            return unrenderable;
        }

        if (lead < EmojiCatalog.BareSequenceFloor && !qualified)
        {
            return false;
        }

        if (EmojiCatalog.TryResolveSequence(key[..keyLength], out var resolved))
        {
            file = resolved;
            return true;
        }

        return unrenderable;
    }

    private static bool TryWriteKey(ReadOnlySpan<char> cluster, Span<char> key, out int keyLength, out int lead,
        out bool qualified)
    {
        keyLength = 0;
        lead = -1;
        qualified = false;
        var offset = 0;
        while (offset < cluster.Length)
        {
            if (Rune.DecodeFromUtf16(cluster[offset..], out var rune, out var consumed) != OperationStatus.Done)
            {
                return false;
            }

            offset += consumed;
            if (lead < 0)
            {
                lead = rune.Value;
            }

            if (rune.Value == EmojiCatalog.PresentationSelector)
            {
                qualified = true;
                continue;
            }

            qualified |= rune.Value == EmojiCatalog.KeycapMark;
            if (keyLength > 0)
            {
                if (keyLength == key.Length)
                {
                    return false;
                }

                key[keyLength++] = SequenceSeparator;
            }

            if (!rune.Value.TryFormat(key[keyLength..], out var written, "x", CultureInfo.InvariantCulture))
            {
                return false;
            }

            keyLength += written;
        }

        return keyLength > 0;
    }
}
