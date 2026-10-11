using Aetherphone.Core.Localization;

namespace Aetherphone.Tests;

internal static class DeckLabelMeasure
{
    public const float LatinWidth = 9f;
    public const float WideWidth = 17f;
    public const char FirstWideCodepoint = (char)0x2E80;

    private static readonly string[] Codes = { "de", "en", "es", "fr", "ja", "pt", "ru", "tr", "zh" };

    public static float Width(string text, float scale)
    {
        var width = 0f;
        for (var index = 0; index < text.Length; index++)
        {
            width += text[index] >= FirstWideCodepoint ? WideWidth : LatinWidth;
        }

        return width * scale;
    }

    public static string[] Translations(LocString key)
    {
        var directory = LocalizationDirectory();
        var values = new string[Codes.Length];
        for (var index = 0; index < Codes.Length; index++)
        {
            var catalog = StringCatalog.Load(Path.Combine(directory, string.Concat(Codes[index], ".json")));
            values[index] = catalog.TryGet(key.Key, out var value) ? value : key.Source;
        }

        return values;
    }

    public static string Widest(LocString key, float scale)
    {
        var values = Translations(key);
        var widest = values[0];
        for (var index = 1; index < values.Length; index++)
        {
            if (Width(values[index], scale) > Width(widest, scale))
            {
                widest = values[index];
            }
        }

        return widest;
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

        throw new DirectoryNotFoundException("src/Aetherphone/Localization was not found above the test output.");
    }
}
