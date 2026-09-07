using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetFitKind
{
    SharedKinks,
    SharedIntent,
    SharedTag,
    SharedLanguage,
    Conflict,
    NoSharedLanguage,
    NoConflicts,
}

internal enum VelvetTokenGroup
{
    None,
    Kinks,
    Tags,
    Limits,
}

internal enum VelvetTokenMatch
{
    None,
    Shared,
    Conflict,
}

internal readonly record struct VelvetFitItem(VelvetFitKind Kind, string Token, int Value);

internal static class VelvetFit
{
    public const int MaxItems = 4;

    private const int MaxConflicts = 2;
    private const int MaxSharedTags = 2;
    private const int AvatarWeight = 3;
    private const int MaxPhotoWeight = 3;
    private const int IntroWeight = 2;
    private const int SharedKinkWeight = 2;
    private const int ConflictWeight = 2;
    private const int SharedLanguageWeight = 2;
    private const int LanguageGapWeight = 2;
    private const int OnlineWeight = 40;
    private const int AwayWeight = 20;
    private const string IrlLimit = "irl";

    private const int DeliberateIntents = VelvetIntent.Erp | VelvetIntent.Gpose | VelvetIntent.Relationship
        | VelvetIntent.Collab | VelvetIntent.Sharing | VelvetIntent.Irl;

    private static readonly string[] NoTokens = Array.Empty<string>();

    public static void Describe(VelvetProfileDto me, VelvetProfileDto other, List<VelvetFitItem> items)
    {
        items.Clear();
        var sharedKinks = SharedCount(me.Kinks, other.Kinks);
        if (sharedKinks > 0)
        {
            items.Add(new VelvetFitItem(VelvetFitKind.SharedKinks, string.Empty, sharedKinks));
        }

        var conflicts = AddConflicts(me, other, items);
        AddLanguage(me, other, items);
        var sharedIntent = me.LookingFor & other.LookingFor & DeliberateIntents;
        if (sharedIntent != 0)
        {
            items.Add(new VelvetFitItem(VelvetFitKind.SharedIntent, string.Empty, VelvetIntent.Primary(sharedIntent)));
        }

        AddSharedTags(me.Tags, other.Tags, items);
        if (conflicts == 0 && HasBoundariesToCompare(me, other))
        {
            items.Add(new VelvetFitItem(VelvetFitKind.NoConflicts, string.Empty, 0));
        }

        if (items.Count > MaxItems)
        {
            items.RemoveRange(MaxItems, items.Count - MaxItems);
        }
    }

    public static int Score(VelvetProfileDto? me, VelvetProfileDto other)
    {
        var score = other.Presence switch
        {
            VelvetPresence.Online => OnlineWeight,
            VelvetPresence.Away or VelvetPresence.Dnd => AwayWeight,
            _ => 0,
        };
        if (!string.IsNullOrEmpty(other.AvatarUrl))
        {
            score += AvatarWeight;
        }

        score += Math.Min(other.Photos?.Length ?? 0, MaxPhotoWeight);

        if (other.Intro.Length > 0)
        {
            score += IntroWeight;
        }

        if (other.Tags.Length > 0)
        {
            score++;
        }

        if (other.Kinks is { Length: > 0 })
        {
            score++;
        }

        if (me is null)
        {
            return score;
        }

        score += SharedCount(me.Kinks, other.Kinks) * SharedKinkWeight;
        score += BitOperations.PopCount((uint)(me.LookingFor & other.LookingFor & DeliberateIntents));
        score += SharedCount(me.Tags, other.Tags);
        score -= ConflictCount(me, other) * ConflictWeight;
        score += LanguageScore(me, other);
        return score;
    }

    private static int LanguageScore(VelvetProfileDto me, VelvetProfileDto other)
    {
        if (!BothChoseLanguages(me, other))
        {
            return 0;
        }

        return VelvetLanguages.Shared(me.Languages, other.Languages) != 0 ? SharedLanguageWeight : -LanguageGapWeight;
    }

    private static void AddLanguage(VelvetProfileDto me, VelvetProfileDto other, List<VelvetFitItem> items)
    {
        if (!BothChoseLanguages(me, other))
        {
            return;
        }

        var shared = VelvetLanguages.Shared(me.Languages, other.Languages);
        items.Add(shared != 0
            ? new VelvetFitItem(VelvetFitKind.SharedLanguage, string.Empty, SpokenLanguages.Primary(shared))
            : new VelvetFitItem(VelvetFitKind.NoSharedLanguage, string.Empty, 0));
    }

    private static bool BothChoseLanguages(VelvetProfileDto me, VelvetProfileDto other) =>
        VelvetLanguages.Sanitize(me.Languages) != 0 && VelvetLanguages.Sanitize(other.Languages) != 0;

    public static int SharedCount(string[]? first, string[]? second)
    {
        var left = first ?? NoTokens;
        var right = second ?? NoTokens;
        var count = 0;
        for (var index = 0; index < left.Length; index++)
        {
            if (Contains(right, left[index]))
            {
                count++;
            }
        }

        return count;
    }

    public static int ConflictCount(VelvetProfileDto me, VelvetProfileDto other)
    {
        var count = SharedCount(other.Limits, me.Kinks) + SharedCount(me.Limits, other.Kinks);
        return IrlConflict(me, other) ? count + 1 : count;
    }

    public static VelvetTokenMatch Match(VelvetProfileDto? me, VelvetTokenGroup group, string token)
    {
        if (me is null)
        {
            return VelvetTokenMatch.None;
        }

        switch (group)
        {
            case VelvetTokenGroup.Kinks:
                return Contains(me.Limits, token) ? VelvetTokenMatch.Conflict
                    : Contains(me.Kinks, token) ? VelvetTokenMatch.Shared : VelvetTokenMatch.None;
            case VelvetTokenGroup.Tags:
                return Contains(me.Tags, token) ? VelvetTokenMatch.Shared : VelvetTokenMatch.None;
            case VelvetTokenGroup.Limits:
                return LimitClashes(me, token) ? VelvetTokenMatch.Conflict
                    : Contains(me.Limits, token) ? VelvetTokenMatch.Shared : VelvetTokenMatch.None;
            default:
                return VelvetTokenMatch.None;
        }
    }

    private static bool LimitClashes(VelvetProfileDto me, string token) =>
        Contains(me.Kinks, token)
        || (string.Equals(token, IrlLimit, StringComparison.OrdinalIgnoreCase)
            && VelvetIntent.Has(me.LookingFor, VelvetIntent.Irl));

    private static int AddConflicts(VelvetProfileDto me, VelvetProfileDto other, List<VelvetFitItem> items)
    {
        var count = AddOverlap(other.Limits, me.Kinks, items, MaxConflicts);
        count += AddOverlap(me.Limits, other.Kinks, items, MaxConflicts - count);
        if (count < MaxConflicts && IrlConflict(me, other) && !HasConflict(items, IrlLimit))
        {
            items.Add(new VelvetFitItem(VelvetFitKind.Conflict, IrlLimit, 0));
            count++;
        }

        return count;
    }

    private static int AddOverlap(string[]? limits, string[]? kinks, List<VelvetFitItem> items, int room)
    {
        if (room <= 0 || limits is null || kinks is null)
        {
            return 0;
        }

        var added = 0;
        for (var index = 0; index < limits.Length && added < room; index++)
        {
            var token = limits[index];
            if (Contains(kinks, token) && !HasConflict(items, token))
            {
                items.Add(new VelvetFitItem(VelvetFitKind.Conflict, token, 0));
                added++;
            }
        }

        return added;
    }

    private static void AddSharedTags(string[] mine, string[] theirs, List<VelvetFitItem> items)
    {
        var added = 0;
        for (var index = 0; index < mine.Length && added < MaxSharedTags; index++)
        {
            if (Contains(theirs, mine[index]))
            {
                items.Add(new VelvetFitItem(VelvetFitKind.SharedTag, mine[index], 0));
                added++;
            }
        }
    }

    private static bool IrlConflict(VelvetProfileDto me, VelvetProfileDto other) =>
        (Contains(other.Limits, IrlLimit) && VelvetIntent.Has(me.LookingFor, VelvetIntent.Irl))
        || (Contains(me.Limits, IrlLimit) && VelvetIntent.Has(other.LookingFor, VelvetIntent.Irl));

    private static bool HasBoundariesToCompare(VelvetProfileDto me, VelvetProfileDto other) =>
        (other.Limits.Length > 0 && me.Kinks is { Length: > 0 })
        || (me.Limits.Length > 0 && other.Kinks is { Length: > 0 });

    private static bool HasConflict(List<VelvetFitItem> items, string token)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Kind == VelvetFitKind.Conflict
                && string.Equals(items[index].Token, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string[]? tokens, string token)
    {
        if (tokens is null)
        {
            return false;
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            if (string.Equals(tokens[index], token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
