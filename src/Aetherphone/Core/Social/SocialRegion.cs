using Aetherphone.Core.Game;

namespace Aetherphone.Core.Social;

internal static class SocialRegion
{
    public static readonly string[] Codes = { "NA", "EU", "JP", "OCE" };

    public static bool IsValid(string code) => Array.IndexOf(Codes, code) >= 0;

    public static string AutoCode(GameData gameData)
    {
        var code = gameData.LocalRegionCode();
        return code.Length > 0 ? code : Codes[0];
    }

    public static string EffectiveCode(Configuration configuration, GameData gameData)
    {
        if (configuration.RegionManual && IsValid(configuration.ManualRegion))
        {
            return configuration.ManualRegion;
        }

        return AutoCode(gameData);
    }
}
