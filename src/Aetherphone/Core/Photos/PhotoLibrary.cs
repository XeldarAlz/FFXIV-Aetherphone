namespace Aetherphone.Core.Photos;

internal sealed class PhotoLibrary
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
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

        var files = new List<string>();
        for (var index = 0; index < Extensions.Length; index++)
        {
            files.AddRange(Directory.GetFiles(directory, "*" + Extensions[index]));
        }

        var result = files.ToArray();
        Array.Sort(result, static (left, right) => string.CompareOrdinal(right, left));
        return result;
    }

    public static bool IsSupported(string path)
    {
        var extension = Path.GetExtension(path);
        for (var index = 0; index < Extensions.Length; index++)
        {
            if (string.Equals(extension, Extensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string? Import(string sourcePath, DateTime takenLocal)
    {
        if (!IsSupported(sourcePath))
        {
            return null;
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var stamp = takenLocal.AddMilliseconds(attempt);
            var target = Path.Combine(directory, $"AEP_{stamp:yyyyMMdd_HHmmss_fff}{extension}");
            if (File.Exists(target))
            {
                continue;
            }

            try
            {
                File.Copy(sourcePath, target);
                return target;
            }
            catch (IOException) when (File.Exists(target))
            {
                continue;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error(exception, "[Photos] failed to import a capture");
                return null;
            }
        }

        AepLog.Warning($"[Photos] could not find a free name to import '{Path.GetFileName(sourcePath)}'");
        return null;
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
