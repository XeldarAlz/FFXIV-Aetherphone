using System.Numerics;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet.Kit;

internal readonly record struct VelvetIntentDef(int Flag, string Label, string Blurb, Vector4 Hue, FontAwesomeIcon Icon);

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

    public const int Mask = Erp | Gpose | Relationship | Collab | Friends | Sharing | Wandering;

    public static readonly VelvetIntentDef[] All =
    {
        new(Erp, "ERP", "Erotic roleplay and scenes", new Vector4(0.898f, 0.102f, 0.357f, 1f), FontAwesomeIcon.Heart),
        new(Gpose, "GPose", "Group pose shoots and art", new Vector4(0.722f, 0.612f, 0.878f, 1f),
            FontAwesomeIcon.Camera),
        new(Relationship, "Relationship", "Something with feelings", new Vector4(0.890f, 0.604f, 0.416f, 1f),
            FontAwesomeIcon.HandHoldingHeart),
        new(Collab, "Collab", "Writing and story partners", new Vector4(0.647f, 0.482f, 0.839f, 1f),
            FontAwesomeIcon.Feather),
        new(Friends, "Friends", "Just here to make friends", new Vector4(0.420f, 0.780f, 0.753f, 1f),
            FontAwesomeIcon.Users),
        new(Sharing, "Sharing", "Trading photos and media", new Vector4(0.776f, 0.294f, 0.690f, 1f),
            FontAwesomeIcon.Image),
        new(Wandering, "Wandering", "Seeing who is around", new Vector4(0.549f, 0.627f, 0.878f, 1f),
            FontAwesomeIcon.Compass),
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
                return All[index].Label;
            }
        }

        return "Any";
    }

    public static string Describe(int mask)
    {
        mask = Sanitize(mask);
        if (mask == 0)
        {
            return "Open to anything";
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

            builder.Append(All[index].Label);
            count++;
        }

        return builder.ToString();
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

internal static class VelvetSuggestions
{
    public static readonly string[] Limits =
    {
        "no irl", "fade to black", "ask first", "no pain", "no gore", "sfw until trust", "no permadeath", "no non-con",
    };

    public static readonly VelvetTagCategory[] DynamicCategories =
    {
        new("Dominant", new Vector4(0.898f, 0.102f, 0.357f, 1f),
            new[] { "dominant", "soft dom", "sadist", "primal", "caregiver", "owner", "brat tamer" }),
        new("Submissive", new Vector4(0.647f, 0.482f, 0.839f, 1f),
            new[] { "submissive", "brat", "masochist", "little", "pet", "service", "rope bunny" }),
        new("Switch", new Vector4(0.420f, 0.780f, 0.753f, 1f),
            new[] { "switch", "vanilla" }),
    };

    public static readonly VelvetTagCategory[] TagCategories =
    {
        new("Tone", new Vector4(0.961f, 0.361f, 0.541f, 1f),
            new[] { "romantic", "passionate", "tender", "rough", "playful", "dark", "wholesome" }),
        new("Pace", new Vector4(0.890f, 0.604f, 0.416f, 1f),
            new[] { "slow burn", "long-term", "casual", "slice-of-life", "one-shot", "late night" }),
        new("Style", new Vector4(0.549f, 0.627f, 0.878f, 1f),
            new[] { "para", "multi-para", "literate", "walk-up", "tell-first", "lore-friendly", "canon", "oc",
                "immersive", "venue" }),
    };
}

internal readonly record struct VelvetTagCategory(string Title, Vector4 Hue, string[] Tags);
