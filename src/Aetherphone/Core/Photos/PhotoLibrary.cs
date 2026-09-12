namespace Aetherphone.Core.Photos;

internal sealed class PhotoLibrary
{
    public const int TrashRetentionDays = 30;

    private const int FreeNameAttempts = 100;

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif" };
    private readonly string directory;
    private readonly string trashDirectory;

    public PhotoLibrary(DirectoryInfo configDirectory)
    {
        directory = Path.Combine(configDirectory.FullName, "Photos");
        trashDirectory = Path.Combine(directory, ".trash");
        Directory.CreateDirectory(directory);
    }

    public string DirectoryPath => directory;

    public void Save(byte[] pixels, int width, int height)
    {
        var path = FreePath(DateTime.Now, ".png");
        if (path is null)
        {
            return;
        }

        Task.Run(() => Write(path, pixels, width, height));
    }

    public string? SaveEdited(byte[] pixels, int width, int height)
    {
        var path = FreePath(DateTime.Now, ".png");
        if (path is null)
        {
            return null;
        }

        return Write(path, pixels, width, height) ? path : null;
    }

    private string? FreePath(DateTime stamp, string extension)
    {
        for (var attempt = 0; attempt < FreeNameAttempts; attempt++)
        {
            var candidate = Path.Combine(directory,
                $"AEP_{stamp.AddMilliseconds(attempt):yyyyMMdd_HHmmss_fff}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        AepLog.Warning($"[Photos] could not find a free photo name near {stamp:yyyyMMdd_HHmmss_fff}");
        return null;
    }

    public string[] List() => ListIn(directory);

    public string[] ListTrash() => ListIn(trashDirectory);

    private static string[] ListIn(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();
        for (var index = 0; index < Extensions.Length; index++)
        {
            files.AddRange(Directory.GetFiles(folder, "*" + Extensions[index]));
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
        for (var attempt = 0; attempt < FreeNameAttempts; attempt++)
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
            Directory.CreateDirectory(trashDirectory);
            var target = FreeSibling(trashDirectory, Path.GetFileName(path));
            File.Move(path, target);
            File.SetCreationTimeUtc(target, DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to move a photo to Recently Deleted");
        }
    }

    public DateTime ExpiresAt(string trashPath)
    {
        try
        {
            return File.GetCreationTime(trashPath).AddDays(TrashRetentionDays);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read when {Path.GetFileName(trashPath)} was deleted");
            return DateTime.Now;
        }
    }

    public string? Restore(string trashPath)
    {
        try
        {
            var target = FreeSibling(directory, Path.GetFileName(trashPath));
            File.Move(trashPath, target);
            return target;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to recover a photo");
            return null;
        }
    }

    public void DeletePermanently(string trashPath)
    {
        try
        {
            File.Delete(trashPath);
            var thumbnail = ThumbnailPathFor(trashPath);
            if (File.Exists(thumbnail))
            {
                File.Delete(thumbnail);
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to delete a photo");
        }
    }

    public void PurgeExpired()
    {
        var trashed = ListTrash();
        var now = DateTime.UtcNow;
        for (var index = 0; index < trashed.Length; index++)
        {
            DateTime deletedAt;
            try
            {
                deletedAt = File.GetCreationTimeUtc(trashed[index]);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"[Photos] could not read when {Path.GetFileName(trashed[index])} was deleted");
                continue;
            }

            if (now - deletedAt >= TimeSpan.FromDays(TrashRetentionDays))
            {
                DeletePermanently(trashed[index]);
            }
        }
    }

    private static string FreeSibling(string folder, string fileName)
    {
        var candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var attempt = 1; attempt < FreeNameAttempts; attempt++)
        {
            candidate = Path.Combine(folder, $"{stem}-{attempt}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{stem}-{Guid.NewGuid():N}{extension}");
    }

    private static bool Write(string path, byte[] pixels, int width, int height)
    {
        try
        {
            var encoded = PngWriter.Encode(pixels, width, height);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, encoded);
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Photos] failed to save photo");
            return false;
        }
    }
}
