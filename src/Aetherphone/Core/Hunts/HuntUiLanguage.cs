using Aetherphone.Core.Localization;

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
}
