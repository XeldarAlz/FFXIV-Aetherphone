using System.Linq;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetFilterSelection
{
    private static readonly VelvetFilterSelection Nothing = new();

    public int Intent;
    public int Gender;
    public int Sexuality;
    public int Relationship;
    public int Race;
    public int RegionMask;
    public int Languages;

    public readonly HashSet<string> Roles = new();
    public readonly HashSet<string> Kinks = new();
    public readonly HashSet<string> Limits = new();
    public readonly HashSet<string> Tags = new();

    public bool AnyBesidesRegion =>
        Intent != 0 || Gender != 0 || Sexuality != 0 || Relationship != 0 || Race != 0 || Languages != 0
        || Roles.Count > 0 || Kinks.Count > 0 || Limits.Count > 0 || Tags.Count > 0;

    public bool Any => AnyBesidesRegion || RegionMask != 0;

    public void Clear()
    {
        Intent = 0;
        Gender = 0;
        Sexuality = 0;
        Relationship = 0;
        Race = 0;
        RegionMask = 0;
        Languages = 0;
        Roles.Clear();
        Kinks.Clear();
        Limits.Clear();
        Tags.Clear();
    }

    public void LoadFrom(VelvetMutePreferences stored)
    {
        Clear();
        Intent = stored.Intent;
        Gender = stored.Gender;
        Sexuality = stored.Sexuality;
        Relationship = stored.Relationship;
        Race = VelvetRace.Sanitize(stored.Race);
        RegionMask = stored.Region;
        Languages = VelvetLanguages.Sanitize(stored.Languages);
        CopyKnownInto(stored.Roles, Roles, VelvetRoles.Tokens);
        CopyInto(stored.Kinks, Kinks);
        CopyInto(stored.Limits, Limits);
        CopyInto(stored.Tags, Tags);
    }

    public void SaveInto(VelvetMutePreferences stored)
    {
        stored.Intent = Intent;
        stored.Gender = Gender;
        stored.Sexuality = Sexuality;
        stored.Relationship = Relationship;
        stored.Race = Race;
        stored.Region = RegionMask;
        stored.Languages = Languages;
        stored.Roles = new List<string>(Roles);
        stored.Kinks = new List<string>(Kinks);
        stored.Limits = new List<string>(Limits);
        stored.Tags = new List<string>(Tags);
    }

    public static VelvetDiscoverFilter MutesOnly(VelvetFilterSelection mutes) => Combine(Nothing, mutes);

    public static VelvetDiscoverFilter Combine(VelvetFilterSelection include, VelvetFilterSelection exclude) =>
        new(VelvetIntent.Sanitize(include.Intent), VelvetIntent.Sanitize(exclude.Intent),
            VelvetGender.Sanitize(include.Gender), VelvetGender.Sanitize(exclude.Gender),
            VelvetSexuality.Sanitize(include.Sexuality), VelvetSexuality.Sanitize(exclude.Sexuality),
            include.Relationship, exclude.Relationship,
            include.Roles.ToArray(), exclude.Roles.ToArray(),
            include.Kinks.ToArray(), exclude.Kinks.ToArray(),
            include.Limits.ToArray(), exclude.Limits.ToArray(),
            include.Tags.ToArray(), exclude.Tags.ToArray(),
            VelvetRace.Sanitize(include.Race), VelvetRace.Sanitize(exclude.Race),
            LanguagesInclude: VelvetLanguages.Sanitize(include.Languages),
            LanguagesExclude: VelvetLanguages.Sanitize(exclude.Languages));

    public static VelvetDiscoverFilter CombineForFeed(VelvetFilterSelection include, VelvetFilterSelection exclude) =>
        Combine(include, exclude) with
        {
            KinksInclude = Array.Empty<string>(),
            LimitsInclude = Array.Empty<string>(),
            TagsInclude = Array.Empty<string>(),
        };

    public static string[] ContentTokens(VelvetFilterSelection include)
    {
        var count = include.Kinks.Count + include.Limits.Count + include.Tags.Count;
        if (count == 0)
        {
            return Array.Empty<string>();
        }

        var tokens = new string[count];
        var cursor = 0;
        CopyTokens(include.Kinks, tokens, ref cursor);
        CopyTokens(include.Limits, tokens, ref cursor);
        CopyTokens(include.Tags, tokens, ref cursor);
        return tokens;
    }

    private static void CopyTokens(HashSet<string> source, string[] target, ref int cursor)
    {
        foreach (var token in source)
        {
            target[cursor++] = token;
        }
    }

    private static void CopyKnownInto(List<string> source, HashSet<string> target, string[] known)
    {
        for (var index = 0; index < source.Count; index++)
        {
            var token = source[index];
            if (Array.IndexOf(known, token) >= 0)
            {
                target.Add(token);
            }
        }
    }

    private static void CopyInto(List<string> source, HashSet<string> target)
    {
        for (var index = 0; index < source.Count; index++)
        {
            target.Add(source[index]);
        }
    }
}
