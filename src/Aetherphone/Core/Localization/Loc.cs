using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Localization;

internal static class Loc
{
    private static string directory = string.Empty;
    private static LanguageInfo current = Languages.English;
    private static CultureInfo culture = CultureInfo.InvariantCulture;
    private static StringCatalog catalog = StringCatalog.Empty;
    private static ushort[] glyphs = Array.Empty<ushort>();
    private static readonly ConcurrentDictionary<string, string> UpperCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CompositeFormat> FormatCache = new(StringComparer.Ordinal);
    public static LanguageInfo Current => current;
    public static CultureInfo Culture => culture;
    public static ushort[] CatalogGlyphs => glyphs;

    public static void Initialize(string code, string localizationDirectory)
    {
        directory = localizationDirectory;
        Apply(Languages.Resolve(code));
#if DEBUG
        LocAudit.Run(directory);
#endif
    }

    public static void SetLanguage(string code)
    {
        var target = Languages.Resolve(code);
        if (ReferenceEquals(target, current))
        {
            return;
        }

        Apply(target);
    }

    public static string Upper(string text)
    {
        if (UpperCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        var upper = culture.TextInfo.ToUpper(text);
        UpperCache[text] = upper;
        return upper;
    }

    public static string T(LocString entry) => catalog.TryGet(entry.Key, out var value) ? value : entry.Source;

    public static string T<T0>(LocString entry, T0 arg0) => string.Format(culture, FormatFor(T(entry)), arg0);

    public static string T<T0, T1>(LocString entry, T0 arg0, T1 arg1) =>
        string.Format(culture, FormatFor(T(entry)), arg0, arg1);

    public static string T<T0, T1, T2>(LocString entry, T0 arg0, T1 arg1, T2 arg2) =>
        string.Format(culture, FormatFor(T(entry)), arg0, arg1, arg2);

    public static string T(LocString entry, params object[] args) => string.Format(culture, T(entry), args);

    public static string Plural(LocPlural entry, int count) =>
        string.Format(culture, FormatFor(PluralTemplate(entry, count)), count);

    public static string Plural<T1>(LocPlural entry, int count, T1 arg1) =>
        string.Format(culture, FormatFor(PluralTemplate(entry, count)), count, arg1);

    public static string Plural<T1, T2>(LocPlural entry, int count, T1 arg1, T2 arg2) =>
        string.Format(culture, FormatFor(PluralTemplate(entry, count)), count, arg1, arg2);

    private static string PluralTemplate(LocPlural entry, int count) => IsOne(count)
        ? Resolve(string.Concat(entry.KeyBase, ".one"), entry.OneSource)
        : Resolve(string.Concat(entry.KeyBase, ".other"), entry.OtherSource);

    private static CompositeFormat FormatFor(string template)
    {
        if (FormatCache.TryGetValue(template, out var cached))
        {
            return cached;
        }

        var parsed = CompositeFormat.Parse(template);
        FormatCache[template] = parsed;
        return parsed;
    }

    private static string Resolve(string key, string source) => catalog.TryGet(key, out var value) ? value : source;

    private static bool IsOne(int count)
    {
        var magnitude = Math.Abs(count);
        return current.PluralKind switch
        {
            PluralKind.French => magnitude is 0 or 1,
            _ => magnitude == 1,
        };
    }

    private static void Apply(LanguageInfo language)
    {
        current = language;
        culture = ResolveCulture(language.CultureName);
        UpperCache.Clear();
        FormatCache.Clear();
        var path = Path.Combine(directory, string.Concat(language.Code, ".json"));
        catalog = ReferenceEquals(language, Languages.English) ? StringCatalog.Empty : StringCatalog.Load(path);
        glyphs = StringCatalog.ScanGlyphs(path);
    }

    private static CultureInfo ResolveCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException exception)
        {
            AepLog.Warning(exception, $"Culture '{name}' is unavailable; falling back to the invariant culture");
            return CultureInfo.InvariantCulture;
        }
    }
}
