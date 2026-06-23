using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core.Net;

internal sealed class DiskCache
{
    private readonly DirectoryInfo root;
    private readonly long maxBytes;
    private readonly object sync = new();

    public DiskCache(DirectoryInfo root, long maxBytes)
    {
        this.root = root;
        this.maxBytes = maxBytes;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public byte[]? Get(string key, TimeSpan maxAge)
    {
        try
        {
            var info = new FileInfo(PathFor(key));
            if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc > maxAge)
            {
                return null;
            }

            return File.ReadAllBytes(info.FullName);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"DiskCache read failed for {key}: {exception.Message}");
            return null;
        }
    }

    public void Set(string key, byte[] bytes)
    {
        try
        {
            lock (sync)
            {
                File.WriteAllBytes(PathFor(key), bytes);
                EnforceBudget();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"DiskCache write failed for {key}: {exception.Message}");
        }
    }

    private void EnforceBudget()
    {
        var files = root.GetFiles();
        long total = 0;
        for (var index = 0; index < files.Length; index++)
        {
            total += files[index].Length;
        }

        if (total <= maxBytes)
        {
            return;
        }

        Array.Sort(files, static (left, right) => left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc));
        for (var index = 0; index < files.Length && total > maxBytes; index++)
        {
            total -= files[index].Length;
            try
            {
                files[index].Delete();
            }
            catch (Exception exception)
            {
                AepLog.Warning($"DiskCache eviction skipped {files[index].Name}: {exception.Message}");
            }
        }
    }

    private string PathFor(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var builder = new StringBuilder(hash.Length * 2);
        for (var index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2"));
        }

        return Path.Combine(root.FullName, builder.ToString());
    }
}
