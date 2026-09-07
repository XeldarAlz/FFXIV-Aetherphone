namespace Aetherphone.Core.Localization;

internal sealed class GlyphCoverage
{
    private const int WordCount = (char.MaxValue + 1) / 64;
    private static readonly ushort[] EmptyRanges = { 0x0000 };
    private readonly ulong[] words = new ulong[WordCount];
    private int count;

    public int Count => count;

    public void Clear()
    {
        Array.Clear(words, 0, WordCount);
        count = 0;
    }

    public bool Contains(int codepoint) => (words[codepoint >> 6] & (1UL << (codepoint & 63))) != 0;

    public bool Add(int codepoint)
    {
        if (Contains(codepoint))
        {
            return false;
        }

        words[codepoint >> 6] |= 1UL << (codepoint & 63);
        count++;
        return true;
    }

    public void AddRanges(ushort[] ranges) => AddRanges(ranges, null);

    public void AddRanges(ushort[] ranges, GlyphCoverage? excluded)
    {
        for (var index = 0; index + 1 < ranges.Length; index += 2)
        {
            var first = ranges[index];
            if (first == 0)
            {
                return;
            }

            var last = ranges[index + 1];
            for (int codepoint = first; codepoint <= last; codepoint++)
            {
                if (excluded is not null && excluded.Contains(codepoint))
                {
                    continue;
                }

                Add(codepoint);
            }
        }
    }

    public ushort[] ToRanges(int firstCodepoint)
    {
        if (count == 0)
        {
            return EmptyRanges;
        }

        var runCount = 0;
        var previous = false;
        for (var codepoint = firstCodepoint; codepoint <= char.MaxValue; codepoint++)
        {
            var present = Contains(codepoint);
            if (present && !previous)
            {
                runCount++;
            }

            previous = present;
        }

        var ranges = new ushort[runCount * 2 + 1];
        var offset = 0;
        var runStart = -1;
        for (var codepoint = firstCodepoint; codepoint <= char.MaxValue; codepoint++)
        {
            var present = Contains(codepoint);
            if (present && runStart < 0)
            {
                runStart = codepoint;
                continue;
            }

            if (present || runStart < 0)
            {
                continue;
            }

            ranges[offset++] = (ushort)runStart;
            ranges[offset++] = (ushort)(codepoint - 1);
            runStart = -1;
        }

        if (runStart >= 0)
        {
            ranges[offset++] = (ushort)runStart;
            ranges[offset] = char.MaxValue;
        }

        return ranges;
    }
}

internal static class GlyphPlan
{
    public const int FirstSharedCodepoint = 0x0080;
    public const int FirstGameSymbol = 0xE020;
    public const int LastGameSymbol = 0xE0E9;

    // Square Enix maps its chat symbols (boxed numbers and letters, arrows, quality marks) into this
    // private use block; the bounds are the first and last member of Dalamud's SeIconChar.
    private static readonly ushort[] GameSymbolRanges = { FirstGameSymbol, LastGameSymbol, 0x0000 };

    private static readonly ushort[] BaseRanges =
    {
        0x0020, 0x00FF,
        0x0100, 0x017F,
        0x2000, 0x206F,
        0x2200, 0x22FF,
        0x25A0, 0x27BF,
    };

    // Scripts other players write in that no UI language bakes natively: Latin Extended-B, combining marks,
    // Greek, Cyrillic, Latin Extended Additional, currency, letterlike, arrows, CJK punctuation, kana,
    // halfwidth and fullwidth forms. Rasterized once in the shared font, never per weight and size.
    private static readonly ushort[] SharedBaseRanges =
    {
        0x0180, 0x024F,
        0x0300, 0x036F,
        0x0370, 0x03FF,
        0x0400, 0x04FF,
        0x0500, 0x052F,
        0x1E00, 0x1EFF,
        0x20A0, 0x20BF,
        0x2100, 0x214F,
        0x2190, 0x21FF,
        0x3000, 0x303F,
        0x3040, 0x309F,
        0x30A0, 0x30FF,
        0x31F0, 0x31FF,
        0xFF00, 0xFFEF,
    };

    private static readonly ushort[] NativeNameRanges = ComposeNativeNameRanges();

    public static ushort[] SharedBase => SharedBaseRanges;

    public static ushort[] GameSymbols => GameSymbolRanges;

    public static bool IsGameSymbol(int codepoint) => codepoint >= FirstGameSymbol && codepoint <= LastGameSymbol;

    public static bool IsSharedBase(int codepoint)
    {
        for (var index = 0; index + 1 < SharedBaseRanges.Length; index += 2)
        {
            if (codepoint >= SharedBaseRanges[index] && codepoint <= SharedBaseRanges[index + 1])
            {
                return true;
            }
        }

        return false;
    }

    public static ushort[] Native(LanguageInfo language)
    {
        var extra = language.ExtraGlyphRanges;
        var extraLength = extra?.Length ?? 0;
        var combined = new ushort[BaseRanges.Length + NativeNameRanges.Length + extraLength + 1];
        var offset = 0;
        Array.Copy(BaseRanges, 0, combined, offset, BaseRanges.Length);
        offset += BaseRanges.Length;
        Array.Copy(NativeNameRanges, 0, combined, offset, NativeNameRanges.Length);
        offset += NativeNameRanges.Length;
        if (extraLength > 0)
        {
            Array.Copy(extra!, 0, combined, offset, extraLength);
        }

        return combined;
    }

    private static ushort[] ComposeNativeNameRanges()
    {
        var coverage = new GlyphCoverage();
        for (var languageIndex = 0; languageIndex < Languages.All.Length; languageIndex++)
        {
            AddName(coverage, Languages.All[languageIndex].NativeName);
        }

        for (var languageIndex = 0; languageIndex < SpokenLanguages.All.Length; languageIndex++)
        {
            AddName(coverage, SpokenLanguages.All[languageIndex].NativeName);
        }

        var ranges = coverage.ToRanges(0x0020);
        return ranges[ranges.Length - 1] == 0 ? ranges[..^1] : ranges;
    }

    private static void AddName(GlyphCoverage coverage, string name)
    {
        for (var charIndex = 0; charIndex < name.Length; charIndex++)
        {
            coverage.Add(name[charIndex]);
        }
    }
}
