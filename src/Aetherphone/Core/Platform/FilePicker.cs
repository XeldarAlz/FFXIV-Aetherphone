using Aetherphone.Core.Localization;
using Dalamud.Interface.ImGuiFileDialog;

namespace Aetherphone.Core.Platform;

internal static class FilePicker
{
    private const string ImageExtensions = "{.png,.jpg,.jpeg,.bmp},.*";
    private const string AudioExtensions = "{.mp3,.wav},.*";
    private static readonly FileDialogManager Manager = new();

    public static void Draw()
    {
        Manager.Draw();
    }

    public static Action<string>? ProblemReporter;

    public static void PickImage(string title, Action<string> onPicked)
    {
        var guarded = OnlyLocalFiles(onPicked);
        if (UsesNativeDialog)
        {
            NativeFileDialog.PickImage(title, guarded);
            return;
        }

        Open(title, Loc.T(L.Common.FileKindImages) + ImageExtensions,
            ExistingFolder(Environment.SpecialFolder.MyPictures), guarded);
    }

    public static void PickAudio(string title, Action<string> onPicked)
    {
        var guarded = OnlyLocalFiles(onPicked);
        if (UsesNativeDialog)
        {
            NativeFileDialog.PickAudio(title, guarded);
            return;
        }

        Open(title, Loc.T(L.Common.FileKindAudio) + AudioExtensions,
            ExistingFolder(Environment.SpecialFolder.MyMusic), guarded);
    }

    private static Action<string> OnlyLocalFiles(Action<string> onPicked) => path =>
    {
        if (NotYetDownloaded(path))
        {
            ProblemReporter?.Invoke(Loc.T(L.Common.FileNotDownloaded));
            return;
        }

        onPicked(path);
    };

    private static bool NotYetDownloaded(string path)
    {
        try
        {
            const uint offline = 0x00001000;
            const uint recallOnOpen = 0x00040000;
            const uint recallOnDataAccess = 0x00400000;
            var attributes = (uint)File.GetAttributes(path);
            return (attributes & (offline | recallOnOpen | recallOnDataAccess)) != 0;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Could not read attributes for {path}: {exception.Message}");
            return false;
        }
    }

    private static bool UsesNativeDialog => Plugin.Cfg?.UseNativeFileDialog ?? NativeFileDialog.IsSupported;

    private static void Open(string title, string filters, string startPath, Action<string> onPicked)
    {
        Manager.OpenFileDialog(title, filters, (success, paths) =>
        {
            if (!success || paths.Count == 0)
            {
                return;
            }

            var path = paths[0];
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            onPicked(path);
        }, 1, startPath, false);
    }

    private static string ExistingFolder(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : string.Empty;
    }
}
