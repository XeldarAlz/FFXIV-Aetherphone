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

    public static readonly IReadOnlyList<NamedColor> Cases = new NamedColor[]
    {
        new(DefaultCaseName, new Vector4(0.145f, 0.145f, 0.170f, 1f)),
        new("Graphite", new Vector4(0.085f, 0.085f, 0.095f, 1f)),
        new("Silver", new Vector4(0.700f, 0.710f, 0.745f, 1f)),
        new("Gold", new Vector4(0.660f, 0.530f, 0.300f, 1f)),
        new("Rose", new Vector4(0.720f, 0.500f, 0.480f, 1f)),
        new("Midnight", new Vector4(0.105f, 0.135f, 0.255f, 1f)),
        new("Jade", new Vector4(0.115f, 0.265f, 0.215f, 1f)),
        new("Coral", new Vector4(0.740f, 0.310f, 0.280f, 1f)),
        new("Lavender", new Vector4(0.480f, 0.420f, 0.680f, 1f)),
        new("Porcelain", new Vector4(0.880f, 0.880f, 0.905f, 1f)),
    };

    public static Vector4 ResolveAccent(string name) => Resolve(Accents, name);

    public static Vector4 ResolveCase(string name) => Resolve(Cases, name);

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

    private static Vector4 Resolve(IReadOnlyList<NamedColor> list, string name) => list[IndexOf(list, name)].Color;
}
