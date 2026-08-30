namespace Aetherphone.Core.Localization;

internal static class CountText
{
    public static string Exact(int value) => value.ToString("N0", Loc.Culture);

    public static string Compact(int value)
    {
        if (value < 1000)
        {
            return value.ToString(Loc.Culture);
        }

        if (value < 10000)
        {
            return (value / 1000f).ToString("0.#", Loc.Culture) + "K";
        }

        if (value < 1000000)
        {
            return (value / 1000).ToString(Loc.Culture) + "K";
        }

        return (value / 1000000f).ToString("0.#", Loc.Culture) + "M";
    }
}
