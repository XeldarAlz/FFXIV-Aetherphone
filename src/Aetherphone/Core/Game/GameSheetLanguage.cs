using Aetherphone.Core.Localization;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Exceptions;

namespace Aetherphone.Core.Game;

internal enum SheetLanguageOverride
{
    None,
    English,
    German,
    French,
    Japanese,
}

internal readonly record struct SheetLanguageGate(bool PreferPhoneLocale, string LocaleCode);

internal static class GameSheetLanguage
{
    public static SheetLanguageGate CurrentGate() =>
        new(Plugin.Cfg.PreferPhoneLocaleForGameData, Loc.Current.Code);

    public static ClientLanguage? Resolve(SheetLanguageOverride overrideLanguage = SheetLanguageOverride.None)
    {
        if (overrideLanguage != SheetLanguageOverride.None)
        {
            return overrideLanguage switch
            {
                SheetLanguageOverride.English => ClientLanguage.English,
                SheetLanguageOverride.German => ClientLanguage.German,
                SheetLanguageOverride.French => ClientLanguage.French,
                SheetLanguageOverride.Japanese => ClientLanguage.Japanese,
                _ => null,
            };
        }

        if (!Plugin.Cfg.PreferPhoneLocaleForGameData)
        {
            return null;
        }

        var phoneLanguage = Loc.Current.Code switch
        {
            "de" => ClientLanguage.German,
            "en" => ClientLanguage.English,
            "fr" => ClientLanguage.French,
            "ja" => ClientLanguage.Japanese,
            _ => (ClientLanguage?)null,
        };

        return phoneLanguage ?? Plugin.DataManager.Language;
    }

    public static ExcelSheet<T> GetLocalizedSheet<T>(this IDataManager data,
        SheetLanguageOverride overrideLanguage = SheetLanguageOverride.None) where T : struct, IExcelRow<T>
    {
        if (Resolve(overrideLanguage) is not { } language)
        {
            return data.GetExcelSheet<T>();
        }

        try
        {
            return data.GetExcelSheet<T>(language);
        }
        catch (UnsupportedLanguageException)
        {
            return language == ClientLanguage.English ? data.GetExcelSheet<T>() : data.GetExcelSheet<T>(ClientLanguage.English);
        }
    }
}
