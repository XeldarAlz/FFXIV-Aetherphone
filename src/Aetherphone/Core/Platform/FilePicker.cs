using Dalamud.Interface.ImGuiFileDialog;

namespace Aetherphone.Core.Platform;

internal static class FilePicker
{
    private const string ImageFilters = "Images{.png,.jpg,.jpeg,.bmp},.*";
    private const string AudioFilters = "Audio{.mp3,.wav},.*";
    private static readonly FileDialogManager Manager = new();

    public static void Draw()
    {
        Manager.Draw();
    }

    public static void PickImage(string title, Action<string> onPicked)
    {
        Open(title, ImageFilters, ExistingFolder(Environment.SpecialFolder.MyPictures), onPicked);
    }

    public static void PickAudio(string title, Action<string> onPicked)
    {
        Open(title, AudioFilters, ExistingFolder(Environment.SpecialFolder.MyMusic), onPicked);
    }

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
