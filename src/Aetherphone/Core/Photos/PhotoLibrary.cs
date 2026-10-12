using Aetherphone.Core.Apps;
using Aetherphone.Core.Media;

namespace Aetherphone.Core.Photos;

internal sealed class PhotoLibrary
{
    public const int TrashRetentionDays = 30;
    public const int ThumbnailMaxDimension = 256;

    private const int FreeNameAttempts = 100;

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif" };
    private static readonly TimeSpan PastedLifetime = TimeSpan.FromDays(1);
    private readonly string directory;
    private readonly string trashDirectory;
    private readonly string pastedDirectory;
    private readonly LaunchIntent openRequest = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> pendingWrites =
        new(StringComparer.OrdinalIgnoreCase);
    private int version;

    public PhotoLibrary(DirectoryInfo configDirectory)
    {
        directory = Path.Combine(configDirectory.FullName, "Photos");
        trashDirectory = Path.Combine(directory, ".trash");
        pastedDirectory = Path.Combine(directory, ".pasted");
        Directory.CreateDirectory(directory);
    }

    public string DirectoryPath => directory;

    public int Version => Volatile.Read(ref version);

    public void RequestOpen(string path) => openRequest.Request(path);

    public bool TryConsumeOpen(out string path) => openRequest.TryConsume(out path);

    public string? Save(byte[] pixels, int width, int height)
    {
        var path = FreePath(directory, DateTime.Now, ".png");
        if (path is null)
        {
            return null;
        }

        pendingWrites[Path.GetFileName(path)] = 0;
        _ = Task.Run(() => WriteAndAnnounce(path, pixels, width, height));
        return path;
    }

    public void AddPendingNames(HashSet<string> names)
    {
        foreach (var name in pendingWrites.Keys)
        {
            names.Add(name);
        }
    }

    private void WriteAndAnnounce(string path, byte[] pixels, int width, int height)
    {
        try
        {
            if (!Write(path, pixels, width, height))
            {
                TryDelete(path);
            }
        }
        finally
        {
            pendingWrites.TryRemove(Path.GetFileName(path), out _);
            MarkChanged();
        }
    }

    public void MarkChanged() => Interlocked.Increment(ref version);

    public async Task<byte[]> ThumbnailBytesAsync(string path, CancellationToken token)
    {
        var thumbnailPath = ThumbnailPathFor(path);
        if (File.Exists(thumbnailPath) && File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(path))
        {
            return await File.ReadAllBytesAsync(thumbnailPath, token).ConfigureAwait(false);
        }

        var bytes = await Task.Run(() => ImageProcessor.BakeJpeg(path, ThumbnailMaxDimension).Bytes, token)
            .ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        var temp = string.Concat(thumbnailPath, ".", Guid.NewGuid().ToString("N"), ".tmp");
        await File.WriteAllBytesAsync(temp, bytes, token).ConfigureAwait(false);
        try
        {
            File.Move(temp, thumbnailPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Debug(exception, $"[Photos] thumbnail for {Path.GetFileName(path)} was written elsewhere first");
            TryDelete(temp);
        }

        return bytes;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not clean up {Path.GetFileName(path)}");
        }
    }

    public string? SaveEdited(byte[] pixels, int width, int height)
    {
        var path = FreePath(directory, DateTime.Now, ".png");
        if (path is null)
        {
            return null;
        }

        if (!Write(path, pixels, width, height))
        {
            return null;
        }

        MarkChanged();
        return path;
    }

    public string? SavePasted(byte[] pixels, int width, int height)
    {
        Directory.CreateDirectory(pastedDirectory);
        PrunePasted();
        var path = FreePath(pastedDirectory, DateTime.Now, ".png");
        return path is not null && Write(path, pixels, width, height) ? path : null;
    }

    private void PrunePasted()
    {
        if (!Directory.Exists(pastedDirectory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - PastedLifetime;
        var files = Directory.GetFiles(pastedDirectory);
        for (var index = 0; index < files.Length; index++)
        {
            if (File.GetLastWriteTimeUtc(files[index]) < cutoff)
            {
                TryDelete(files[index]);
            }
        }
    }

    private static string? FreePath(string folder, DateTime stamp, string extension)
    {
        for (var attempt = 0; attempt < FreeNameAttempts; attempt++)
        {
            var candidate = Path.Combine(folder,
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
                MarkChanged();
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

        PrunePasted();
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
