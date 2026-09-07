namespace Aetherphone.Core.Localization;

internal readonly record struct SpokenLanguage(int Flag, string Code, string NativeName);

internal static class SpokenLanguages
{
    public static readonly SpokenLanguage[] All =
    {
        new(1 << 0, "en", "English"),
        new(1 << 1, "ja", "日本語"),
        new(1 << 2, "de", "Deutsch"),
        new(1 << 3, "fr", "Français"),
        new(1 << 4, "es", "Español"),
        new(1 << 5, "pt", "Português"),
        new(1 << 6, "ru", "Русский"),
        new(1 << 7, "zh", "中文"),
        new(1 << 8, "tr", "Türkçe"),
        new(1 << 9, "it", "Italiano"),
        new(1 << 10, "pl", "Polski"),
    };

    public const int Mask = (1 << 11) - 1;

    public static readonly int[] Flags = BuildFlags();

    public static bool Has(int mask, int flag) => (mask & flag) != 0;

    public static int Toggle(int mask, int flag) => (mask & flag) != 0 ? mask & ~flag : mask | flag;

    public static int Sanitize(int mask) => mask & Mask;

    public static int FlagOf(string code)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return All[index].Flag;
            }
        }

        return 0;
    }

    public static string Label(int flag)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Flag == flag)
            {
                return All[index].NativeName;
            }
        }

        return string.Empty;
    }

    public static int Primary(int mask)
    {
        mask = Sanitize(mask);
        for (var index = 0; index < All.Length; index++)
        {
            if ((mask & All[index].Flag) != 0)
            {
                return All[index].Flag;
            }
        }

        return 0;
    }

    private static int[] BuildFlags()
    {
        var flags = new int[All.Length];
        for (var index = 0; index < All.Length; index++)
        {
            flags[index] = All[index].Flag;
        }

        return flags;
    }
}
