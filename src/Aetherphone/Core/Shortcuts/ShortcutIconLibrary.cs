using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shortcuts;

internal sealed class ShortcutIconLibrary
{
    public const int IconSize = 256;

    private const string Extension = ".jpg";

    private readonly DirectoryInfo directory;
    private readonly Configuration configuration;
    private readonly WallpaperImageCache images;
    private readonly Dictionary<string, string> pathsById = new();

    public ShortcutIconLibrary(DirectoryInfo directory, Configuration configuration, WallpaperImageCache images)
    {
        this.directory = directory;
        this.configuration = configuration;
        this.images = images;
        directory.Create();

        var ids = configuration.CustomShortcutIconIds;
        var shortcuts = configuration.Shortcuts;
        var swept = false;
        for (var index = ids.Count - 1; index >= 0; index--)
        {
            var id = ids[index];
            var path = Path.Combine(directory.FullName, id + Extension);
            if (IsReferenced(shortcuts, id))
            {
                pathsById[id] = path;
                continue;
            }

            ids.RemoveAt(index);
            swept = true;
            DeleteFile(path, id);
        }

        if (swept)
        {
            configuration.Save();
        }
    }

    public static byte[] Bake(string sourcePath, WallpaperCrop crop) =>
        ImageProcessor.BakeSquare(sourcePath, crop, IconSize).Bytes;

    public string Commit(byte[] bakedBytes)
    {
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory.FullName, id + Extension);
        File.WriteAllBytes(path, bakedBytes);
        configuration.CustomShortcutIconIds.Add(id);
        configuration.Save();
        pathsById[id] = path;
        return id;
    }

    public string? Duplicate(string id)
    {
        if (!pathsById.TryGetValue(id, out var sourcePath))
        {
            return null;
        }

        var newId = Guid.NewGuid().ToString("N");
        var newPath = Path.Combine(directory.FullName, newId + Extension);
        File.Copy(sourcePath, newPath, true);
        configuration.CustomShortcutIconIds.Add(newId);
        configuration.Save();
        pathsById[newId] = newPath;
        return newId;
    }

    public void Remove(string id)
    {
        if (!pathsById.TryGetValue(id, out var path))
        {
            return;
        }

        configuration.CustomShortcutIconIds.Remove(id);
        configuration.Save();
        pathsById.Remove(id);
        DeleteFile(path, id);
    }

    public IDalamudTextureWrap? Icon(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        return pathsById.TryGetValue(id, out var path) ? images.Get(path) : null;
    }

    private static bool IsReferenced(List<ShortcutEntry> shortcuts, string id)
    {
        for (var index = 0; index < shortcuts.Count; index++)
        {
            if (shortcuts[index].IconImage == id)
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteFile(string path, string id)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Shortcuts] failed to delete custom icon {id}");
        }
    }
}
