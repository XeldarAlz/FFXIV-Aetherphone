namespace Aetherphone.Core.Wallpapers;

internal static class BuiltInWallpapers
{
    public const string DefaultLightId = "BloomLight";
    public const string DefaultDarkId = "BloomDark";

    private static readonly (string RetiredId, string ReplacementId)[] Retired =
    {
        ("DuskLight", "BloomLight"),
        ("DuskDark", "BloomDark"),
        ("HaloLight", "CrystalLight"),
        ("HaloDark", "CrystalDark"),
        ("SkyLight", "CurrentLight"),
        ("SkyDark", "CurrentDark"),
        ("ShadowLight", "FrostLight"),
        ("ShadowDark", "FrostDark"),
    };

    public static string Replace(string id, ref bool changed)
    {
        for (var index = 0; index < Retired.Length; index++)
        {
            if (Retired[index].RetiredId != id)
            {
                continue;
            }

            changed = true;
            return Retired[index].ReplacementId;
        }

        return id;
    }
}
