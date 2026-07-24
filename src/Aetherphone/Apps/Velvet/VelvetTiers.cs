using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Velvet;

internal static class VelvetPresence
{
    public const int Offline = 0;
    public const int Online = 1;
    public const int Away = 2;
    public const int Dnd = 3;

    public static string Label(int value) =>
        value switch
        {
            Online => Loc.T(L.Velvet.PresenceOnline),
            Away => Loc.T(L.Velvet.PresenceAway),
            Dnd => Loc.T(L.Velvet.PresenceDnd),
            _ => Loc.T(L.Velvet.PresenceOffline),
        };

    public static Vector4 Color(int value) =>
        value switch
        {
            Online => new Vector4(0.30f, 0.82f, 0.44f, 1f),
            Away => new Vector4(0.95f, 0.72f, 0.25f, 1f),
            Dnd => new Vector4(0.90f, 0.30f, 0.34f, 1f),
            _ => new Vector4(0.45f, 0.45f, 0.50f, 1f),
        };
}

internal static class VelvetRelationship
{
    public const int NotSaying = 0;
    public const int Single = 1;
    public const int Taken = 2;
    public const int Open = 3;
    public const int Complicated = 4;
    public const int Poly = 5;
    public static readonly int[] All = { NotSaying, Single, Taken, Poly, Open, Complicated };

    public static string Label(int value) =>
        value switch
        {
            Single => Loc.T(L.Velvet.RelSingle),
            Taken => Loc.T(L.Velvet.RelTaken),
            Poly => Loc.T(L.Velvet.RelPoly),
            Open => Loc.T(L.Velvet.RelOpen),
            Complicated => Loc.T(L.Velvet.RelComplicated),
            _ => Loc.T(L.Velvet.RelNotSaying),
        };
}

internal static class VelvetGender
{
    public const int None = 0;
    public const int Female = 1 << 0;
    public const int Male = 1 << 1;
    public const int Femboy = 1 << 2;
    public const int FemalePlus = 1 << 3;
    public const int MalePlus = 1 << 4;
    public const int Genderfluid = 1 << 6;
    public const int Nonbinary = 1 << 7;
    public const int Transgender = 1 << 8;

    public const int Mask = Female | Male | Femboy | FemalePlus | MalePlus | Genderfluid | Nonbinary | Transgender;

    public static readonly int[] All =
        { Male, Female, MalePlus, FemalePlus, Genderfluid, Nonbinary, Transgender, Femboy };

    public static bool Has(int mask, int flag) => (mask & flag) != 0;

    public static int Toggle(int mask, int flag) => (mask & flag) != 0 ? mask & ~flag : mask | flag;

    public static int Sanitize(int mask) => mask & Mask;

    public static string[] Labels(int mask)
    {
        mask = Sanitize(mask);
        if (mask == None)
        {
            return Array.Empty<string>();
        }

        var labels = new List<string>(All.Length);
        for (var index = 0; index < All.Length; index++)
        {
            if ((mask & All[index]) != 0)
            {
                labels.Add(Label(All[index]));
            }
        }

        return labels.ToArray();
    }

    public static string Label(int flag) =>
        flag switch
        {
            Female => Loc.T(L.Velvet.GenderFemale),
            Male => Loc.T(L.Velvet.GenderMale),
            Femboy => Loc.T(L.Velvet.GenderFemboy),
            FemalePlus => Loc.T(L.Velvet.GenderFemalePlus),
            MalePlus => Loc.T(L.Velvet.GenderMalePlus),
            Genderfluid => Loc.T(L.Velvet.GenderGenderfluid),
            Nonbinary => Loc.T(L.Velvet.GenderNonbinary),
            Transgender => Loc.T(L.Velvet.GenderTransgender),
            _ => string.Empty,
        };
}

internal static class VelvetSexuality
{
    public const int None = 0;
    public const int Straight = 1 << 0;
    public const int Gay = 1 << 1;
    public const int Bi = 1 << 2;
    public const int Pan = 1 << 3;
    public const int Asexual = 1 << 4;
    public const int Demisexual = 1 << 5;

    public const int Mask = Straight | Gay | Bi | Pan | Asexual | Demisexual;

    public static readonly int[] All = { Straight, Gay, Bi, Pan, Asexual, Demisexual };

    public static bool Has(int mask, int flag) => (mask & flag) != 0;

    public static int Toggle(int mask, int flag) => (mask & flag) != 0 ? mask & ~flag : mask | flag;

    public static int Sanitize(int mask) => mask & Mask;

    public static string[] Labels(int mask)
    {
        mask = Sanitize(mask);
        if (mask == None)
        {
            return Array.Empty<string>();
        }

        var labels = new List<string>(All.Length);
        for (var index = 0; index < All.Length; index++)
        {
            if ((mask & All[index]) != 0)
            {
                labels.Add(Label(All[index]));
            }
        }

        return labels.ToArray();
    }

    public static string Label(int flag) =>
        flag switch
        {
            Straight => Loc.T(L.Velvet.SexualityStraight),
            Gay => Loc.T(L.Velvet.SexualityGay),
            Bi => Loc.T(L.Velvet.SexualityBi),
            Pan => Loc.T(L.Velvet.SexualityPan),
            Asexual => Loc.T(L.Velvet.SexualityAsexual),
            Demisexual => Loc.T(L.Velvet.SexualityDemisexual),
            _ => string.Empty,
        };
}

internal static class VelvetConnectionState
{
    public const int None = 0;
    public const int OutgoingRequest = 1;
    public const int IncomingRequest = 2;
    public const int Connected = 3;
    public const int Blocked = 4;
}

internal static class VelvetPostAudience
{
    public const int Connections = 0;
    public const int Public = 1;
}

internal static class VelvetTags
{
    public static string[] Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(parts.Length);
        for (var index = 0; index < parts.Length; index++)
        {
            var tag = parts[index].ToLowerInvariant();
            if (tag.Length > 0 && !result.Contains(tag))
            {
                result.Add(tag);
            }
        }

        return result.ToArray();
    }

    public static string Join(string[] tags) => tags.Length == 0 ? string.Empty : string.Join(", ", tags);
}
