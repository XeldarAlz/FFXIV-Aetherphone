using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet.Kit;

internal readonly record struct VelvetIntentDef(int Flag, LocString Label, LocString Blurb, Vector4 Hue,
    string Glyph);

internal static class VelvetIntent
{
    public const int Any = 0;
    public const int Erp = 1 << 0;
    public const int Gpose = 1 << 1;
    public const int Relationship = 1 << 2;
    public const int Collab = 1 << 3;
    public const int Friends = 1 << 4;
    public const int Sharing = 1 << 5;
    public const int Wandering = 1 << 6;
    public const int Irl = 1 << 7;
    public const int NonIrl = 1 << 8;

    public const int Mask = Erp | Gpose | Relationship | Collab | Friends | Sharing | Wandering | Irl | NonIrl;

    public static readonly VelvetIntentDef[] All =
    {
        new(Erp, L.Velvet.IntentErp, L.Velvet.IntentErpBlurb, new Vector4(0.898f, 0.102f, 0.357f, 1f),
            PhoneIcons.Heart),
        new(Gpose, L.Velvet.IntentGpose, L.Velvet.IntentGposeBlurb, new Vector4(0.722f, 0.612f, 0.878f, 1f),
            PhoneIcons.Camera),
        new(Relationship, L.Velvet.IntentRelationship, L.Velvet.IntentRelationshipBlurb,
            new Vector4(0.890f, 0.604f, 0.416f, 1f), PhoneIcons.HeartHandshake),
        new(Collab, L.Velvet.IntentCollab, L.Velvet.IntentCollabBlurb, new Vector4(0.647f, 0.482f, 0.839f, 1f),
            PhoneIcons.Feather),
        new(Friends, L.Velvet.IntentFriends, L.Velvet.IntentFriendsBlurb, new Vector4(0.420f, 0.780f, 0.753f, 1f),
            PhoneIcons.Users),
        new(Sharing, L.Velvet.IntentSharing, L.Velvet.IntentSharingBlurb, new Vector4(0.776f, 0.294f, 0.690f, 1f),
            PhoneIcons.Photo),
        new(Wandering, L.Velvet.IntentWandering, L.Velvet.IntentWanderingBlurb, new Vector4(0.549f, 0.627f, 0.878f, 1f),
            PhoneIcons.Compass),
        new(Irl, L.Velvet.IntentIrl, L.Velvet.IntentIrlBlurb, new Vector4(0.427f, 0.757f, 0.549f, 1f),
            PhoneIcons.World),
        new(NonIrl, L.Velvet.IntentNonIrl, L.Velvet.IntentNonIrlBlurb, new Vector4(0.502f, 0.588f, 0.851f, 1f),
            PhoneIcons.Gamepad),
    };

    public static bool Has(int mask, int flag) => (mask & flag) != 0;

    public static int Toggle(int mask, int flag) => (mask & flag) != 0 ? mask & ~flag : mask | flag;

    public static int Sanitize(int mask) => mask & Mask;

    public static bool IncludesErp(int mask) => (mask & Erp) != 0;

    public static Vector4 Hue(int flag)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Flag == flag)
            {
                return All[index].Hue;
            }
        }

        return new Vector4(0.718f, 0.682f, 0.769f, 1f);
    }

    public static string Label(int flag)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Flag == flag)
            {
                return Loc.T(All[index].Label);
            }
        }

        return Loc.T(L.Velvet.IntentAny);
    }

    public static string Describe(int mask)
    {
        mask = Sanitize(mask);
        if (mask == 0)
        {
            return Loc.T(L.Velvet.OpenToAnything);
        }

        var builder = new System.Text.StringBuilder();
        var count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if ((mask & All[index].Flag) == 0)
            {
                continue;
            }

            if (count > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Loc.T(All[index].Label));
            count++;
        }

        return builder.ToString();
    }

    private static readonly Dictionary<int, string> Summaries = new();
    private static LanguageInfo? summaryLanguage;

    public static string Summary(int mask)
    {
        mask = Sanitize(mask);
        if (!ReferenceEquals(summaryLanguage, Loc.Current))
        {
            summaryLanguage = Loc.Current;
            Summaries.Clear();
        }

        if (Summaries.TryGetValue(mask, out var cached))
        {
            return cached;
        }

        var summary = mask == 0 ? Loc.T(L.Velvet.OpenToAnything) : Loc.T(L.Velvet.LookingForOne, Describe(mask));
        Summaries[mask] = summary;
        return summary;
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

        return Any;
    }
}

internal readonly record struct VelvetRoleDef(string Token, LocString Label);

internal static class VelvetRoles
{
    public static readonly VelvetRoleDef[] All =
    {
        new("dom", L.Velvet.RoleDom),
        new("sub", L.Velvet.RoleSub),
        new("switch", L.Velvet.RoleSwitch),
    };

    public static readonly string[] Tokens = BuildTokens();

    public static bool TryLabel(string token, out string label)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Token, token, StringComparison.OrdinalIgnoreCase))
            {
                label = Loc.T(All[index].Label);
                return true;
            }
        }

        label = token;
        return false;
    }

    private static string[] BuildTokens()
    {
        var tokens = new string[All.Length];
        for (var index = 0; index < All.Length; index++)
        {
            tokens[index] = All[index].Token;
        }

        return tokens;
    }
}

internal sealed class VelvetTokenSet
{
    public readonly string[] Tokens;

    private readonly string[] labels;

    public VelvetTokenSet(string[] labels)
    {
        this.labels = labels;
        Tokens = new string[labels.Length];
        for (var index = 0; index < labels.Length; index++)
        {
            Tokens[index] = labels[index].ToLowerInvariant();
        }
    }

    public bool TryLabel(string token, out string label)
    {
        for (var index = 0; index < Tokens.Length; index++)
        {
            if (string.Equals(Tokens[index], token, StringComparison.OrdinalIgnoreCase))
            {
                label = labels[index];
                return true;
            }
        }

        label = token;
        return false;
    }
}

internal static class VelvetKinks
{
    private static readonly VelvetTokenSet Set = new(new[]
    {
        "Sadist", "Masochist", "Handler", "Pet", "Rigger", "Rope bunny", "Brat", "Brat tamer", "Master", "Slave",
        "Watersports", "Gangbang", "SFW till trust", "Vanilla",
    });

    public static string[] Tokens => Set.Tokens;

    public static bool TryLabel(string token, out string label) => Set.TryLabel(token, out label);
}

internal static class VelvetLimits
{
    private static readonly VelvetTokenSet Set = new(new[]
    {
        "Pain", "Permadeath", "Drugs", "Non-con", "IRL", "Fade to black", "Gore", "Vore", "Watersports", "Feet",
        "Anal",
    });

    public static string[] Tokens => Set.Tokens;

    public static bool TryLabel(string token, out string label) => Set.TryLabel(token, out label);
}

internal static class VelvetTokenLabels
{
    public static string Of(string token)
    {
        if (VelvetRoles.TryLabel(token, out var role))
        {
            return role;
        }

        if (VelvetKinks.TryLabel(token, out var kink))
        {
            return kink;
        }

        if (VelvetLimits.TryLabel(token, out var limit))
        {
            return limit;
        }

        return VelvetSuggestions.TryTagLabel(token, out var tag) ? tag : token;
    }
}

internal static class VelvetSuggestions
{
    private static readonly VelvetTokenSet Tone = new(new[]
    {
        "Romantic", "Passionate", "Tender", "Rough", "Playful", "Dark", "Wholesome",
    });

    private static readonly VelvetTokenSet Pace = new(new[]
    {
        "Slow burn", "Long-term", "Casual", "Slice-of-life", "One-shot", "Late night",
    });

    private static readonly VelvetTokenSet Style = new(new[]
    {
        "Para", "Multi-para", "Literate", "Walk-up", "Tell-first", "Lore-friendly", "Canon", "OC", "Immersive",
        "Venue",
    });

    public static readonly VelvetTagCategory[] TagCategories =
    {
        new(L.Velvet.CatTone, new Vector4(0.961f, 0.361f, 0.541f, 1f), Tone.Tokens),
        new(L.Velvet.CatPace, new Vector4(0.890f, 0.604f, 0.416f, 1f), Pace.Tokens),
        new(L.Velvet.CatStyle, new Vector4(0.549f, 0.627f, 0.878f, 1f), Style.Tokens),
    };

    public static readonly Vector4 KinkHue = new(0.647f, 0.482f, 0.839f, 1f);

    public static readonly VelvetTagCategory[] PostTagCategories = BuildPostTagCategories();

    public static readonly string[] PostTagTokens = BuildPostTagTokens();

    public static bool TryTagLabel(string token, out string label) =>
        Tone.TryLabel(token, out label) || Pace.TryLabel(token, out label) || Style.TryLabel(token, out label);

    private static string[] BuildPostTagTokens()
    {
        var count = 0;
        for (var index = 0; index < PostTagCategories.Length; index++)
        {
            count += PostTagCategories[index].Tags.Length;
        }

        var tokens = new string[count];
        var cursor = 0;
        for (var index = 0; index < PostTagCategories.Length; index++)
        {
            var tags = PostTagCategories[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                tokens[cursor++] = tags[tagIndex];
            }
        }

        return tokens;
    }

    private static VelvetTagCategory[] BuildPostTagCategories()
    {
        var categories = new VelvetTagCategory[TagCategories.Length + 2];
        categories[0] = new VelvetTagCategory(L.Velvet.CardKinks, KinkHue, VelvetKinks.Tokens);
        categories[1] = new VelvetTagCategory(L.Velvet.CardLimits, VelvetTheme.Gold, VelvetLimits.Tokens);
        for (var index = 0; index < TagCategories.Length; index++)
        {
            categories[index + 2] = TagCategories[index];
        }

        return categories;
    }
}

internal readonly record struct VelvetTagCategory(LocString Title, Vector4 Hue, string[] Tags);
