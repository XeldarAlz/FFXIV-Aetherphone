namespace Aetherphone.Core.Localization;

internal enum PluralKind : byte
{
    EnglishLike,
    French,
}

internal sealed class LanguageInfo
{
    public LanguageInfo(string code, string nativeName, string englishName, string cultureName, PluralKind pluralKind,
        ushort[]? extraGlyphRanges)
    {
        Code = code;
        NativeName = nativeName;
        EnglishName = englishName;
        CultureName = cultureName;
        PluralKind = pluralKind;
        ExtraGlyphRanges = extraGlyphRanges;
    }

    public string Code { get; }
    public string NativeName { get; }
    public string EnglishName { get; }
    public string CultureName { get; }
    public PluralKind PluralKind { get; }
    public ushort[]? ExtraGlyphRanges { get; }
}

internal static class Languages
{
    // Han ideographs are deliberately absent: baking the full CJK unified block into every weight and
    // size combination overflows the font atlas. Ideographs stream in through the FontService glyph
    // ledger the first time they are displayed and persist across sessions.
    private static readonly ushort[] JapaneseGlyphRanges =
    {
        0x3000, 0x303F, // CJK symbols and punctuation
        0x3040, 0x309F, // Hiragana
        0x30A0, 0x30FF, // Katakana
        0x31F0, 0x31FF, // Katakana phonetic extensions
        0xFF00, 0xFFEF, // Halfwidth and fullwidth forms
    };

    private static readonly ushort[] ChineseGlyphRanges =
    {
        0x3000, 0x303F, // CJK symbols and punctuation
        0xFF00, 0xFFEF, // Halfwidth and fullwidth forms
    };

    public static readonly LanguageInfo
        English = new("en", "English", "English", "en-US", PluralKind.EnglishLike, null);

    public static readonly LanguageInfo French = new("fr", "Français", "French", "fr-FR", PluralKind.French, null);
    public static readonly LanguageInfo German = new("de", "Deutsch", "German", "de-DE", PluralKind.EnglishLike, null);
    public static readonly LanguageInfo Turkish = new("tr", "Türkçe", "Turkish", "tr-TR", PluralKind.EnglishLike, null);
    public static readonly LanguageInfo Spanish = new("es", "Español", "Spanish", "es-ES", PluralKind.EnglishLike, null);
    public static readonly LanguageInfo Russian = new("ru", "Русский", "Russian", "ru-RU", PluralKind.EnglishLike, new ushort[] { 0x0400, 0x04FF });
    public static readonly LanguageInfo Japanese = new("ja", "日本語", "Japanese", "ja-JP", PluralKind.EnglishLike, JapaneseGlyphRanges);
    public static readonly LanguageInfo Chinese = new("zh", "中文", "Chinese", "zh-CN", PluralKind.EnglishLike, ChineseGlyphRanges);

    public static readonly LanguageInfo[] All =
    {
        English, French, German, Turkish, Spanish, Russian, Japanese, Chinese,
    };

    public static LanguageInfo Resolve(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return English;
        }

        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return All[index];
            }
        }

        return English;
    }
}
