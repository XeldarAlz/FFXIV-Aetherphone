using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetFilterSelection
{
    public int Intent;
    public int Gender;
    public int Sexuality;
    public int Relationship;
    public string Region = string.Empty;

    public readonly HashSet<string> Roles = new();
    public readonly HashSet<string> Kinks = new();
    public readonly HashSet<string> Limits = new();
    public readonly HashSet<string> Tags = new();

    public bool Any =>
        Intent != 0 || Gender != 0 || Sexuality != 0 || Relationship != 0
        || Roles.Count > 0 || Kinks.Count > 0 || Limits.Count > 0 || Tags.Count > 0
        || Region.Length > 0;

    public void Clear()
    {
        Intent = 0;
        Gender = 0;
        Sexuality = 0;
        Relationship = 0;
        Region = string.Empty;
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
        CopyInto(stored.Roles, Roles);
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
        stored.Roles = new List<string>(Roles);
        stored.Kinks = new List<string>(Kinks);
        stored.Limits = new List<string>(Limits);
        stored.Tags = new List<string>(Tags);
    }

    public static VelvetDiscoverFilter Combine(VelvetFilterSelection include, VelvetFilterSelection exclude) =>
        new(VelvetIntent.Sanitize(include.Intent), VelvetIntent.Sanitize(exclude.Intent),
            VelvetGender.Sanitize(include.Gender), VelvetGender.Sanitize(exclude.Gender),
            VelvetSexuality.Sanitize(include.Sexuality), VelvetSexuality.Sanitize(exclude.Sexuality),
            include.Relationship, exclude.Relationship,
            include.Roles.ToArray(), exclude.Roles.ToArray(),
            include.Kinks.ToArray(), exclude.Kinks.ToArray(),
            include.Limits.ToArray(), exclude.Limits.ToArray(),
            include.Tags.ToArray(), exclude.Tags.ToArray());

    private static void CopyInto(List<string> source, HashSet<string> target)
    {
        for (var index = 0; index < source.Count; index++)
        {
            target.Add(source[index]);
        }
    }
}
