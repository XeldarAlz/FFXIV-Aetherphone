using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Music.Library;

internal readonly struct PlaylistCoverSet
{
    public static readonly PlaylistCoverSet None = new(string.Empty, string.Empty, string.Empty);

    public readonly string Master;
    public readonly string Small;
    public readonly string Tiny;

    private PlaylistCoverSet(string master, string small, string tiny)
    {
        Master = master;
        Small = small;
        Tiny = tiny;
    }

    public bool HasCover => Master.Length > 0;

    public string For(float drawnPixels)
    {
        if (drawnPixels <= PlaylistCovers.TinySize)
        {
            return Tiny;
        }

        return drawnPixels <= PlaylistCovers.SmallSize ? Small : Master;
    }

    public static PlaylistCoverSet From(string masterPath)
    {
        if (string.IsNullOrEmpty(masterPath))
        {
            return None;
        }

        return new PlaylistCoverSet(masterPath, PlaylistCovers.VariantPath(masterPath, PlaylistCovers.SmallSize),
            PlaylistCovers.VariantPath(masterPath, PlaylistCovers.TinySize));
    }
}

internal static class PlaylistCovers
{
    public const int MasterSize = 512;
    public const int SmallSize = 128;
    public const int TinySize = 64;
    private const string FolderName = "covers";
    private const string Extension = ".jpg";

    public static string Folder =>
        Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "Music", FolderName);

    public static string Save(string directory, string playlistId, string sourcePath, WallpaperCrop crop)
    {
        Directory.CreateDirectory(directory);
        var stamp = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var master = Path.Combine(directory, playlistId + "-" + stamp + Extension);
        WriteBaked(master, ImageProcessor.BakeSquareJpeg(sourcePath, crop, MasterSize).Bytes);
        WriteBaked(VariantPath(master, SmallSize), ImageProcessor.BakeSquareJpeg(sourcePath, crop, SmallSize).Bytes);
        WriteBaked(VariantPath(master, TinySize), ImageProcessor.BakeSquareJpeg(sourcePath, crop, TinySize).Bytes);
        return master;
    }

    public static string VariantPath(string masterPath, int size)
    {
        var stem = masterPath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? masterPath[..^Extension.Length]
            : masterPath;
        return stem + "." + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + Extension;
    }

    public static void Delete(string masterPath)
    {
        if (string.IsNullOrEmpty(masterPath) ||
            !Path.GetFullPath(masterPath).StartsWith(Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDelete(masterPath);
        TryDelete(VariantPath(masterPath, SmallSize));
        TryDelete(VariantPath(masterPath, TinySize));
    }

    private static void WriteBaked(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Debug(exception, $"Playlist cover cleanup skipped {path}");
        }
    }
}
