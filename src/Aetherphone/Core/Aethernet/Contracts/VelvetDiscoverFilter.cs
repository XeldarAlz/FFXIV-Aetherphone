namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record VelvetDiscoverFilter(
    int IntentInclude,
    int IntentExclude,
    int GenderInclude,
    int GenderExclude,
    int SexualityInclude,
    int SexualityExclude,
    int RelationshipInclude,
    int RelationshipExclude,
    string[] RolesInclude,
    string[] RolesExclude,
    string[] KinksInclude,
    string[] KinksExclude,
    string[] LimitsInclude,
    string[] LimitsExclude,
    string[] TagsInclude,
    string[] TagsExclude,
    int RaceInclude = 0,
    int RaceExclude = 0,
    int ActiveWithinDays = 0,
    bool HasPhoto = false,
    int LanguagesInclude = 0,
    int LanguagesExclude = 0)
{
    public static readonly VelvetDiscoverFilter Empty = new(0, 0, 0, 0, 0, 0, 0, 0,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public bool IsEmpty =>
        IntentInclude == 0 && IntentExclude == 0 && GenderInclude == 0 && GenderExclude == 0
        && SexualityInclude == 0 && SexualityExclude == 0
        && RelationshipInclude == 0 && RelationshipExclude == 0
        && RolesInclude.Length == 0 && RolesExclude.Length == 0
        && KinksInclude.Length == 0 && KinksExclude.Length == 0
        && LimitsInclude.Length == 0 && LimitsExclude.Length == 0
        && TagsInclude.Length == 0 && TagsExclude.Length == 0
        && RaceInclude == 0 && RaceExclude == 0
        && ActiveWithinDays == 0 && !HasPhoto
        && LanguagesInclude == 0 && LanguagesExclude == 0;

    public bool Matches(VelvetDiscoverFilter other) =>
        IntentInclude == other.IntentInclude && IntentExclude == other.IntentExclude
        && GenderInclude == other.GenderInclude && GenderExclude == other.GenderExclude
        && SexualityInclude == other.SexualityInclude && SexualityExclude == other.SexualityExclude
        && RelationshipInclude == other.RelationshipInclude && RelationshipExclude == other.RelationshipExclude
        && SameTokens(RolesInclude, other.RolesInclude) && SameTokens(RolesExclude, other.RolesExclude)
        && SameTokens(KinksInclude, other.KinksInclude) && SameTokens(KinksExclude, other.KinksExclude)
        && SameTokens(LimitsInclude, other.LimitsInclude) && SameTokens(LimitsExclude, other.LimitsExclude)
        && SameTokens(TagsInclude, other.TagsInclude) && SameTokens(TagsExclude, other.TagsExclude)
        && RaceInclude == other.RaceInclude && RaceExclude == other.RaceExclude
        && ActiveWithinDays == other.ActiveWithinDays && HasPhoto == other.HasPhoto
        && LanguagesInclude == other.LanguagesInclude && LanguagesExclude == other.LanguagesExclude;

    public static bool SameTokens(string[] left, string[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
