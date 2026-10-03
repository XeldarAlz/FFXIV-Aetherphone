using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Settings;

internal static class SettingsSearch
{
    private const CompareOptions MatchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
                                                CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;

    public static bool Matches(CompareInfo compare, string text, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        if (text.Length == 0)
        {
            return false;
        }

        return compare.IndexOf(text, query, MatchOptions) >= 0;
    }

    public static bool MatchesPage(CompareInfo compare, string title, string summary, string query) =>
        Matches(compare, title, query) || Matches(compare, summary, query);

    public static bool MatchesEntry(CompareInfo compare, in SettingsEntry entry, string query) =>
        query.Length > 0 && Matches(compare, Loc.T(entry.Label), query);

    public static bool MatchesApp(CompareInfo compare, string displayName, string query) =>
        query.Length > 0 && Matches(compare, displayName, query);
}
