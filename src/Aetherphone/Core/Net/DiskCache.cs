using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core.Net;

internal sealed class DiskCache
{
    public const string ProtectedMarkerName = ".protected";
    private const int MagicLength = 4;
    private static readonly byte[] ProtectedMagic = "AEC1"u8.ToArray();
    private static readonly byte[] ProtectionEntropy = "Aetherphone.DiskCache.v1"u8.ToArray();
    private readonly DirectoryInfo root;
    private readonly long maxBytes;
    private readonly bool protect;
    private readonly object sync = new();

    public DiskCache(DirectoryInfo root, long maxBytes, bool protect = false)
    {
        this.root = root;
        this.maxBytes = maxBytes;
        this.protect = protect;
        if (!root.Exists)
        {
            root.Create();
        }

        if (protect)
        {
            PurgeLegacyPlaintext();
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

            if (!protect)
            {
                return File.ReadAllBytes(info.FullName);
            }

            var opened = ReadProtected(info.FullName);
            if (opened is null)
            {
                Discard(info);
            }

            return opened;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"DiskCache read failed for {key}");
            return null;
        }
    }

    public void Set(string key, byte[] bytes)
    {
        try
        {
            var sealedBytes = protect ? Seal(bytes) : null;
            lock (sync)
            {
                if (sealedBytes is null)
                {
                    File.WriteAllBytes(PathFor(key), bytes);
                }
                else
                {
                    WriteProtected(PathFor(key), sealedBytes);
                }

                EnforceBudget();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"DiskCache write failed for {key}");
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            var files = root.GetFiles();
            for (var index = 0; index < files.Length; index++)
            {
                if (IsMarker(files[index]))
                {
                    continue;
                }

                Discard(files[index]);
            }
        }
    }

    public long SizeBytes()
    {
        try
        {
            root.Refresh();
            if (!root.Exists)
            {
                return 0;
            }

            var files = root.GetFiles();
            long total = 0;
            for (var index = 0; index < files.Length; index++)
            {
                if (IsMarker(files[index]))
                {
                    continue;
                }

                total += files[index].Length;
            }

            return total;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"DiskCache size check failed for {root.Name}");
            return 0;
        }
    }

    private static byte[] Seal(byte[] bytes)
    {
        return ProtectedData.Protect(bytes, ProtectionEntropy, DataProtectionScope.CurrentUser);
    }

    private static void WriteProtected(string path, byte[] sealedBytes)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(ProtectedMagic);
        stream.Write(sealedBytes);
    }

    private static byte[]? ReadProtected(string path)
    {
        byte[] sealedBytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var length = stream.Length;
            if (length <= MagicLength)
            {
                return null;
            }

            Span<byte> magic = stackalloc byte[MagicLength];
            stream.ReadExactly(magic);
            if (!magic.SequenceEqual(ProtectedMagic))
            {
                return null;
            }

            sealedBytes = new byte[length - MagicLength];
            stream.ReadExactly(sealedBytes);
        }

        try
        {
            return ProtectedData.Unprotect(sealedBytes, ProtectionEntropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private void PurgeLegacyPlaintext()
    {
        try
        {
            var marker = new FileInfo(Path.Combine(root.FullName, ProtectedMarkerName));
            if (marker.Exists)
            {
                return;
            }

            lock (sync)
            {
                var files = root.GetFiles();
                for (var index = 0; index < files.Length; index++)
                {
                    Discard(files[index]);
                }

                File.WriteAllBytes(marker.FullName, Array.Empty<byte>());
            }

            AepLog.Info($"[Media] purged {root.Name} cache files written before encryption at rest");
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"DiskCache legacy purge failed for {root.Name}");
        }
    }

    private static bool IsMarker(FileInfo file)
    {
        return string.Equals(file.Name, ProtectedMarkerName, StringComparison.Ordinal);
    }

    private static void Discard(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"DiskCache could not delete {file.Name}");
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
            if (IsMarker(files[index]))
            {
                continue;
            }

            total -= files[index].Length;
            try
            {
                files[index].Delete();
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"DiskCache eviction skipped {files[index].Name}");
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
