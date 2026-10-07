namespace Aetherphone.Core.Shell.Spotlight;

internal static class SpotlightMatch
{
    private const int ExactQuality = 1000;
    private const int PrefixQuality = 800;
    private const int WordStartQuality = 600;
    private const int ContainsQuality = 400;
    private const int LengthPenaltyCap = 48;

    public static int Score(string text, string query)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var position = text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (position < 0)
        {
            return 0;
        }

        int quality;
        if (position == 0)
        {
            quality = text.Length == query.Length ? ExactQuality : PrefixQuality;
        }
        else
        {
            quality = IsWordStart(text, position) ? WordStartQuality : ContainsQuality;
        }

        return quality - Math.Min(text.Length, LengthPenaltyCap);
    }

    private static bool IsWordStart(string text, int position) => !char.IsLetterOrDigit(text[position - 1]);
}
