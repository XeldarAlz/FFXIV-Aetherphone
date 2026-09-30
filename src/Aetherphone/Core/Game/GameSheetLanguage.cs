using Aetherphone.Core.Localization;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Exceptions;

namespace Aetherphone.Core.Game;

internal readonly record struct SheetLanguageGate(ClientLanguage? Language);

internal static class GameSheetLanguage
{
    private static readonly HashSet<ClientLanguage> UnsupportedLanguages = new();
    private static readonly object UnsupportedLanguagesLock = new();

    public static SheetLanguageGate CurrentGate() => new(Resolve());

    public static ClientLanguage? Resolve(ClientLanguage? overrideLanguage = null)
    {
        if (overrideLanguage is { } forced)
        {
            return forced;
        }

        if (!Plugin.Cfg.PreferPhoneLocaleForGameData)
        {
            return null;
        }

        return Loc.Current.Code switch
        {
            "de" => ClientLanguage.German,
            "en" => ClientLanguage.English,
            "fr" => ClientLanguage.French,
            "ja" => ClientLanguage.Japanese,
            _ => null,
        };
    }

    public static ExcelSheet<T> GetLocalizedSheet<T>(this IDataManager data, ClientLanguage? overrideLanguage = null)
        where T : struct, IExcelRow<T> =>
        GetSheetForLanguage<T>(data, Resolve(overrideLanguage));

    public static ExcelSheet<T> GetLocalizedSheet<T>(this IDataManager data, SheetLanguageGate gate)
        where T : struct, IExcelRow<T> =>
        GetSheetForLanguage<T>(data, gate.Language);

    private static ExcelSheet<T> GetSheetForLanguage<T>(IDataManager data, ClientLanguage? language)
        where T : struct, IExcelRow<T>
    {
        if (language is not { } resolved)
        {
            return data.GetExcelSheet<T>();
        }

        lock (UnsupportedLanguagesLock)
        {
            if (UnsupportedLanguages.Contains(resolved))
            {
                return data.GetExcelSheet<T>();
            }
        }

        try
        {
            return data.GetExcelSheet<T>(resolved);
        }
        catch (UnsupportedLanguageException)
        {
            lock (UnsupportedLanguagesLock)
            {
                UnsupportedLanguages.Add(resolved);
            }

            return data.GetExcelSheet<T>();
        }
    }
}
