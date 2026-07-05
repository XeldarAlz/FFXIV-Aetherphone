using System.Globalization;
using System.IO;

namespace Aetherphone.Core.Localization;

internal static class Loc
{
    private static string directory = string.Empty;
    private static LanguageInfo current = Languages.English;
    private static CultureInfo culture = CultureInfo.InvariantCulture;
    private static StringCatalog catalog = StringCatalog.Empty;
    public static LanguageInfo Current => current;
    public static CultureInfo Culture => culture;

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

    public static string T(LocString entry) => catalog.TryGet(entry.Key, out var value) ? value : entry.Source;
    public static string T(LocString entry, params object[] args) => string.Format(culture, T(entry), args);

    public static string Plural(LocPlural entry, int count)
    {
        var template = IsOne(count)
            ? Resolve(string.Concat(entry.KeyBase, ".one"), entry.OneSource)
            : Resolve(string.Concat(entry.KeyBase, ".other"), entry.OtherSource);
        return string.Format(culture, template, count);
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
        catalog = ReferenceEquals(language, Languages.English)
            ? StringCatalog.Empty
            : StringCatalog.Load(Path.Combine(directory, string.Concat(language.Code, ".json")));
    }

    private static CultureInfo ResolveCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
