using Aetherphone.Core.Localization;
using Dalamud.Game;

namespace Aetherphone.Core.Game;

internal static class GameSheetLanguage
{
    public static ClientLanguage Current() => Loc.Current.Code switch
    {
        "de" => ClientLanguage.German,
        "en" => ClientLanguage.English,
        "fr" => ClientLanguage.French,
        "ja" => ClientLanguage.Japanese,
        _ => ClientLanguage.English,
    };
}
