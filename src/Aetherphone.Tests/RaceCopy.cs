using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Tests;

internal static class RaceCopy
{
    public const float MaxTextZoom = 1.5f;

    private const float EmPixels = 16f;
    private const float SafetyMargin = 1.05f;
    private const float ThinAdvance = 0.27f;
    private const float SlimAdvance = 0.36f;
    private const float LetterAdvance = 0.56f;
    private const float DigitAdvance = 0.58f;
    private const float CapitalAdvance = 0.66f;
    private const float BroadAdvance = 0.85f;
    private const float WideAdvance = 1f;
    private const char FirstWideCharacter = (char)0x2E80;
    private const string Thin = "iljI.,:;'!|";
    private const string Slim = "frt()-";
    private const string Broad = "mwMWшщжмШЩЖМ";

    private static StringCatalog[]? catalogs;

    public static StringCatalog[] Catalogs => catalogs ??= Load();

    public static float Measure(string text, in TextStyle style, float scale, float zoom)
    {
        var ems = 0f;
        for (var index = 0; index < text.Length; index++)
        {
            ems += Advance(text[index]);
        }

        return ems * SafetyMargin * EmPixels * style.Scale * zoom * scale;
    }

    public static string Text(StringCatalog catalog, LocString entry) =>
        catalog.TryGet(entry.Key, out var value) ? value : entry.Source;

    private static float Advance(char character)
    {
        if (character >= FirstWideCharacter)
        {
            return WideAdvance;
        }

        if (character == ' ' || Thin.Contains(character, StringComparison.Ordinal))
        {
            return ThinAdvance;
        }

        if (Slim.Contains(character, StringComparison.Ordinal))
        {
            return SlimAdvance;
        }

        if (Broad.Contains(character, StringComparison.Ordinal))
        {
            return BroadAdvance;
        }

        if (char.IsDigit(character))
        {
            return DigitAdvance;
        }

        return char.IsUpper(character) ? CapitalAdvance : LetterAdvance;
    }

    private static StringCatalog[] Load()
    {
        var directory = LocalizationDirectory();
        var loaded = new StringCatalog[Languages.All.Length];
        for (var index = 0; index < loaded.Length; index++)
        {
            loaded[index] = StringCatalog.Load(Path.Combine(directory,
                string.Concat(Languages.All[index].Code, ".json")));
        }

        return loaded;
    }

    private static string LocalizationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone", "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(AppContext.BaseDirectory);
    }
}
