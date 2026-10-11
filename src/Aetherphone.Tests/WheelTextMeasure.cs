using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Tests;

internal static class WheelTextMeasure
{
    public const float EmPixels = 17f;
    public const float LineSpacing = 4f;

    private const float WideAdvance = 1f;
    private const float CapitalAdvance = 0.74f;
    private const float LetterAdvance = 0.62f;
    private const float SpaceAdvance = 0.3f;
    private const float NarrowAdvance = 0.34f;
    private const int WideFrom = 0x2E80;

    private static readonly Dictionary<string, StringCatalog> Catalogs = new(StringComparer.Ordinal);

    public static float Width(string text, in TextStyle style, float scale)
    {
        var em = EmPixels * style.Scale * scale;
        var total = 0f;
        for (var index = 0; index < text.Length; index++)
        {
            total += em * Advance(text[index]);
        }

        return total;
    }

    public static bool Inside(Rect inner, Rect outer, float slack = 0.01f) =>
        inner.Min.X >= outer.Min.X - slack && inner.Min.Y >= outer.Min.Y - slack
        && inner.Max.X <= outer.Max.X + slack && inner.Max.Y <= outer.Max.Y + slack;

    public static bool Disjoint(Rect first, Rect second) =>
        first.Max.X <= second.Min.X + 0.01f || second.Max.X <= first.Min.X + 0.01f
        || first.Max.Y <= second.Min.Y + 0.01f || second.Max.Y <= first.Min.Y + 0.01f;

    public static Rect Capsule(in CasinoStageLayout layout, float scale)
    {
        var half = new Vector2(layout.CapsuleMaxWidth * 0.5f, CasinoStageLayout.CapsuleHeight * 0.5f * scale);
        return new Rect(layout.CapsuleCenter - half, layout.CapsuleCenter + half);
    }

    public static Rect Phone(float scale) =>
        new(new Vector2(100f, 50f),
            new Vector2(100f + PhoneSizeCatalog.DesignWidth * scale, 50f + PhoneSizeCatalog.DesignHeight * scale));

    public static CasinoStageLayout Stage(float scale, float deckHeight) =>
        CasinoStageLayout.Compute(Phone(scale), true, false, deckHeight, scale);

    public static string[] Translations(LocString entry)
    {
        var values = new string[Languages.All.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var code = Languages.All[index].Code;
            values[index] = Catalog(code).TryGet(entry.Key, out var value) ? value : entry.Source;
        }

        return values;
    }

    public static float Widest(LocString entry, in TextStyle style, float scale)
    {
        var values = Translations(entry);
        var widest = 0f;
        for (var index = 0; index < values.Length; index++)
        {
            widest = MathF.Max(widest, Width(values[index], style, scale));
        }

        return widest;
    }

    public static float Widest(LocString entry, string argument, in TextStyle style, float scale)
    {
        var values = Translations(entry);
        var widest = 0f;
        for (var index = 0; index < values.Length; index++)
        {
            widest = MathF.Max(widest, Width(string.Format(values[index], argument), style, scale));
        }

        return widest;
    }

    private static float Advance(char character)
    {
        if (character >= WideFrom)
        {
            return WideAdvance;
        }

        if (character == ' ')
        {
            return SpaceAdvance;
        }

        if (character is '.' or ',' or ':' or '\'' or 'i' or 'l' or 'I' or '!')
        {
            return NarrowAdvance;
        }

        return char.IsUpper(character) || character is 'm' or 'w' or 'ш' or 'щ' or 'ж' or 'ю' or 'ы'
            ? CapitalAdvance
            : LetterAdvance;
    }

    private static StringCatalog Catalog(string code)
    {
        lock (Catalogs)
        {
            if (Catalogs.TryGetValue(code, out var cached))
            {
                return cached;
            }

            var catalog = StringCatalog.Load(Path.Combine(LocalizationDirectory(), string.Concat(code, ".json")));
            Catalogs[code] = catalog;
            return catalog;
        }
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

        throw new DirectoryNotFoundException("Could not locate src/Aetherphone/Localization.");
    }
}
