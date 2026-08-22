using Dalamud.Game;

namespace Aetherphone.Core.Hunts;

internal static class HuntClientLanguage
{
    public static string Key() => Plugin.DataManager.Language switch
    {
        ClientLanguage.Japanese => "ja",
        ClientLanguage.French => "fr",
        ClientLanguage.German => "de",
        _ => "en",
    };
}
