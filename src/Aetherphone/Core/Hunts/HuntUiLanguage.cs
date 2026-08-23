using Aetherphone.Core.Localization;
using Dalamud.Game;

namespace Aetherphone.Core.Hunts;

internal static class HuntUiLanguage
{
    public static string Key() => Loc.Current.Code switch
    {
        "de" => "de",
        "fr" => "fr",
        "ja" => "ja",
        _ => "en",
    };

    public static ClientLanguage GameClientLanguage() => Loc.Current.Code switch
    {
        "de" => ClientLanguage.German,
        "fr" => ClientLanguage.French,
        "ja" => ClientLanguage.Japanese,
        _ => ClientLanguage.English,
    };
}
