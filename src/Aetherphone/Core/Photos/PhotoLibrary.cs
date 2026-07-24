namespace Aetherphone.Core.Photos;

internal sealed class PhotoLibrary
{
    private readonly string directory;

    public PhotoLibrary(DirectoryInfo configDirectory)
    {
        directory = Path.Combine(configDirectory.FullName, "Photos");
        Directory.CreateDirectory(directory);
    }

    public string DirectoryPath => directory;

    public void Save(byte[] pixels, int width, int height)
    {
        var fileName = $"AEP_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var path = Path.Combine(directory, fileName);
        Task.Run(() => Write(path, pixels, width, height));
    }

    public string[] List()
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        var files = Directory.GetFiles(directory, "*.png");
        Array.Sort(files, static (left, right) => string.CompareOrdinal(right, left));
        return files;
    }

    public string ThumbnailPathFor(string path)
    {
        return Path.Combine(directory, ".thumbs", Path.GetFileNameWithoutExtension(path) + ".jpg");
    }

    public void Delete(string path)
    {
        try
        {
            File.Delete(path);
            var thumbnail = ThumbnailPathFor(path);
            if (File.Exists(thumbnail))
            {
                File.Delete(thumbnail);
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to delete photo");
        }
    }

    private static void Write(string path, byte[] pixels, int width, int height)
    {
        try
        {
            var encoded = PngWriter.Encode(pixels, width, height);
            File.WriteAllBytes(path, encoded);
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to save photo");
        }
    }
}
