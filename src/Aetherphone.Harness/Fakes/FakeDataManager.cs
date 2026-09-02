using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeDataManager : IDataManager
{
    public FakeDataManager(string? sqpackDirectory)
    {
        if (string.IsNullOrEmpty(sqpackDirectory) || !Directory.Exists(sqpackDirectory))
        {
            return;
        }

        Store = new GameData(sqpackDirectory, new LuminaOptions
        {
            LoadMultithreaded = false,
            PanicOnSheetChecksumMismatch = false,
            DefaultExcelLanguage = global::Lumina.Data.Language.English,
        });
    }

    public GameData? Store { get; }

    public bool HasGameData => Store is not null;

    public ClientLanguage Language => ClientLanguage.English;

    public GameData GameData => Store ?? throw Unavailable();

    public ExcelModule Excel => GameData.Excel;

    public bool HasModifiedGameDataFiles => false;

    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null)
        where T : struct, IExcelRow<T> =>
        GameData.GetExcelSheet<T>(ToLumina(language), name) ?? throw Unavailable();

    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null)
        where T : struct, IExcelSubrow<T> =>
        GameData.GetSubrowExcelSheet<T>(ToLumina(language), name) ?? throw Unavailable();

    public FileResource? GetFile(string path) => Store?.GetFile(path);

    public T? GetFile<T>(string path)
        where T : FileResource => Store?.GetFile<T>(path);

    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken)
        where T : FileResource => Task.FromResult(GetFile<T>(path) ?? throw Unavailable());

    public bool FileExists(string path) => Store?.FileExists(path) ?? false;

    private static global::Lumina.Data.Language? ToLumina(ClientLanguage? language) => language switch
    {
        ClientLanguage.Japanese => global::Lumina.Data.Language.Japanese,
        ClientLanguage.English => global::Lumina.Data.Language.English,
        ClientLanguage.German => global::Lumina.Data.Language.German,
        ClientLanguage.French => global::Lumina.Data.Language.French,
        _ => null,
    };

    private static InvalidOperationException Unavailable() => new(
        "Game data is not available in the harness. Copy sqpack/ffxiv/0a0000.win32.* from a game install into the harness sqpack directory.");
}
