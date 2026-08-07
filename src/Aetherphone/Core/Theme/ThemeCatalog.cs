namespace Aetherphone.Core.Theme;

internal sealed record NamedColor(string Name, Vector4 Color);

internal static class ThemeCatalog
{
    public static readonly IReadOnlyList<NamedColor> Accents = new NamedColor[]
    {
        new("Violet", new Vector4(0.55f, 0.45f, 0.95f, 1f)), new("Blue", new Vector4(0.30f, 0.55f, 0.98f, 1f)),
        new("Green", new Vector4(0.20f, 0.78f, 0.45f, 1f)), new("Pink", new Vector4(0.95f, 0.40f, 0.65f, 1f)),
        new("Amber", new Vector4(0.96f, 0.65f, 0.20f, 1f)),
    };

    public const string DefaultCaseName = "Titanium";

    private static readonly PhoneCase[] BuiltInCases =
    {
        PhoneCase.Color(DefaultCaseName, new Vector4(0.145f, 0.145f, 0.170f, 1f)),
        PhoneCase.Art("Black", new Vector4(0.158f, 0.158f, 0.158f, 1f)),
        PhoneCase.Art("Blue", new Vector4(0.331f, 0.551f, 0.963f, 1f)),
        PhoneCase.Art("Green", new Vector4(0.255f, 0.767f, 0.461f, 1f)),
        PhoneCase.Art("Grey", new Vector4(0.371f, 0.371f, 0.371f, 1f)),
        PhoneCase.Art("Lavender", new Vector4(0.399f, 0.355f, 0.530f, 1f)),
        PhoneCase.Art("Pink", new Vector4(0.931f, 0.415f, 0.647f, 1f)),
        PhoneCase.Art("Purple", new Vector4(0.551f, 0.461f, 0.932f, 1f)),
        PhoneCase.Art("Teal", new Vector4(0.281f, 0.527f, 0.584f, 1f)),
        PhoneCase.Art("White", new Vector4(0.893f, 0.893f, 0.893f, 1f)),
        PhoneCase.Art("Yellow", new Vector4(0.943f, 0.647f, 0.255f, 1f)),
        PhoneCase.Art("BlackCatGradient", new Vector4(0.380f, 0.256f, 0.327f, 1f)),
        PhoneCase.Art("BruteBomberGradient", new Vector4(0.524f, 0.212f, 0.176f, 1f)),
        PhoneCase.Art("DancingGreenGradient", new Vector4(0.775f, 0.788f, 0.680f, 1f)),
        PhoneCase.Art("GridaniaGradient", new Vector4(0.867f, 0.754f, 0.469f, 1f)),
        PhoneCase.Art("HoneyBLovelyGradient", new Vector4(0.811f, 0.706f, 0.552f, 1f)),
        PhoneCase.Art("HowlingBladeGradient", new Vector4(0.501f, 0.513f, 0.445f, 1f)),
        PhoneCase.Art("LimsaGradient", new Vector4(0.785f, 0.399f, 0.236f, 1f)),
        PhoneCase.Art("LindwurmGradient", new Vector4(0.424f, 0.306f, 0.292f, 1f)),
        PhoneCase.Art("MoogleGradient", new Vector4(0.840f, 0.757f, 0.598f, 1f)),
        PhoneCase.Art("RedHotDeepBlueGradient", new Vector4(0.538f, 0.408f, 0.567f, 1f)),
        PhoneCase.Art("Solution9Gradient", new Vector4(0.165f, 0.235f, 0.456f, 1f)),
        PhoneCase.Art("SpheneGradient", new Vector4(0.886f, 0.891f, 0.714f, 1f)),
        PhoneCase.Art("SugarRiotGradient", new Vector4(0.263f, 0.451f, 0.555f, 1f)),
        PhoneCase.Art("TheTyrantGradient", new Vector4(0.583f, 0.411f, 0.461f, 1f)),
        PhoneCase.Art("TuliyollalGradient", new Vector4(0.644f, 0.558f, 0.380f, 1f)),
        PhoneCase.Art("UldahGradient", new Vector4(0.334f, 0.222f, 0.115f, 1f)),
        PhoneCase.Art("VampFataleGradient", new Vector4(0.482f, 0.136f, 0.227f, 1f)),
        PhoneCase.Art("WickedThunderGradient", new Vector4(0.662f, 0.571f, 0.718f, 1f)),
        PhoneCase.Art("Silkie", new Vector4(0.998f, 0.915f, 0.912f, 1f)),
        PhoneCase.Art("FatCat", new Vector4(0.772f, 0.730f, 0.672f, 1f)),
        PhoneCase.Art("CosmicEX", new Vector4(0.141f, 0.141f, 0.172f, 1f)),
        PhoneCase.Art("Caduceus", new Vector4(0.414f, 0.398f, 0.209f, 1f)),
        PhoneCase.Art("MagicalGirl", new Vector4(0.911f, 0.593f, 0.734f, 1f)),
        PhoneCase.Art("Atomos", new Vector4(0.830f, 0.574f, 0.691f, 1f)),
        PhoneCase.Art("BabyBat", new Vector4(0.201f, 0.174f, 0.196f, 1f)),
        PhoneCase.Art("DwarfRabbit", new Vector4(0.860f, 0.686f, 0.382f, 1f)),
        PhoneCase.Art("Enkidu", new Vector4(0.561f, 0.672f, 0.301f, 1f)),
        PhoneCase.Art("Horror", new Vector4(0.407f, 0.202f, 0.210f, 1f)),
        PhoneCase.Art("MoogleCase", new Vector4(0.964f, 0.964f, 0.964f, 1f)),
        PhoneCase.Art("Runic", new Vector4(0.414f, 0.296f, 0.491f, 1f)),
    };

    public static IReadOnlyList<PhoneCase> Cases { get; } = BuiltInCases;

    public static bool IsCustomAccent(string name) => name.Length > 0 && name[0] == '#';

    public static Vector4 ResolveAccent(string name) =>
        IsCustomAccent(name) && HexColor.TryParse(name, out var custom)
            ? custom
            : Accents[IndexOf(Accents, name)].Color;

    public static PhoneCase ResolveCase(string id) => Cases[IndexOf(Cases, id)];

    public static int IndexOf(IReadOnlyList<NamedColor> list, string name)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Name == name)
            {
                return index;
            }
        }

        return 0;
    }

    public static int IndexOf(IReadOnlyList<PhoneCase> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Id == id)
            {
                return index;
            }
        }

        return 0;
    }
}
